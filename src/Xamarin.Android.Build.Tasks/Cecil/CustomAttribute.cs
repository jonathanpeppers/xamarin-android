using System.Collections.Generic;

namespace System.Reflection.Metadata.Cecil
{
	public class CustomAttribute
	{
		static readonly CustomAttributeTypeProvider typeProvider = new CustomAttributeTypeProvider ();

		readonly Metadata.CustomAttribute attribute;

		internal CustomAttribute (MetadataReader reader, Metadata.CustomAttribute attribute)
		{
			this.attribute = attribute;

			var ctor = reader.GetMemberReference ((MemberReferenceHandle)attribute.Constructor);
			var attributeType = reader.GetTypeReference ((TypeReferenceHandle)ctor.Parent);
			AttributeType = new TypeReference (reader, attributeType);

			try {
				var value = attribute.DecodeValue (typeProvider);
				var list = new List<CustomAttributeArgument> (value.FixedArguments.Length);
				foreach (var arg in value.FixedArguments) {
					list.Add (new CustomAttributeArgument (arg.Value));
				}
				ConstructorArguments = list.ToArray ();
			} catch (BadImageFormatException) {
				//TODO: WHY????
			}
		}

		public TypeReference AttributeType {
			get;
			private set;
		}

		public CustomAttributeArgument [] ConstructorArguments {
			get;
			private set;
		}
	}
}
