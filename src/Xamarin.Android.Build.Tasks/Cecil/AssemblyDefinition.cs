using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

namespace System.Reflection.Metadata.Cecil
{
	public class AssemblyDefinition : IDisposable
    {
		public static AssemblyDefinition ReadAssembly (string path)
		{
			return ReadAssembly (path);
		}

		readonly PEReader peReader;
		readonly MetadataReader reader;
		readonly Metadata.AssemblyDefinition assembly;

		private AssemblyDefinition (string path)
		{
			FileName = Path.GetFullPath (path);
			peReader = new PEReader (File.OpenRead (FileName));
			reader = peReader.GetMetadataReader ();
			resources = new Lazy<Resource []> (LoadResources);
			customAttributes = new Lazy<CustomAttribute []> (LoadCustomAttributes);
			references = new Lazy<AssemblyNameReference []> (LoadReferences);
			assembly = reader.GetAssemblyDefinition ();
			name = new Lazy<AssemblyName> (() => assembly.GetAssemblyName ());
			FullName = reader.GetString (assembly.Name);
		}

		public string FileName {
			get;
			private set;
		}

		public string FullName {
			get;
			private set;
		}

		readonly Lazy<AssemblyName> name;

		public AssemblyName Name => name.Value;

		Resource [] LoadResources ()
		{
			var list = new List<Resource> (reader.ManifestResources.Count);
			foreach (var r in reader.ManifestResources) {
				var resource = reader.GetManifestResource (r);
				list.Add (new Resource (reader, resource));
			}
			return list.ToArray ();
		}

		readonly Lazy<Resource []> resources;

		public Resource [] Resources => resources.Value;

		CustomAttribute [] LoadCustomAttributes ()
		{
			var list = new List<CustomAttribute> (reader.CustomAttributes.Count);
			foreach (var a in reader.CustomAttributes) {
				var attribute = reader.GetCustomAttribute (a);
				if (attribute.Constructor.Kind == HandleKind.MemberReference) {
					list.Add (new CustomAttribute (reader, attribute));
				}
			}
			return list.ToArray ();
		}

		readonly Lazy<CustomAttribute []> customAttributes;

		public CustomAttribute [] CustomAttributes => customAttributes.Value;

		AssemblyNameReference [] LoadReferences ()
		{
			var list = new List<AssemblyNameReference> (reader.AssemblyReferences.Count);
			foreach (var r in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (r);
				list.Add (new AssemblyNameReference (reader, reference));
			}
			return list.ToArray ();
		}

		readonly Lazy<AssemblyNameReference []> references;

		public AssemblyNameReference [] AssemblyReferences => references.Value;

		public void Dispose () => peReader.Dispose ();
	}
}
