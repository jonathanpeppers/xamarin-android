using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Invokes `bundletool` to install an APK set to an attached device
	/// 
	/// Usage: bundletool install-apks --apks=foo.apks
	/// </summary>
	public class BundleToolInstallApkSet : BundleTool
	{
		[Required]
		public string ApkSet { get; set; }

		[Required]
		public string AdbToolPath { get; set; }

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var adb = OS.IsWindows ? "adb.exe" : "adb";
			var cmd = base.GetCommandLineBuilder ();
			cmd.AppendSwitch ("install-apks");
			cmd.AppendSwitchIfNotNull ("--apks ", ApkSet);
			cmd.AppendSwitchIfNotNull ("--adb ", Path.Combine (AdbToolPath, adb));
			cmd.AppendSwitchIfNotNull ("--modules ", "_ALL_");
			cmd.AppendSwitch ("--allow-downgrade");
			return cmd;
		}
	}
}
