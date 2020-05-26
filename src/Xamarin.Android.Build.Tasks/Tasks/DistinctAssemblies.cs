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
	/// Removes duplicate .NET assemblies by MVID.
	/// Also sets some metadata:
	/// * %(FrameworkAssembly)=True to determine if framework or user assembly
	/// * %(HasMonoAndroidReference)=True for incremental build performance
	/// </summary>
	public class DistinctAssemblies : FilterAssemblies
	{
		public override string TaskPrefix => "DIAS";

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

						// Set metadata
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

			return !Log.HasLoggedErrors;
		}
	}
}
