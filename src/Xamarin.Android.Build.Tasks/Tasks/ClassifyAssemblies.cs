using System;
using System.Collections.Generic;
using System.IO;
using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ClassifyAssemblies : Task
	{
		[Required]
		public string[] Assemblies { get; set; }

		[Required]
		public string [] ReferenceAssembliesDirectories { get; set; }

		[Output]
		public string [] ResolveLibraryProjectAssemblies { get; set; }

		[Output]
		public string [] AdditionalResourceAssemblies { get; set; }

		[Output]
		public string [] EmbeddedNativeLibraryAssemblies { get; set; }

		public override bool Execute ()
		{
			var resolveLibraryProjectAssemblies = new List<string> ();
			var additionalResourcesAssemblies   = new List<string> ();
			var embeddedNativeLibraryAssemblies = new List<string> ();

			using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: false)) {
				foreach (var dir in ReferenceAssembliesDirectories)
					resolver.SearchDirectories.Add (dir);

				foreach (var assembly in Assemblies) {
					var assembly_path = Path.GetDirectoryName (assembly);
					if (!resolver.SearchDirectories.Contains (assembly_path))
						resolver.SearchDirectories.Add (assembly_path);

					var assemblyDef = resolver.Load (assembly);
					//FIXME: error code?
					if (assemblyDef == null)
						throw new InvalidOperationException ("Failed to load assembly " + assembly);

					foreach (var m in assemblyDef.Modules) {
						foreach (var r in m.Resources) {
							switch (r.Name) {
								case "__AndroidNativeLibraries__.zip":
									embeddedNativeLibraryAssemblies.Add (assembly);
									break;
								case "__AndroidLibraryProjects__.zip":
									if (!resolveLibraryProjectAssemblies.Contains (assembly))
										resolveLibraryProjectAssemblies.Add (assembly);
									break;
								default:
									if (r.Name.EndsWith (".jar", StringComparison.InvariantCultureIgnoreCase)) {
										if (!resolveLibraryProjectAssemblies.Contains (assembly))
											resolveLibraryProjectAssemblies.Add (assembly);
									}
									break;
							}
						}
					}

					foreach (var ca in assemblyDef.CustomAttributes) {
						switch (ca.AttributeType.FullName) {
							case "Android.IncludeAndroidResourcesFromAttribute":
							case "Java.Interop.JavaLibraryReferenceAttribute":
							case "Android.NativeLibraryReferenceAttribute":
								additionalResourcesAssemblies.Add (assembly);
								break;
						}
					}
				}
			}

			ResolveLibraryProjectAssemblies = resolveLibraryProjectAssemblies.ToArray ();
			AdditionalResourceAssemblies = additionalResourcesAssemblies.ToArray ();
			EmbeddedNativeLibraryAssemblies = embeddedNativeLibraryAssemblies.ToArray ();

			return !Log.HasLoggedErrors;
		}
	}
}
