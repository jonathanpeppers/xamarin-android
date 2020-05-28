using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Processes .dll files coming from @(ResolvedFileToPublish). Removes duplicate .NET assemblies by MVID.
	/// 
	/// Also sets some metadata:
	/// * %(FrameworkAssembly)=True to determine if framework or user assembly
	/// * %(HasMonoAndroidReference)=True for incremental build performance
	/// * %(AbiDirectory) if an assembly has an architecture-specific version
	/// </summary>
	public class ProcessAssemblies : FilterAssemblies
	{
		public override string TaskPrefix => "PRAS";

		public override bool RunTask ()
		{
			var output = new Dictionary<Guid, ITaskItem> ();

			foreach (var assembly in InputAssemblies) {
				using (var pe = new PEReader (File.OpenRead (assembly.ItemSpec))) {
					var reader = pe.GetMetadataReader ();
					var module = reader.GetModuleDefinition ();
					var mvid = reader.GetGuid (module.Mvid);
					if (!output.ContainsKey (mvid)) {
						output.Add (mvid, assembly);

						// Set metadata, such as %(FrameworkAssembly) and %(HasMonoAndroidReference)
						string packageId = assembly.GetMetadata ("NuGetPackageId");
						bool frameworkAssembly = packageId.StartsWith ("Microsoft.NETCore.App.Runtime.") ||
							packageId.StartsWith ("Microsoft.Android.Runtime.");
						assembly.SetMetadata ("FrameworkAssembly", frameworkAssembly.ToString ());
						assembly.SetMetadata ("HasMonoAndroidReference", HasReference (reader).ToString ());
					} else {
						Log.LogDebugMessage ($"Removing duplicate: {assembly.ItemSpec}");
					}
				}
			}

			OutputAssemblies = output.Values.ToArray ();

			// Set %(AbiDirectory) for architecture-specific assemblies

			var fileNames = new Dictionary<string, ITaskItem> (StringComparer.OrdinalIgnoreCase);
			foreach (var assembly in OutputAssemblies) {
				var fileName = Path.GetFileName (assembly.ItemSpec);
				if (fileNames.TryGetValue (fileName, out ITaskItem other)) {
					SetAbiDirectory (assembly);
					SetAbiDirectory (other);
				} else {
					fileNames.Add (fileName, assembly);
				}
			}

			return !Log.HasLoggedErrors;
		}

		/// <summary>
		/// Sets %(AbiDirectory) based on %(RuntimeIdentifier)
		/// </summary>
		void SetAbiDirectory (ITaskItem assembly)
		{
			var rid = assembly.GetMetadata ("RuntimeIdentifier");
			var abi = MonoAndroidHelper.RuntimeIdentifierToAbi (rid);
			if (!string.IsNullOrEmpty (abi)) {
				assembly.SetMetadata ("AbiDirectory", abi);
			} else {
				Log.LogDebugMessage ($"Android ABI not found for: {assembly.ItemSpec}");
			}
		}
	}
}
