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
			arguments = new Lazy<CustomAttributeArgument []> (LoadArguments);

			var ctor = reader.GetMemberReference ((MemberReferenceHandle)attribute.Constructor);
			var attributeType = reader.GetTypeReference ((TypeReferenceHandle)ctor.Parent);
			AttributeType = new TypeReference (reader, attributeType);
		}

		public TypeReference AttributeType {
			get;
			private set;
		}

		CustomAttributeArgument[] LoadArguments ()
		{
			var value = attribute.DecodeValue (typeProvider);
			var list = new List<CustomAttributeArgument> (value.FixedArguments.Length);
			foreach (var arg in value.FixedArguments) {
				list.Add (new CustomAttributeArgument (arg.Value));
			}
			return list.ToArray ();
		}

		readonly Lazy<CustomAttributeArgument []> arguments;

		public CustomAttributeArgument [] ConstructorArguments => arguments.Value;
	}
}
