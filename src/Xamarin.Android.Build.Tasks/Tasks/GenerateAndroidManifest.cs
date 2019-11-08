using System;
using System.Collections.Generic;
using System.IO;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class GenerateAndroidManifest : JavaStubsBaseTask
	{
		public override string TaskPrefix => "GAM";

		public string AndroidSdkDir { get; set; }
		public string AndroidSdkPlatform { get; set; }
		public string ManifestTemplate { get; set; }
		public string [] MergedManifestDocuments { get; set; }
		public string MergedAndroidManifestOutput { get; set; }
		public bool Debug { get; set; }
		public bool MultiDex { get; set; }
		public string ApplicationName { get; set; }
		public string PackageName { get; set; }
		public string [] ManifestPlaceholders { get; set; }
		public bool NeedsInternet { get; set; }
		public bool InstantRunEnabled { get; set; }
		public bool EmbedAssemblies { get; set; }
		public bool UseSharedRuntime { get; set; }
		public string OutputDirectory { get; set; }
		public string ApplicationJavaClass { get; set; }
		public string BundledWearApplicationName { get; set; }

		public override bool RunTask ()
		{
			using (var resolver = new MetadataResolver ()) {
				Run (resolver);
			}

			return !Log.HasLoggedErrors;
		}

		void Run (MetadataResolver resolver)
		{
			List<string> assemblies = GetAssemblies ();

			foreach (var assembly in assemblies) {
				resolver.EnumerateTypes (assembly);
			}

			// Step 3 - Merge [Activity] and friends into AndroidManifest.xml
			var manifest = new ManifestDocument (ManifestTemplate, this.Log);

			//List<Mono.Cecil.TypeDefinition> java_types = null;
			List<Mono.Cecil.TypeDefinition> all_java_types = null;

			manifest.PackageName = PackageName;
			manifest.ApplicationName = ApplicationName ?? PackageName;
			manifest.Placeholders = ManifestPlaceholders;
			//manifest.Assemblies.AddRange (assemblies);
			manifest.Resolver = resolver;
			manifest.SdkDir = AndroidSdkDir;
			manifest.SdkVersion = AndroidSdkPlatform;
			manifest.Debug = Debug;
			manifest.MultiDex = MultiDex;
			manifest.NeedsInternet = NeedsInternet;
			manifest.InstantRunEnabled = InstantRunEnabled;

			var additionalProviders = manifest.Merge (all_java_types, ApplicationJavaClass, EmbedAssemblies, BundledWearApplicationName, MergedManifestDocuments);

			using (var stream = new MemoryStream ()) {
				manifest.Save (stream);

				// Only write the new manifest if it actually changed
				MonoAndroidHelper.CopyIfStreamChanged (stream, MergedAndroidManifestOutput);
			}

			// Create additional runtime provider java sources.
			string providerTemplateFile = UseSharedRuntime ? "MonoRuntimeProvider.Shared.java" : "MonoRuntimeProvider.Bundled.java";
			string providerTemplate = GetResource (providerTemplateFile);

			foreach (var provider in additionalProviders) {
				var contents = providerTemplate.Replace ("MonoRuntimeProvider", provider);
				var real_provider = Path.Combine (OutputDirectory, "src", "mono", provider + ".java");
				MonoAndroidHelper.CopyIfStringChanged (contents, real_provider);
			}

			// Create additional application java sources.
			StringWriter regCallsWriter = new StringWriter ();
			regCallsWriter.WriteLine ("\t\t// Application and Instrumentation ACWs must be registered first.");
			foreach (var type in java_types) {
				if (JavaNativeTypeManager.IsApplication (type) || JavaNativeTypeManager.IsInstrumentation (type)) {
					string javaKey = JavaNativeTypeManager.ToJniName (type).Replace ('/', '.');
					regCallsWriter.WriteLine ("\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);",
						type.GetAssemblyQualifiedName (), javaKey);
				}
			}
			regCallsWriter.Close ();

			var real_app_dir = Path.Combine (OutputDirectory, "src", "mono", "android", "app");
			string applicationTemplateFile = "ApplicationRegistration.java";
			SaveResource (applicationTemplateFile, applicationTemplateFile, real_app_dir,
				template => template.Replace ("// REGISTER_APPLICATION_AND_INSTRUMENTATION_CLASSES_HERE", regCallsWriter.ToString ()));

			if (Log.HasLoggedErrors) {
				Files.DeleteFile (MergedAndroidManifestOutput, Log);
			}
		}

		string GetResource (string resource)
		{
			using (var stream = GetType ().Assembly.GetManifestResourceStream (resource))
			using (var reader = new StreamReader (stream))
				return reader.ReadToEnd ();
		}

		void SaveResource (string resource, string filename, string destDir, Func<string, string> applyTemplate)
		{
			string template = GetResource (resource);
			template = applyTemplate (template);
			MonoAndroidHelper.CopyIfStringChanged (template, Path.Combine (destDir, filename));
		}
	}
}
