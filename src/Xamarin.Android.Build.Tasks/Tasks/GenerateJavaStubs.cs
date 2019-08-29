// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;


using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.Diagnostics;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;

using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	using PackageNamingPolicyEnum   = PackageNamingPolicy;

	public class GenerateJavaStubs : CreateTypemaps
	{
		public override string TaskPrefix => "GJS";

		[Required]
		public ITaskItem [] FrameworkDirectories { get; set; }

		public string ManifestTemplate { get; set; }
		public string[] MergedManifestDocuments { get; set; }

		public bool Debug { get; set; }
		public bool MultiDex { get; set; }
		public string ApplicationName { get; set; }
		public string PackageName { get; set; }
		public string [] ManifestPlaceholders { get; set; }

		public string AndroidSdkDir { get; set; }

		public string AndroidSdkPlatform { get; set; }
		public string MergedAndroidManifestOutput { get; set; }

		public bool EmbedAssemblies { get; set; }
		public bool NeedsInternet   { get; set; }

		[Required]
		public string ResourceDirectory { get; set; }

		public string BundledWearApplicationName { get; set; }

		public string PackageNamingPolicy { get; set; }
		
		public string ApplicationJavaClass { get; set; }

		public override bool RunTask ()
		{
			try {
				// We're going to do 3 steps here instead of separate tasks so
				// we can share the list of JLO TypeDefinitions between them
				using (var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true)) {
					Run (res);
				}
			} catch (XamarinAndroidException e) {
				Log.LogCodedError (string.Format ("XA{0:0000}", e.Code), e.MessageWithoutCode);
				if (MonoAndroidHelper.LogInternalExceptions)
					Log.LogMessage (e.ToString ());
			}

			return !Log.HasLoggedErrors;
		}

		void Run (DirectoryAssemblyResolver res)
		{
			PackageNamingPolicy pnp;
			JavaNativeTypeManager.PackageNamingPolicy = Enum.TryParse (PackageNamingPolicy, out pnp) ? pnp : PackageNamingPolicyEnum.LowercaseHash;

			foreach (var dir in FrameworkDirectories) {
				if (Directory.Exists (dir.ItemSpec))
					res.SearchDirectories.Add (dir.ItemSpec);
			}

			// Put every assembly we'll need in the resolver
			foreach (var assembly in ResolvedAssemblies) {
				if (bool.TryParse (assembly.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {assembly.ItemSpec}");
					continue;
				}
				res.Load (assembly.ItemSpec);
			}

			// However we only want to look for JLO types in user code
			var assemblies = new List<string> ();
			foreach (var asm in ResolvedUserAssemblies) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				if (!assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (asm.ItemSpec)))
					continue;
				Log.LogDebugMessage ($"Adding {asm.ItemSpec} to assemblies.");
				assemblies.Add (asm.ItemSpec);
			}
			foreach (var asm in MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies)) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				if (!assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (asm.ItemSpec)))
					continue;
				Log.LogDebugMessage ($"Adding {asm.ItemSpec} to assemblies.");
				assemblies.Add (asm.ItemSpec);
			}

			// Step 1 - Find all the JLO types
			var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
				ErrorOnCustomJavaObject     = ErrorOnCustomJavaObject,
			};
			var all_java_types = scanner.GetJavaTypes (assemblies, res);

			WriteTypeMappings (all_java_types);

			var java_types = all_java_types
				.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t))
				.ToArray ();

			// Step 2 - Generate Java stub code
			Generator.CreateJavaSources (
				Log,
				java_types,
				JavaSourceOutputDirectory,
				ApplicationJavaClass,
				AndroidSdkPlatform,
				UseSharedRuntime,
				int.Parse (AndroidSdkPlatform) <= 10,
				ResolvedAssemblies.Any (assembly => Path.GetFileName (assembly.ItemSpec) == "Mono.Android.Export.dll"));
			if (Log.HasLoggedErrors)
				return;

			CreateAcwMap (java_types);

			// Step 3 - Merge [Activity] and friends into AndroidManifest.xml
			var manifest = new ManifestDocument (ManifestTemplate, this.Log);

			manifest.PackageName = PackageName;
			manifest.ApplicationName = ApplicationName ?? PackageName;
			manifest.Placeholders = ManifestPlaceholders;
			manifest.Assemblies.AddRange (assemblies);
			manifest.Resolver = res;
			manifest.SdkDir = AndroidSdkDir;
			manifest.SdkVersion = AndroidSdkPlatform;
			manifest.Debug = Debug;
			manifest.MultiDex = MultiDex;
			manifest.NeedsInternet = NeedsInternet;
			manifest.InstantRunEnabled = InstantRunEnabled;

			var additionalProviders = manifest.Merge (all_java_types, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments, isApplication: true);

			var stream = GetMemoryStream ();
			manifest.Save (stream);
			// Only write the new manifest if it actually changed
			MonoAndroidHelper.CopyIfStreamChanged (stream, MergedAndroidManifestOutput);

			WriteExtraJavaSources (java_types, additionalProviders);
		}
	}
}
