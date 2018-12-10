using System;

namespace System.Reflection.Metadata.Cecil
{
	/// <summary>
	/// NOTE: mostly a dummy type, only GetPrimitiveType is called
	/// See https://github.com/dotnet/corefx/issues/31889
	/// </summary>
	class CustomAttributeTypeProvider : ICustomAttributeTypeProvider<string>
	{
		public string GetPrimitiveType (PrimitiveTypeCode typeCode)
		{
			return typeCode == PrimitiveTypeCode.String ? "string" : null;
		}

		public string GetSystemType ()
		{
			return null;
		}

		public string GetSZArrayType (string elementType)
		{
			return null;
		}

		public string GetTypeFromDefinition (MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
		{
			return null;
		}

		public string GetTypeFromReference (MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
		{
			return null;
		}

		public string GetTypeFromSerializedName (string name)
		{
			return null;
		}

		public PrimitiveTypeCode GetUnderlyingEnumType (string type)
		{
			return PrimitiveTypeCode.String;
		}

		public bool IsSystemType (string type)
		{
			return type == "string";
		}
	}
}
