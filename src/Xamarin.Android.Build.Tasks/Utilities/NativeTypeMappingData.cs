using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	class NativeTypeMappingData
	{
		public IDictionary<byte[], TypeMapGenerator.ModuleData> Modules { get; }
		public IDictionary<string, string> AssemblyNames { get; }
		public IDictionary<string, TypeMapGenerator.TypeMapEntry> JavaTypes { get; }

		public uint MapModuleCount { get; }
		public uint JavaTypeCount  { get; }
		public uint JavaNameWidth  { get; }

		public NativeTypeMappingData (Action<string> logger, IDictionary<byte[], TypeMapGenerator.ModuleData> modules, int javaTypeCount, int javaNameWidth)
		{
			Modules = modules ?? throw new ArgumentNullException (nameof (modules));

			MapModuleCount = (uint)modules.Count;
			JavaTypeCount = (uint)javaTypeCount;
			JavaNameWidth = (uint)javaNameWidth;

			AssemblyNames = new SortedDictionary<string, string> (StringComparer.Ordinal);
			JavaTypes = new SortedDictionary<string, TypeMapGenerator.TypeMapEntry> (StringComparer.Ordinal);

			List<TypeMapGenerator.ModuleData> moduleList = modules.Values.ToList ();
			var duplicates = new SortedDictionary<string, uint> (StringComparer.Ordinal);
			int managedStringCounter = 0;
			foreach (var kvp in modules) {
				TypeMapGenerator.ModuleData data = kvp.Value;
				data.AssemblyNameLabel = $"map_aname.{managedStringCounter++}";
				AssemblyNames.Add (data.AssemblyNameLabel, data.AssemblyName);

				int moduleIndex = moduleList.IndexOf (data);
				foreach (var kvp2 in data.Types) {
					TypeMapGenerator.TypeMapEntry entry = kvp2.Value;
					entry.ModuleIndex = moduleIndex;
					if (!JavaTypes.ContainsKey (entry.JavaName)) {
						JavaTypes.Add (entry.JavaName, entry);
						continue;
					}

					uint duplicateCount;
					if (!duplicates.TryGetValue (entry.JavaName, out duplicateCount)) {
						duplicates.Add (entry.JavaName, 1);
					}
					duplicates[entry.JavaName]++;
				}
			}

			if (duplicates.Count == 0)
				return;

			logger ("Duplicate types were found across different assemblies while generating type maps:");
			foreach (var kvp in duplicates) {
				string javaTypeName = kvp.Key;
				uint count = kvp.Value;

				logger ($"  {javaTypeName}, {count} instances");
			}
		}
	}
}
