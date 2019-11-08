using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class MetadataResolverTests : BaseTest
	{
		[Test]
		public void BaseTypes ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "Build should have succeeded.");

				var assets = Path.Combine (Root, b.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets");
				using (var resolver = new MetadataResolver ()) {
					resolver.AddSearchDirectory (assets);

					var mainActivity = resolver.EnumerateTypes ("UnnamedProject").First (t => t.Name == "MainActivity");
					var baseTypes = resolver.EnumerateBaseTypes (mainActivity).Select (t => t.FullName).ToArray ();
					CollectionAssert.AreEqual (new [] {
						"Android.App.Activity",
						"Android.Views.ContextThemeWrapper",
						"Android.Content.ContextWrapper",
						"Android.Content.Context",
						"Java.Lang.Object",
						"System.Object",
					}, baseTypes);
				}
			}
		}
	}
}
