using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Android.Runtime
{
	static class ReflectionExtensions
	{
		/// <summary>
		/// A more performant equivalent of $"{type.FullName}, {type.Assembly.GetName().Name}"
		/// </summary>
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static string GetTypeAndAssemblyName (this Type type) =>
			type.FullName + ", " + type.Assembly.GetAssemblyName ();

		/// <summary>
		/// A more performant equivalent of `Assembly.GetName().Name`
		/// </summary>
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static string GetAssemblyName (this Assembly assembly)
		{
			var name = assembly.FullName;
			int index = name.IndexOf (',');
			if (index != -1) {
				return name.Substring (0, index);
			}
			return name;
		}
	}
}
