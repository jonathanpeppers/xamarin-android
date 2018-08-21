using System;
using System.Collections.Generic;
using NUnit.Framework;
using Xamarin.ProjectTools;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using System.Text;
using Xamarin.Android.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Build.Tests {

	[TestFixture]
	[Parallelizable (ParallelScope.Self)]
	public class ConvertResourcesCasesTests  : BaseTest {
		[Test]
		public void CheckClassIsReplacedWithMd5 ()
		{
			var path = Path.Combine (Root, "temp", "CheckClassIsReplacedWithMd5");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?>
<LinearLayout xmlns:android='http://schemas.android.com/apk/res/android'>
<ClassLibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
<classlibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
</LinearLayout>
");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new ConvertResourcesCases {
				BuildEngine = engine
			};
			task.ResourceDirectories = new ITaskItem [] {
				new TaskItem (resPath),
			};
			task.AcwMapFile = Path.Combine (path, "acwmap.txt");
			File.WriteAllLines (task.AcwMapFile, new string [] {
				"ClassLibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
				"classlibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
			});
			Assert.IsTrue (task.Execute (), "Task should have executed successfully");
			var output = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.Contains ("md5d6f7135293df7527c983d45d07471c5e.CustomTextView", output, "md5d6f7135293df7527c983d45d07471c5e.CustomTextView should exist in the main.xml");
			StringAssert.DoesNotContain ("ClassLibrary1.CustomView", output, "ClassLibrary1.CustomView should have been replaced.");
			StringAssert.DoesNotContain ("classlibrary1.CustomView", output, "classlibrary1.CustomView should have been replaced.");
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void CheckClassIsNotReplacedWithMd5 ()
		{
			var path = Path.Combine (Root, "temp", "CheckClassIsNotReplacedWithMd5");
			Directory.CreateDirectory (path);
			var resPath = Path.Combine (path, "res");
			Directory.CreateDirectory (Path.Combine (resPath, "layout"));
			File.WriteAllText (Path.Combine (resPath, "layout", "main.xml"), @"<?xml version='1.0' ?>
<LinearLayout xmlns:android='http://schemas.android.com/apk/res/android'>
<ClassLibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
<classLibrary1.CustomView xmlns:android='http://schemas.android.com/apk/res/android' />
</LinearLayout>
");
			var errors = new List<BuildErrorEventArgs> ();
			IBuildEngine engine = new MockBuildEngine (TestContext.Out, errors);
			var task = new ConvertResourcesCases {
				BuildEngine = engine
			};
			task.ResourceDirectories = new ITaskItem [] {
				new TaskItem (resPath),
			};
			task.AcwMapFile = Path.Combine (path, "acwmap.txt");
			File.WriteAllLines (task.AcwMapFile, new string [] {
				"ClassLibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
				"classlibrary1.CustomView;md5d6f7135293df7527c983d45d07471c5e.CustomTextView",
			});
			Assert.IsTrue (task.Execute (), "Task should have executed successfully");
			var output = File.ReadAllText (Path.Combine (resPath, "layout", "main.xml"));
			StringAssert.Contains ("md5d6f7135293df7527c983d45d07471c5e.CustomTextView", output, "md5d6f7135293df7527c983d45d07471c5e.CustomTextView should exist in the main.xml");
			StringAssert.DoesNotContain ("ClassLibrary1.CustomView", output, "ClassLibrary1.CustomView should have been replaced.");
			StringAssert.Contains ("classLibrary1.CustomView", output, "classLibrary1.CustomView should have been replaced.");
			Assert.AreEqual (1, errors.Count, "One Error should have been raised.");
			Assert.AreEqual ("XA1002", errors [0].Code, "XA1002 should have been raised.");
			Directory.Delete (path, recursive: true);
		}

		[Test]
		public void PerformanceTiming ()
		{
			var proj = new XamarinAndroidApplicationProject ();
			proj.MainActivity = proj.DefaultMainActivity.Replace ("public class MainActivity : Activity", "public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity");

			var packages = proj.Packages;
			packages.Add (KnownPackages.XamarinForms_3_0_0_561731);
			packages.Add (KnownPackages.Android_Arch_Core_Common_26_1_0);
			packages.Add (KnownPackages.Android_Arch_Lifecycle_Common_26_1_0);
			packages.Add (KnownPackages.Android_Arch_Lifecycle_Runtime_26_1_0);
			packages.Add (KnownPackages.AndroidSupportV4_27_0_2_1);
			packages.Add (KnownPackages.SupportCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportCoreUI_27_0_2_1);
			packages.Add (KnownPackages.SupportCoreUtils_27_0_2_1);
			packages.Add (KnownPackages.SupportDesign_27_0_2_1);
			packages.Add (KnownPackages.SupportFragment_27_0_2_1);
			packages.Add (KnownPackages.SupportMediaCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportV7AppCompat_27_0_2_1);
			packages.Add (KnownPackages.SupportV7CardView_27_0_2_1);
			packages.Add (KnownPackages.SupportV7MediaRouter_27_0_2_1);
			packages.Add (KnownPackages.SupportV7RecyclerView_27_0_2_1);

			using (var b = CreateApkBuilder (Path.Combine ("temp", TestName))) {
				Assert.IsTrue(b.Build (proj), "first build should have succeeded.");

				//Build log of the form

				//Task Performance Summary:
				//     4559 ms  ConvertResourcesCases                      2 calls

				int? totalMilliseconds = null;
				bool perfSummary = false;
				var regex = new Regex (@"\s*(\d+)\s?ms", RegexOptions.Compiled | RegexOptions.IgnoreCase);
				foreach (var line in b.LastBuildOutput) {
					if (perfSummary) {
						if (line.Contains (nameof (ConvertResourcesCases))) {
							var match = regex.Match (line);
							if (match.Success && int.TryParse (match.Groups[1].Value, out int ms)) {
								totalMilliseconds = ms;
								break;
							}
						}
					} else {
						perfSummary = line.IndexOf ("Task Performance Summary:", StringComparison.InvariantCultureIgnoreCase) >= 0;
					}
				}

				if (totalMilliseconds == null) {
					Assert.Fail ($"Did not find `{nameof (ConvertResourcesCases)}` in performance summary!");
				}

				const int limit = 10000;
				if (totalMilliseconds > limit) {
					Assert.LessOrEqual (totalMilliseconds.Value, limit, $"`{nameof (ConvertResourcesCases)}` should not be slower than {limit}ms!");
				}
				TestContext.WriteLine ($"{nameof (ConvertResourcesCases)} took {totalMilliseconds}ms");
			}
		}
	}
}
