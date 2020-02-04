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
		public void ParallelDesignTimeBuild ()
		{
			Parallel.For (0, 10, i => {
				using (var builder = CreateApkBuilder (cleanupOnDispose: false)) {
					builder.AutomaticNuGetRestore = false;
					builder.BuildLogFile = null;
					builder.BinLogFile = $"msbuild{i}.binlog";
					Assert.IsTrue (builder.DesignTimeBuild (project, doNotCleanupOnUpdate: true), $"DesignTimeBuild {i} failed");
				}
			});
		}

		class Project : XamarinAndroidApplicationProject
		{
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
