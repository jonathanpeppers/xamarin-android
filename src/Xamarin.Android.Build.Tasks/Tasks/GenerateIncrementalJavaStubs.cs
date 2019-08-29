using System;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class GenerateIncrementalJavaStubs : DirectoryAssemblyResolverTask
	{
		public override string TaskPrefix => "GIJS";

		[Required]
		public ITaskItem [] Assemblies { get; set; }

		[Required]
		public string [] OutputDirectories { get; set; }

		[Required]
		public string [] StampFiles { get; set; }

		public string AndroidSdkPlatform { get; set; }

		public string ApplicationJavaClass { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public bool UseSharedRuntime { get; set; }

		public override bool RunTask ()
		{
			if (Assemblies.Length != OutputDirectories.Length || Assemblies.Length != StampFiles.Length) {
				throw new ArgumentException ("source and destination count mismatch");
			}

			DirectoryAssemblyResolver resolver = GetResolver ();

			bool hasExportReference = Assemblies.Any (assembly => Path.GetFileName (assembly.ItemSpec) == "Mono.Android.Export.dll");

			for (int i = 0; i < Assemblies.Length; i++) {
				var assembly = Assemblies [i];
				var outputDir = OutputDirectories [i];
				var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
					ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
				};
				var all_java_types = scanner.GetJavaTypes (new [] { assembly.ItemSpec }, resolver);

				var java_types = all_java_types
					.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t))
					.ToArray ();

				var changes = Generator.CreateJavaSources (
					Log,
					java_types,
					outputDir,
					ApplicationJavaClass,
					AndroidSdkPlatform,
					UseSharedRuntime,
					generateOnCreateOverrides: false,
					hasExportReference);
				if (Log.HasLoggedErrors)
					return false;

				// Write to the stamp file if changes occurred, or it doesn't exist
				var stamp = StampFiles [i];
				if (changes || !File.Exists (stamp)) {
					Log.LogDebugMessage ($"Touching stamp file: {stamp}");
					File.WriteAllText (stamp, "");
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
