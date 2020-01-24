using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Mono.Cecil;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;

namespace Xamarin.Android.Tasks
{
	class TypeMapGenerator
	{
		const string TypeMapMagicString = "XATM"; // Xamarin Android TypeMap
		const string TypeMapIndexMagicString = "XATI"; // Xamarin Android Typemap Index
		const uint TypeMapFormatVersion = 1; // Keep in sync with the value in src/monodroid/jni/xamarin-app.hh

		sealed class UUIDByteArrayComparer : IComparer<byte[]>
		{
			public int Compare (byte[] left, byte[] right)
			{
				int ret;

				for (int i = 0; i < 16; i++) {
					ret = left[i].CompareTo (right[i]);
					if (ret != 0)
						return ret;
				}

				return 0;
			}
		}

		internal sealed class TypeMapEntry
		{
			public string JavaName;
			public string ManagedTypeName;
			public uint Token;
			public int AssemblyNameIndex = -1;
			public int ModuleIndex = -1;
		}

		internal sealed class ModuleData
		{
			public Guid Mvid;
			public AssemblyDefinition Assembly;
			public SortedDictionary<string, TypeMapEntry> Types;
			public Dictionary<uint, TypeMapEntry> DuplicateTypes;
			public string AssemblyName;
			public string AssemblyNameLabel;
			public string OutputFilePath;
		}

		Action<string> logger;
		Encoding binaryEncoding;
		byte[] moduleMagicString;
		byte[] typemapIndexMagicString;
		string[] supportedAbis;

		public IList<string> GeneratedBinaryTypeMaps { get; } = new List<string> ();

		public TypeMapGenerator (Action<string> logger, string[] supportedAbis)
		{
			this.logger = logger ?? throw new ArgumentNullException (nameof (logger));
			if (supportedAbis == null)
				throw new ArgumentNullException (nameof (supportedAbis));
			if (supportedAbis.Length == 0)
				throw new ArgumentException ("must not be empty", nameof (supportedAbis));
			this.supportedAbis = supportedAbis;

			binaryEncoding = new UTF8Encoding (false);
			moduleMagicString = binaryEncoding.GetBytes (TypeMapMagicString);
			typemapIndexMagicString = binaryEncoding.GetBytes (TypeMapIndexMagicString);
		}

