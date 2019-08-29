using System;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Closes an opened DirectoryAssemblyResolver
	/// </summary>
	public class CloseAssemblies : DirectoryAssemblyResolverTask
	{
		public override string TaskPrefix => "CLOS";

		public override bool RunTask ()
		{
			UnregisterAll ();
			return !Log.HasLoggedErrors;
		}
	}
}
