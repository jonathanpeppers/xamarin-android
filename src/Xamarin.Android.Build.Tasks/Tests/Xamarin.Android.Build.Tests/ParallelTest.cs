using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// A set of tests that verify specific Targets/Tasks can run in parallel
	/// </summary>
	[TestFixture]
	public class ParallelTest : BaseTest
	{
		Project project;
		ProjectBuilder restoreBuilder;

		[SetUp]
		public void Setup ()
		{
			project = new Project ();
			restoreBuilder = CreateApkBuilder ();
			restoreBuilder.AutomaticNuGetRestore = false;
			restoreBuilder.Save (project);
			Assert.IsTrue (restoreBuilder.Restore (project), "Restore should succeed");
		}

		[TearDown]
		public void TearDown ()
		{
			restoreBuilder?.Dispose ();
		}

		[Test]
		[Repeat (5)]
		public void ParallelDesignTimeBuild ()
		{
			var parameters = new [] { "SkipCompilerExecution=True", "ProvideCommandLineArgs=True" };

			Parallel.For (0, 10, i => {
				using (var builder = CreateApkBuilder (cleanupOnDispose: false)) {
					builder.AutomaticNuGetRestore = false;
					builder.BuildLogFile = null;
					builder.BinLogFile = $"msbuild{i}.binlog";
					Assert.IsTrue (builder.DesignTimeBuild (project, "CompileDesignTime", doNotCleanupOnUpdate: true, parameters: parameters),
						$"DesignTimeBuild {i} failed");
				}
			});
		}

		class Project : XASdkProject
		{
			XamarinFormsAndroidApplicationProject project;

			public Project () : base ("0.0.1")
			{
				// Create a Xamarin.Forms project and copy the item groups over
				project = new XamarinFormsAndroidApplicationProject ();
				foreach (var package in project.PackageReferences) {
					PackageReferences.Add (package);
				}
				foreach (var group in project.ItemGroupList) {
					foreach (var item in group) {
						if (GetItem (item.Include ()) == null) {
							if (item.BuildAction == "Compile" ||
								item.BuildAction == "EmbeddedResource" ||
								item.BuildAction == "AndroidResource") {
								Sources.Add (item);
							} else {
								OtherBuildItems.Add (item);
							}
						}
					}
				}
			}

			bool shouldPopulate = true;

			public override bool ShouldPopulate => shouldPopulate;

			public override void UpdateProjectFiles (string directory, IEnumerable<ProjectResource> projectFiles, bool doNotCleanup = false)
			{
				base.UpdateProjectFiles (directory, projectFiles, doNotCleanup);

				shouldPopulate = false;
			}
		}
	}
}
