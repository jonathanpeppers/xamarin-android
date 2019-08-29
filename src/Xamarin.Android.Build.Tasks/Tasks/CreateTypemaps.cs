using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A base class for generating all forms of typemaps:
	/// * acw-map.txt
	/// * typemap.mj/typemap.jm
	/// * typemap.mj.armeabi-v7a.s/typemap.jm.armeabi-v7a.s
	/// </summary>
	public class CreateTypemaps : DirectoryAssemblyResolverTask
	{
		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		static readonly Encoding Encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false);

		public override string TaskPrefix => "CRTM";

		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem [] ResolvedUserAssemblies { get; set; }

		[Required]
		public string AcwMapFile { get; set; }

		[Required]
		public string [] SupportedAbis { get; set; }

		[Required]
		public string TypemapOutputDirectory { get; set; }

		[Required]
		public string JavaSourceOutputDirectory { get; set; }

		public bool UseSharedRuntime { get; set; }

		public bool InstantRunEnabled { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		public override bool RunTask ()
		{
			var resolver = GetResolver ();

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
				ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
			};
			var all_java_types = scanner.GetJavaTypes (assemblies, resolver);

			// Step 2 - Write the typemap.mj/jm files
			WriteTypeMappings (all_java_types);

			var java_types = all_java_types
				.Where (t => !JavaTypeScanner.ShouldSkipJavaCallableWrapperGeneration (t))
				.ToArray ();

			// Step 3 - Write the acw-map.txt
			CreateAcwMap (java_types);

			// Step 4 - Write additional Java sources
			var additionalProviders = GetCachedManifestProviders () ?? Array.Empty<string> ();
			WriteExtraJavaSources (java_types, additionalProviders);

			return !Log.HasLoggedErrors;
		}

		protected void WriteTypeMappings (List<TypeDefinition> types)
		{
			void logger (TraceLevel level, string value) => Log.LogDebugMessage (value);
			TypeNameMapGenerator createTypeMapGenerator () => UseSharedRuntime ?
				new TypeNameMapGenerator (types, logger) :
				new TypeNameMapGenerator (ResolvedAssemblies.Select (p => p.ItemSpec), logger);
			using (var gen = createTypeMapGenerator ()) {
				UpdateWhenChanged (Path.Combine (TypemapOutputDirectory, "typemap.jm"), "jm", gen.WriteJavaToManaged);
				UpdateWhenChanged (Path.Combine (TypemapOutputDirectory, "typemap.mj"), "mj", gen.WriteManagedToJava);
			}
		}

		protected void CreateAcwMap (TypeDefinition [] java_types)
		{
			// We need to save a map of .NET type -> ACW type for resource file fixups
			var managed = new Dictionary<string, TypeDefinition> (java_types.Length, StringComparer.Ordinal);
			var java = new Dictionary<string, TypeDefinition> (java_types.Length, StringComparer.Ordinal);

			var managedConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);
			var javaConflicts = new Dictionary<string, List<string>> (0, StringComparer.Ordinal);

			// Allocate a MemoryStream with a reasonable guess at its capacity
			var stream = GetMemoryStream ();
			using (var acw_map = NewStreamWriter (stream)) {
				foreach (var type in java_types) {
					string managedKey = type.FullName.Replace ('/', '.');
					string javaKey = JavaNativeTypeManager.ToJniName (type).Replace ('/', '.');

					acw_map.Write (type.GetPartialAssemblyQualifiedName ());
					acw_map.Write (';');
					acw_map.Write (javaKey);
					acw_map.WriteLine ();

					bool hasConflict = false;
					if (managed.TryGetValue (managedKey, out TypeDefinition conflict)) {
						if (!managedConflicts.TryGetValue (managedKey, out var list))
							managedConflicts.Add (managedKey, list = new List<string> { conflict.GetPartialAssemblyName () });
						list.Add (type.GetPartialAssemblyName ());
						hasConflict = true;
					}
					if (java.TryGetValue (javaKey, out conflict)) {
						if (!javaConflicts.TryGetValue (javaKey, out var list))
							javaConflicts.Add (javaKey, list = new List<string> { conflict.GetAssemblyQualifiedName () });
						list.Add (type.GetAssemblyQualifiedName ());
						hasConflict = true;
					}
					if (!hasConflict) {
						managed.Add (managedKey, type);
						java.Add (javaKey, type);

						acw_map.Write (managedKey);
						acw_map.Write (';');
						acw_map.Write (javaKey);
						acw_map.WriteLine ();

						acw_map.Write (JavaNativeTypeManager.ToCompatJniName (type).Replace ('/', '.'));
						acw_map.Write (';');
						acw_map.Write (javaKey);
						acw_map.WriteLine ();
					}
				}

				acw_map.Flush ();
				MonoAndroidHelper.CopyIfStreamChanged (stream, AcwMapFile);
			}

			foreach (var kvp in managedConflicts) {
				Log.LogCodedWarning (
					"XA4214",
					"The managed type `{0}` exists in multiple assemblies: {1}. " +
					"Please refactor the managed type names in these assemblies so that they are not identical.",
					kvp.Key,
					string.Join (", ", kvp.Value));
				Log.LogCodedWarning ("XA4214", "References to the type `{0}` will refer to `{0}, {1}`.", kvp.Key, kvp.Value [0]);
			}

			foreach (var kvp in javaConflicts) {
				Log.LogCodedError (
					"XA4215",
					"The Java type `{0}` is generated by more than one managed type. " +
					"Please change the [Register] attribute so that the same Java type is not emitted.",
					kvp.Key);
				foreach (var typeName in kvp.Value)
					Log.LogCodedError ("XA4215", "  `{0}` generated by: {1}", kvp.Key, typeName);
			}
		}

		protected void WriteExtraJavaSources (TypeDefinition [] java_types, IList<string> additionalProviders)
		{
			// Create additional runtime provider java sources.
			string providerTemplateFile = UseSharedRuntime ? "MonoRuntimeProvider.Shared.java" : "MonoRuntimeProvider.Bundled.java";
			string providerTemplate = GetResource (providerTemplateFile);

			foreach (var provider in additionalProviders) {
				var contents = providerTemplate.Replace ("MonoRuntimeProvider", provider);
				var real_provider = Path.Combine (JavaSourceOutputDirectory, "mono", provider + ".java");
				MonoAndroidHelper.CopyIfStringChanged (contents, real_provider);
			}

			// Create additional application java sources.
			var regCallsWriter = new StringWriter ();
			regCallsWriter.WriteLine ("\t\t// Application and Instrumentation ACWs must be registered first.");
			foreach (var type in java_types) {
				if (JavaNativeTypeManager.IsApplication (type) || JavaNativeTypeManager.IsInstrumentation (type)) {
					string javaKey = JavaNativeTypeManager.ToJniName (type).Replace ('/', '.');
					regCallsWriter.WriteLine ("\t\tmono.android.Runtime.register (\"{0}\", {1}.class, {1}.__md_methods);",
						type.GetAssemblyQualifiedName (), javaKey);
				}
			}
			regCallsWriter.Close ();

			var real_app_dir = Path.Combine (JavaSourceOutputDirectory, "mono", "android", "app");
			string applicationTemplateFile = "ApplicationRegistration.java";
			SaveResource (applicationTemplateFile, applicationTemplateFile, real_app_dir,
				template => template.Replace ("// REGISTER_APPLICATION_AND_INSTRUMENTATION_CLASSES_HERE", regCallsWriter.ToString ()));
		}

		void UpdateWhenChanged (string path, string type, Action<Stream> generator)
		{
			var ms = GetMemoryStream ();
			if (InstantRunEnabled) {
				generator (ms);
				MonoAndroidHelper.CopyIfStreamChanged (ms, path);
			}

			string dataFilePath = $"{path}.inc";
			using (var stream = new NativeAssemblyDataStream ()) {
				generator (stream);
				stream.EndOfFile ();
				MonoAndroidHelper.CopyIfStreamChanged (stream, dataFilePath);

				var generatedFiles = new List<ITaskItem> ();
				string mappingFieldName = $"{type}_typemap";
				string dataFileName = Path.GetFileName (dataFilePath);
				NativeAssemblerTargetProvider asmTargetProvider;
				foreach (string abi in SupportedAbis) {
					ms.SetLength (0);
					switch (abi.Trim ()) {
						case "armeabi-v7a":
							asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: false);
							break;

						case "arm64-v8a":
							asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: true);
							break;

						case "x86":
							asmTargetProvider = new X86NativeAssemblerTargetProvider (is64Bit: false);
							break;

						case "x86_64":
							asmTargetProvider = new X86NativeAssemblerTargetProvider (is64Bit: true);
							break;

						default:
							throw new InvalidOperationException ($"Unknown ABI {abi}");
					}

					var asmgen = new TypeMappingNativeAssemblyGenerator (asmTargetProvider, stream, dataFileName, stream.MapByteCount, mappingFieldName);
					string asmFileName = $"{path}.{abi.Trim ()}.s";
					using (var sw = NewStreamWriter (ms)) {
						asmgen.Write (sw, dataFileName);
						MonoAndroidHelper.CopyIfStreamChanged (ms, asmFileName);
					}
				}
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

		MemoryStream memoryStream;

		protected MemoryStream GetMemoryStream ()
		{
			if (memoryStream == null)
				memoryStream = new MemoryStream ();
			else
				memoryStream.SetLength (0); // Ready for reuse
			return memoryStream;
		}

		protected StreamWriter NewStreamWriter (Stream stream) =>
			new StreamWriter (stream, Encoding, bufferSize: 8192, leaveOpen: true);
	}
}
