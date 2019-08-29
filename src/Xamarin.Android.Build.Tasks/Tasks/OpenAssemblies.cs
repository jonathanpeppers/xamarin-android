using System;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Creates a DirectoryAssemblyResolver instance with a full list of SearchDirectories
	/// </summary>
	public class OpenAssemblies : DirectoryAssemblyResolverTask
	{
		public override string TaskPrefix => "OPEN";

		public string [] SearchDirectories { get; set; }

		public override bool RunTask ()
		{
			var resolver = NewResolver ();
			foreach (var dir in SearchDirectories) {
				resolver.SearchDirectories.Add (dir);
			}
			return !Log.HasLoggedErrors;
		}
	}
}
