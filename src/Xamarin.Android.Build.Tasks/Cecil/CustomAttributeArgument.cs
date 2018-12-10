using System;

namespace System.Reflection.Metadata.Cecil
{
	public class CustomAttributeArgument
	{
		internal CustomAttributeArgument (object value)
		{
			Value = value;
		}

		public object Value {
			get;
			private set;
		}
	}
}
