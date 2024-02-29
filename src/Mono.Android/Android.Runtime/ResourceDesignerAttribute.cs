using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace Android.Runtime
{
	[AttributeUsage (AttributeTargets.Assembly)]
	public class ResourceDesignerAttribute : Attribute
	{
		public ResourceDesignerAttribute (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
				Type resourceDesignerType)
		{
			ResourceDesignerType = resourceDesignerType;
		}

		public ResourceDesignerAttribute (
				[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
				string fullName)
		{
			FullName = fullName;

			// NOTE: Android class libraries older than .NET 9 may use this API
			// Suppress warnings in source, so they are passed along to users
			#pragma warning disable IL2026, IL2072
			ResourceDesignerType = Assembly.GetCallingAssembly ().GetType (fullName, throwOnError: true);
			#pragma warning restore IL2026, IL2072
		}

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		public string FullName { get; set; }

		public bool IsApplication { get; set; }

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicMethods)]
		public Type ResourceDesignerType { get; private set; }
	}
}
