using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Xamarin.Android.Tools;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	public class GenerateTypeMaps : DirectoryAssemblyResolverTask
	{
		public override string TaskPrefix => "GTM";

		[Required]
		public string[] SupportedAbis { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public bool GenerateNativeAssembly { get; set; }

		[Output]
		public string[] GeneratedBinaryTypeMaps { get; set; }

		public override bool RunTask ()
		{
			try {
				DirectoryAssemblyResolver res = GetResolver ();
				List<TypeDefinition> javaTypes = GetJavaTypes ();
				var tmg = new TypeMapGenerator ((string message) => Log.LogDebugMessage (message), SupportedAbis);
				if (!tmg.Generate (res, Assemblies, javaTypes, OutputDirectory, GenerateNativeAssembly))
					throw new XamarinAndroidException (9999, "Failed to generate type maps");
				GeneratedBinaryTypeMaps = tmg.GeneratedBinaryTypeMaps.ToArray ();
			} catch (XamarinAndroidException e) {
				Log.LogCodedError (string.Format ("XA{0:0000}", e.Code), e.MessageWithoutCode);
				if (MonoAndroidHelper.LogInternalExceptions)
					Log.LogMessage (e.ToString ());
			}

			if (Log.HasLoggedErrors) {
				CleanupOnError (OutputDirectory);
				return false;
			}

			return true;
		}

		void CleanupOnError (string outputDirectory)
		{
			// Ensure that on a rebuild, we don't *skip* the `_GenerateJavaStubs` target,
			// by ensuring that the target outputs have been deleted.
			Files.DeleteFile (Path.Combine (outputDirectory, "typemap.index"), Log);
			foreach (string file in Directory.EnumerateFiles (outputDirectory, "*.typemap")) {
				Files.DeleteFile (file, Log);
			}
		}
	}
}
