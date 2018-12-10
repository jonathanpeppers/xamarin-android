using System;

namespace System.Reflection.Metadata.Cecil
{
	public class Resource
	{
		internal Resource (MetadataReader reader, ManifestResource resource)
		{
			Name = reader.GetString (resource.Name);
		}

		public string Name {
			get;
			private set;
		}
	}
}
