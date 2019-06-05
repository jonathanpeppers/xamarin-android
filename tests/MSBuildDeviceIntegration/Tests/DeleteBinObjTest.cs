using NUnit.Framework;
using System.IO;
using Xamarin.ProjectTools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class DeleteBinObjTest : DeviceTest
	{
		const string BaseUrl = "https://xamjenkinsartifact.azureedge.net/mono-jenkins/xamarin-android-test/";
		readonly DownloadedCache Cache = new DownloadedCache ();

		string HostOS => IsWindows ? "Windows" : "Darwin";

		void RunTest (string name, string csproj, string version, bool isRelease)
		{
			var configuration = isRelease ? "Release" : "Debug";
			var zipPath = Cache.GetAsFile ($"{BaseUrl}{name}-{version}-{configuration}-{HostOS}.zip");
			using (var builder = CreateApkBuilder (Path.Combine ("temp", TestName)))
			using (var zip = ZipArchive.Open (zipPath, FileMode.Open)) {
				var projectDir = Path.Combine (Root, builder.ProjectDirectory);
				if (Directory.Exists (projectDir))
					Directory.Delete (projectDir, recursive: true);
				zip.ExtractAll (projectDir);

				var project = new ExistingProject {
					ProjectFilePath = Path.Combine (projectDir, csproj),
				};
				
				if (HasDevices) {
					Assert.IsTrue (builder.Install (project, doNotCleanupOnUpdate: true, saveProject: false), "Install should have succeeded.");
				} else {
					Assert.IsTrue (builder.Build (project, doNotCleanupOnUpdate: true, saveProject: false), "Build should have succeeded.");
				}
			}
		}

		[Test]
		public void HelloForms ([Values (false, true)] bool isRelease)
		{
			RunTest (nameof (HelloForms), Path.Combine ("HelloForms.Android", "HelloForms.Android.csproj"), "15.9", isRelease);
		}
	}
}
