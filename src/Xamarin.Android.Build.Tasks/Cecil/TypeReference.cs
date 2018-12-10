using System;

namespace System.Reflection.Metadata.Cecil
{
	public class TypeReference
	{
		internal TypeReference (MetadataReader reader, Metadata.TypeReference reference)
		{
			Name = reader.GetString (reference.Name);
			Namespace = reader.GetString (reference.Namespace);
		}

		public string Name {
			get;
			private set;
		}

		public string Namespace {
			get;
			private set;
		}

		public string FullName => Namespace + "." + Name;
	}
}
