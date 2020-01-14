using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
    public class GenerateTypeMaps : AndroidTask
	{
		public override string TaskPrefix => "GTM";

		[Required]
		public ITaskItem[] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem[] ResolvedUserAssemblies { get; set; }

		[Required]
		public ITaskItem [] FrameworkDirectories { get; set; }

		[Required]
		public string[] SupportedAbis { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		[Required]
		public bool GenerateNativeAssembly { get; set; }

		[Output]
		public string[] GeneratedBinaryTypeMaps { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public override bool RunTask ()
		{
			try {
				var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true);
				foreach (var dir in FrameworkDirectories) {
					if (Directory.Exists (dir.ItemSpec))
						res.SearchDirectories.Add (dir.ItemSpec);
				}

				Run (res);
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

		void Run (DirectoryAssemblyResolver res)
		{
			var interestingAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			foreach (ITaskItem assembly in ResolvedAssemblies) {
				res.Load (assembly.ItemSpec);
				if (String.Compare ("MonoAndroid", assembly.GetMetadata ("TargetFrameworkIdentifier"), StringComparison.Ordinal) != 0)
					continue;

				if (Boolean.TryParse (assembly.GetMetadata (GenerateJavaStubs.AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping typemap Generation for {assembly.ItemSpec}");
					continue;
				}

				if (interestingAssemblies.Contains (assembly.ItemSpec))
					continue;

				interestingAssemblies.Add (assembly.ItemSpec);
			}

			var tmg = new TypeMapGenerator ((string message) => Log.LogDebugMessage (message), SupportedAbis);
			if (!tmg.Generate (res, interestingAssemblies, OutputDirectory, GenerateNativeAssembly))
				throw new XamarinAndroidException (99999, "Failed to generate type maps");
			GeneratedBinaryTypeMaps = tmg.GeneratedBinaryTypeMaps.ToArray ();
		}
	}
}
