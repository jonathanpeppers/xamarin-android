using System;
using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class JavaDaemonTests : BaseTest
	{
		XamarinAndroidApplicationProject CreateProject (bool isRelease = false)
		{
			var proj = new XamarinAndroidApplicationProject {
				IsRelease = isRelease,
				AndroidUseJavaDaemon = true,
				ManifestMerger = "manifestmerger.jar",
			};
			if (isRelease) {
				var abis = new string [] { "armeabi-v7a", "x86" };
				proj.SetProperty (KnownProperties.AndroidSupportedAbis, string.Join (";", abis));
			}
			return proj;
		}

		void InstallOrBuild (XamarinAndroidApplicationProject proj)
		{
			using (var builder = CreateApkBuilder ()) {
				if (HasDevices) {
					try {
						Assert.IsTrue (builder.Install (proj), "Install should have succeeded.");
					} finally {
						try {
							string result = RunAdbCommand ($"uninstall {proj.PackageName}");
							TestContext.WriteLine ($"adb uninstall: {result}");
						} catch (Exception exc) {
							TestContext.WriteLine (exc);
						}
					}
				} else {
					Assert.IsTrue (builder.Build (proj), "Build should have succeeded.");
				}
				Assert.IsTrue (StringAssertEx.ContainsText (builder.LastBuildOutput, "java daemon already running"), "Java daemon should be reused!");
			}
		}

		[Test]
		public void BasicApp ([Values (false, true)] bool isRelease)
		{
			var proj = CreateProject (isRelease);
			InstallOrBuild (proj);
		}

		[Test]
		public void ProguardAndDX ()
		{
			var proj = CreateProject (isRelease: true);
			proj.DexTool = "dx";
			proj.LinkTool = "proguard";
			InstallOrBuild (proj);
		}

		[Test]
		public void MultiDexAndDX ()
		{
			var proj = CreateProject (isRelease: true);
			proj.DexTool = "dx";
			proj.SetProperty ("AndroidEnableMultiDex", "True");
			InstallOrBuild (proj);
		}

		[Test]
		public void R8 ()
		{
			var proj = CreateProject (isRelease: true);
			proj.LinkTool = "r8";
			InstallOrBuild (proj);
		}
	}
}
