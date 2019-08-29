using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Generates AndroidManifest.xml, which may be an intermediate, partial manifest.
	/// * Refactored from GenerateJavaStubs.cs
	/// </summary>
	public class GenerateManifest : DirectoryAssemblyResolverTask
	{
		public override string TaskPrefix => "GMAN";

		[Required]
		public ITaskItem [] Assemblies { get; set; }
		[Required]
		public string [] ManifestOutput { get; set; }
		/// <summary>
		/// Is this the final manifest?
		/// </summary>
		public bool IsApplication { get; set; }
		public string PackageName { get; set; }
		public string ApplicationName { get; set; }
		public string [] ManifestPlaceholders { get; set; }
		public string ManifestTemplate { get; set; }
		public string AndroidSdkDirectory { get; set; }
		public string AndroidSdkPlatform { get; set; }
		public bool Debug { get; set; }
		public bool MultiDex { get; set; }
		public bool ErrorOnCustomJavaObject { get; set; }
		public bool EmbedAssemblies { get; set; }
		public bool NeedsInternet { get; set; }
		public bool InstantRunEnabled { get; set; }
		public string ApplicationJavaClass { get; set; }
		public string BundledWearApplicationName { get; set; }
		public string [] MergedManifestDocuments { get; set; }

		static readonly XElement EmptyManifest = new XElement ("manifest");

		public override bool RunTask ()
		{
			if (!IsApplication && Assemblies.Length != ManifestOutput.Length) {
				throw new ArgumentException ("source and destination count mismatch");
			}

			DirectoryAssemblyResolver resolver = GetResolver ();

			using (var stream = new MemoryStream ()) {
				for (int i = 0; i < ManifestOutput.Length; i++) {
					var output = ManifestOutput [i];
					List<TypeDefinition> types;

					var manifest = new ManifestDocument (ManifestTemplate, Log) {
						PackageName = PackageName,
						ApplicationName = ApplicationName ?? PackageName,
						Placeholders = ManifestPlaceholders,
						Resolver = resolver,
						SdkDir = AndroidSdkDirectory,
						SdkVersion = AndroidSdkPlatform,
						Debug = Debug,
						MultiDex = MultiDex,
						NeedsInternet = NeedsInternet,
						InstantRunEnabled = InstantRunEnabled,
					};
					if (IsApplication) {
						var asm = Assemblies [i];
						if (bool.TryParse (asm.GetMetadata (GenerateJavaStubs.AndroidSkipJavaStubGeneration), out bool value) && value) {
							Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
							EmptyManifest.Save (stream);
							if (MonoAndroidHelper.CopyIfStreamChanged (stream, output)) {
								Log.LogDebugMessage ($"Saving: {output}");
							}
							continue;
						}

						var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
							ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
						};
						types = scanner.GetJavaTypes (new [] { asm.ItemSpec }, resolver);
						manifest.Assemblies.Add (asm.ItemSpec);
					} else {
						var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
							ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
						};
						var assemblies = Assemblies.Select (a => a.ItemSpec).ToArray ();
						types = scanner.GetJavaTypes (assemblies, resolver);
						manifest.Assemblies.AddRange (assemblies);
					}

					var additionalProviders = manifest.Merge (types, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments, IsApplication);
					if (IsApplication) {
						CacheManifestProviders (additionalProviders);
					}

					stream.SetLength (0); // Reuse the stream
					manifest.Save (stream);
					if (MonoAndroidHelper.CopyIfStreamChanged (stream, output)) {
						Log.LogDebugMessage ($"Saving: {output}");
					} else {
						Log.LogDebugMessage ($"Skipping unchanged file: {output}");
						File.SetLastAccessTimeUtc (output, DateTime.UtcNow);
					}
				}
			}

			return !Log.HasLoggedErrors;
		}
	}
}
