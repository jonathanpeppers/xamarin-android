using System;

namespace System.Reflection.Metadata.Cecil
{
	public class AssemblyNameReference
	{
		public AssemblyNameReference (MetadataReader reader, AssemblyReference reference)
		{
			Name = reader.GetString (reference.Name);
		}

		public string Name {
			get;
			private set;
		}
	}
}
