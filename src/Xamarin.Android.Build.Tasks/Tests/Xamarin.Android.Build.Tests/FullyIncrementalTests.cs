using System.IO;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// A set of tests for $(_AndroidFullyIncremental)
	/// </summary>
	[TestFixture]
	public class FullyIncrementalTests : BaseTest
	{
		XamarinAndroidApplicationProject CreateApplicationProject ()
		{
			var proj = new XamarinAndroidApplicationProject {
				FullyIncremental = true,
				PackageReferences = {
					KnownPackages.SupportV7AppCompat_27_0_2_1,
				},
			};
			proj.MainActivity = proj.DefaultMainActivity.Replace (
				"public class MainActivity : Activity",
				"public class MainActivity : Android.Support.V7.App.AppCompatActivity"
			);
			return proj;
		}

		[Test]
		public void AppTouchJLO ()
		{
			var proj = CreateApplicationProject ();
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded");

				// Touch a JLO subclass
				proj.Touch ("MainActivity.cs");
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build should have succeeded");

				var partialTargets = new string [] {
					"_GenerateAndroidManifests",
					"_GenerateJavaStubs",
				};
				foreach (var target in partialTargets) {
					Assert.IsTrue (b.Output.IsTargetPartiallyBuilt (target), $"`{target}` should partially build!");
				}

				var skippedTargets = new string [] {
					"_CompileJavaStubs",
					"_CompileJava",
					"_CompileToDalvikWithD8",
				};
				foreach (var target in skippedTargets) {
					Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped!");
				}
			}
		}

		[Test]
		public void AppNewJLO ()
		{
			var proj = CreateApplicationProject ();
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "first build should have succeeded");

				// Create a new JLO subclass
				proj.Sources.Add (new BuildItem.Source ("Foo.cs") {
					TextContent = () => "class Foo : Java.Lang.Object { }"
				});
				Assert.IsTrue (b.Build (proj, doNotCleanupOnUpdate: true), "second build should have succeeded");

				var partialTargets = new string [] {
					"_GenerateAndroidManifests",
					"_GenerateJavaStubs",
					"_CompileJavaStubs",
				};
				foreach (var target in partialTargets) {
					Assert.IsTrue (b.Output.IsTargetPartiallyBuilt (target), $"`{target}` should partially build!");
				}

				var skippedTargets = new string [] {
					"_CompileJava",
				};
				foreach (var target in skippedTargets) {
					Assert.IsTrue (b.Output.IsTargetSkipped (target), $"`{target}` should be skipped!");
				}

				var targets = new string [] {
					"_CompileToDalvikWithD8",
				};
				foreach (var target in targets) {
					Assert.IsFalse (b.Output.IsTargetSkipped (target), $"`{target}` should *not* be skipped!");
				}
			}
		}

		// This case tests the classpath/dependencies that `javac` will encounter
		[Test]
		public void XamarinFormsApp ()
		{
			var proj = new XamarinFormsAndroidApplicationProject {
				FullyIncremental = true,
			};
			using (var b = CreateApkBuilder ()) {
				Assert.IsTrue (b.Build (proj), "build should have succeeded");
			}
		}
	}
}