		public bool Generate (DirectoryAssemblyResolver resolver, IEnumerable<string> assemblies, List<TypeDefinition> javaTypes, string outputDirectory, bool generateNativeAssembly)
		{
			if (assemblies == null)
				throw new ArgumentNullException (nameof (assemblies));
			if (String.IsNullOrEmpty (outputDirectory))
				throw new ArgumentException ("must not be null or empty", nameof (outputDirectory));

			if (!Directory.Exists (outputDirectory))
				Directory.CreateDirectory (outputDirectory);

			int assemblyId = 0;
			int maxJavaNameLength = 0;
			int maxModuleFileNameLength = 0;
			var knownAssemblies = new Dictionary<string, int> (StringComparer.Ordinal);
			var modules = new SortedDictionary<byte[], ModuleData> (new UUIDByteArrayComparer ());
			Dictionary <AssemblyDefinition, int> moduleCounter = null;

			foreach (TypeDefinition td in javaTypes) {
				string assemblyName = td.Module.Assembly.FullName;

				if (!knownAssemblies.ContainsKey (assemblyName)) {
					assemblyId++;
					knownAssemblies.Add (assemblyName, assemblyId);
				}

				byte[] moduleUUID = td.Module.Mvid.ToByteArray ();
				ModuleData moduleData;
				if (!modules.TryGetValue (moduleUUID, out moduleData)) {
					if (moduleCounter == null)
						moduleCounter = new Dictionary <AssemblyDefinition, int> ();

					moduleData = new ModuleData {
						Mvid = td.Module.Mvid,
						Assembly = td.Module.Assembly,
						AssemblyName = td.Module.Assembly.Name.Name,
						Types = new SortedDictionary<string, TypeMapEntry> (StringComparer.Ordinal),
						DuplicateTypes = new Dictionary<uint, TypeMapEntry> (),
					};
					modules.Add (moduleUUID, moduleData);

					if (!generateNativeAssembly) {
						int moduleNum;
						if (!moduleCounter.TryGetValue (moduleData.Assembly, out moduleNum)) {
							moduleNum = 0;
							moduleCounter [moduleData.Assembly] = 0;
						} else {
							moduleNum++;
							moduleCounter [moduleData.Assembly] = moduleNum;
						}

						string fileName = $"{moduleData.Assembly.Name.Name}.{moduleNum}.typemap";
						moduleData.OutputFilePath = Path.Combine (outputDirectory, fileName);
						if (maxModuleFileNameLength < fileName.Length)
							maxModuleFileNameLength = fileName.Length;
					}
				}

				string javaName = Java.Interop.Tools.TypeNameMappings.JavaNativeTypeManager.ToJniName (td);
				if (generateNativeAssembly) {
					if (javaName.Length > maxJavaNameLength)
						maxJavaNameLength = javaName.Length;
				}

				var entry = new TypeMapEntry {
					JavaName = javaName,
					ManagedTypeName = td.FullName,
					Token = td.MetadataToken.ToUInt32 (),
					AssemblyNameIndex = knownAssemblies [assemblyName]
				};

				if (moduleData.Types.ContainsKey (entry.JavaName)) {
					logger ($"Warning: duplicate Java type name '{entry.JavaName}' (new token: {entry.Token}).");
					moduleData.DuplicateTypes.Add (entry.Token, entry);
				} else
					moduleData.Types.Add (entry.JavaName, entry);
			}

			NativeTypeMappingData data;
			if (!generateNativeAssembly) {
				string typeMapIndexPath = Path.Combine (outputDirectory, "typemap.index");
				// Try to approximate the index size:
				//   16 bytes for the header
				//   16 bytes (UUID) + filename length per each entry
				using (var ms = new MemoryStream (16 + (modules.Count * (16 + 128)))) {
					using (var indexWriter = new BinaryWriter (ms)) {
						OutputModules (outputDirectory, modules, indexWriter, maxModuleFileNameLength + 1);
						indexWriter.Flush ();
						MonoAndroidHelper.CopyIfStreamChanged (ms, typeMapIndexPath);
					}
				}
				GeneratedBinaryTypeMaps.Add (typeMapIndexPath);

				data = new NativeTypeMappingData (logger, new Dictionary<byte[], ModuleData> (), 0, 0);
			} else {
				data = new NativeTypeMappingData (logger, modules, javaTypes.Count, maxJavaNameLength + 1);
			}

			NativeAssemblerTargetProvider asmTargetProvider;
			bool sharedBitsWritten = false;
			bool sharedIncludeUsesAbiPrefix;
			foreach (string abi in supportedAbis) {
				sharedIncludeUsesAbiPrefix = false;
				switch (abi.Trim ()) {
					case "armeabi-v7a":
						asmTargetProvider = new ARMNativeAssemblerTargetProvider (is64Bit: false);
						sharedIncludeUsesAbiPrefix = true; // ARMv7a is "special", it uses different directive prefix
														   // than the others and the "shared" code won't build for it
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

				var generator = new TypeMappingNativeAssemblyGenerator (asmTargetProvider, data, Path.Combine (outputDirectory, "typemaps"), sharedBitsWritten, sharedIncludeUsesAbiPrefix);

				using (var ms = new MemoryStream ()) {
					using (var sw = new StreamWriter (ms, new UTF8Encoding (false))) {
						generator.Write (sw);
						sw.Flush ();
						MonoAndroidHelper.CopyIfStreamChanged (ms, generator.MainSourceFile);
						if (!sharedIncludeUsesAbiPrefix)
							sharedBitsWritten = true;
					}
				}
			}
			return true;
		}

		// Binary index file format, all data is little-endian:
		//
		//  [Magic string]             # XATI
		//  [Format version]           # 32-bit unsigned integer, 4 bytes
		//  [Entry count]              # 32-bit unsigned integer, 4 bytes
		//  [Module file name width]   # 32-bit unsigned integer, 4 bytes
		//  [Index entries]            # Format described below, [Entry count] entries
		//
		// Index entry format:
		//
		//  [Module UUID][File name]<NUL>
		//
		// Where:
		//
		//   [Module UUID] is 16 bytes long
		//   [File name] is right-padded with <NUL> characters to the [Module file name width] boundary.
		//
		void OutputModules (string outputDirectory, IDictionary<byte[], ModuleData> modules, BinaryWriter indexWriter, int moduleFileNameWidth)
		{
			var moduleCounter = new Dictionary <AssemblyDefinition, int> ();

			indexWriter.Write (typemapIndexMagicString);
			indexWriter.Write (TypeMapFormatVersion);
			indexWriter.Write (modules.Count);
			indexWriter.Write (moduleFileNameWidth);

			foreach (var kvp in modules) {
				byte[] mvid = kvp.Key;
				ModuleData data = kvp.Value;

				OutputModule (outputDirectory, mvid, data, moduleCounter);
				indexWriter.Write (mvid);

				string outputFilePath = Path.GetFileName (data.OutputFilePath);
				indexWriter.Write (binaryEncoding.GetBytes (outputFilePath));
				PadField (indexWriter, outputFilePath.Length, moduleFileNameWidth);
			}
		}

		void OutputModule (string outputDirectory, byte[] moduleUUID, ModuleData moduleData, Dictionary <AssemblyDefinition, int> moduleCounter)
		{
			if (moduleData.Types.Count == 0)
				return;

			int initialStreamSize =
				(36 + 128) + // Static header size + assembly file name
				((128 + 4) * moduleData.Types.Count) + // java-to-managed size
				(8 * moduleData.Types.Count) + // managed-to-java size
				(8 * moduleData.DuplicateTypes.Count); // managed-to-java duplicates;

			using (var ms = new MemoryStream (initialStreamSize)) {
				using (var bw = new BinaryWriter (ms)) {
					OutputModule (bw, moduleUUID, moduleData);
					bw.Flush ();
					MonoAndroidHelper.CopyIfStreamChanged (ms, moduleData.OutputFilePath);
				}
			}
			GeneratedBinaryTypeMaps.Add (moduleData.OutputFilePath);
		}

		// Binary file format, all data is little-endian:
		//
		//  [Magic string]                    # XATM
		//  [Format version]                  # 32-bit integer, 4 bytes
		//  [Module UUID]                     # 16 bytes
		//  [Entry count]                     # unsigned 32-bit integer, 4 bytes
		//  [Duplicate count]                 # unsigned 32-bit integer, 4 bytes (might be 0)
		//  [Java type name width]            # unsigned 32-bit integer, 4 bytes
		//  [Assembly name size]              # unsigned 32-bit integer, 4 bytes
		//  [Assembly name]                   # Non-null terminated assembly name
		//  [Java-to-managed map]             # Format described below, [Entry count] entries
		//  [Managed-to-java map]             # Format described below, [Entry count] entries
		//  [Managed-to-java duplicates map]  # Map of unique managed IDs which point to the same Java type name (might be empty)
		//
		// Java-to-managed map format:
		//
		//    [Java type name]<NUL>[Managed type token ID]
		//
		//  Each name is padded with <NUL> to the width specified in the [Java type name width] field above.
		//  Names are written without the size prefix, instead they are always terminated with a nul character
		//  to make it easier and faster to handle by the native runtime.
		//
		//  Each token ID is an unsigned 32-bit integer, 4 bytes
		//
		//
		// Managed-to-java map format:
		//
		//    [Managed type token ID][Java type name table index]
		//
		//  Both fields are unsigned 32-bit integers, to a total of 8 bytes per entry. Index points into the
		//  [Java-to-managed map] table above.
		//
		// Managed-to-java duplicates map format:
		//
		//  Format is identical to [Managed-to-java] above.
		//
		void OutputModule (BinaryWriter bw, byte[] moduleUUID, ModuleData moduleData)
		{
			bw.Write (moduleMagicString);
			bw.Write (TypeMapFormatVersion);
			bw.Write (moduleUUID);

			var javaNames = new SortedDictionary<string, uint> (StringComparer.Ordinal);
			var managedTypes = new SortedDictionary<uint, uint> ();
			int maxJavaNameLength = 0;

			foreach (var kvp in moduleData.Types) {
				TypeMapEntry entry = kvp.Value;

				javaNames.Add (entry.JavaName, entry.Token);
				if (entry.JavaName.Length > maxJavaNameLength)
					maxJavaNameLength = entry.JavaName.Length;

				managedTypes.Add (entry.Token, 0);
			}

			var javaNameList = javaNames.Keys.ToList ();
			foreach (var kvp in moduleData.Types) {
				TypeMapEntry entry = kvp.Value;
				var javaIndex = (uint)javaNameList.IndexOf (entry.JavaName);
				managedTypes[entry.Token] = javaIndex;
			}

			bw.Write (javaNames.Count);
			bw.Write (moduleData.DuplicateTypes.Count);
			bw.Write (maxJavaNameLength + 1);

			string assemblyName = moduleData.Assembly.Name.Name;
			bw.Write (assemblyName.Length);
			bw.Write (binaryEncoding.GetBytes (assemblyName));

			foreach (var kvp in javaNames) {
				string typeName = kvp.Key;
				uint token = kvp.Value;

				bw.Write (binaryEncoding.GetBytes (typeName));
				PadField (bw, typeName.Length, maxJavaNameLength + 1);
				bw.Write (token);
			}

			WriteManagedTypes (managedTypes);
			if (moduleData.DuplicateTypes.Count == 0)
				return;

			var managedDuplicates = new SortedDictionary<uint, uint> ();
			foreach (var kvp in moduleData.DuplicateTypes) {
				uint javaIndex = kvp.Key;
				uint typeId = kvp.Value.Token;

				managedDuplicates.Add (javaIndex, typeId);
			}

			WriteManagedTypes (managedDuplicates);

			void WriteManagedTypes (IDictionary<uint, uint> types)
			{
				foreach (var kvp in types) {
					uint token = kvp.Key;
					uint javaIndex = kvp.Value;

					bw.Write (token);
					bw.Write (javaIndex);
				}
			}
		}

		void PadField (BinaryWriter bw, int valueWidth, int maxWidth)
		{
			for (int i = 0; i < maxWidth - valueWidth; i++) {
				bw.Write ((byte)0);
			}
		}
	}
}
