#nullable enable
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// ValidateJavaVersion's job is to shell out to java and javac to detect their version
	/// </summary>
	public class ValidateJavaVersion : AndroidTask
	{
		public override string TaskPrefix => "VJV";

		public string? JavaSdkPath { get; set; }

		public string? JavaToolExe { get; set; }

		public string? JavacToolExe { get; set; }

		public string? TargetPlatformVersion { get; set; }

		[Required]
		public string LatestSupportedJavaVersion { get; set; } = "";

		[Required]
		public string MinimumSupportedJavaVersion { get; set; } = "";

		/// <summary>
		/// If true use the old method of calling `java -version` and `javac -version`
		/// $(_AndroidUseJavaExeVersion) is passed in from MSBuild targets and defaults to false.
		/// </summary>
		public bool UseJavaExeVersion { get; set; } = true;

		[Output]
		public string? MinimumRequiredJdkVersion { get; set; }

		[Output]
		public string? JdkVersion { get; set; }

		public override bool RunTask ()
		{
			if (!ValidateJava ())
				return false;

			Log.LogDebugMessage ($"{nameof (ValidateJavaVersion)} Outputs:");
			Log.LogDebugMessage ($"  {nameof (JdkVersion)}: {JdkVersion}");
			Log.LogDebugMessage ($"  {nameof (MinimumRequiredJdkVersion)}: {MinimumRequiredJdkVersion}");

			return !Log.HasLoggedErrors;
		}

		// `java -version` will produce values such as:
		//  java version "9.0.4"
		//  java version "1.8.0_77"
		static readonly Regex JavaVersionRegex = new Regex (@"version ""(?<version>[\d\.]+)(_\d+)?[^""]*""");

		// `javac -version` will produce values such as:
		//  javac 9.0.4
		//  javac 1.8.0_77
		internal static readonly Regex JavacVersionRegex = new Regex (@"javac (?<version>\d+\.[\d\.]+)(_\d+)?");

		bool ValidateJava ()
		{
			var java = JavaToolExe ?? (OS.IsWindows ? "java.exe" : "java");
			var javac = JavacToolExe ?? (OS.IsWindows ? "javac.exe" : "javac");

			return ValidateJava (java, JavaVersionRegex) &&
				(!UseJavaExeVersion || ValidateJava (javac, JavacVersionRegex));
		}

		protected virtual bool ValidateJava (string javaExe, Regex versionRegex)
		{
			var required = Version.Parse (MinimumSupportedJavaVersion);
			if (Version.TryParse (TargetPlatformVersion, out var targetPlatformVersion) && targetPlatformVersion.Major >= 31) {
				required = new Version (11, 0);
			}
			MinimumRequiredJdkVersion = required.ToString ();

			try {
				var versionNumber = GetVersionFromTool (javaExe, versionRegex);
				if (versionNumber != null) {
					Log.LogMessage (MessageImportance.Normal, $"Found Java SDK version {versionNumber}.");
					if (versionNumber < required) {
						Log.LogCodedError ("XA0031", Properties.Resources.XA0031_NET, required);
					}
					var latest = Version.Parse (LatestSupportedJavaVersion);
					if (versionNumber > latest) {
						Log.LogCodedError ("XA0030", Properties.Resources.XA0030, versionNumber, latest.ToString (fieldCount: 2));
					}
				}
			} catch (Exception ex) {
				Log.LogWarningFromException (ex);
				Log.LogCodedWarning ("XA0034", Properties.Resources.XA0034, required);
				return false;
			}

			return !Log.HasLoggedErrors;
		}

		protected Version? GetVersionFromTool (string javaExe, Regex versionRegex)
		{
			// New code path, reads directly from `release` text file
			if (!UseJavaExeVersion && GetVersionFromFile (javaExe, out var version)) {
				JdkVersion = version.ToString ();
				return version;
			}

			// NOTE: this doesn't need to use GetRegisteredTaskObjectAssemblyLocal()
			// because the path to java/javac is the key and the value is a System.Version.
			var javaTool = Path.Combine (JavaSdkPath, "bin", javaExe);
			var key = new Tuple<string, string> (nameof (ValidateJavaVersion), javaTool);
			var cached = BuildEngine4.GetRegisteredTaskObject (key, RegisteredTaskObjectLifetime.AppDomain) as Version;
			if (cached != null) {
				Log.LogDebugMessage ($"Using cached value for `{javaTool} -version`: {cached}");
				JdkVersion = cached.ToString ();
				return cached;
			}

			var sb = new StringBuilder ();
			MonoAndroidHelper.RunProcess (javaTool, "-version", (s, e) => {
				if (!e.Data.IsNullOrEmpty ())
					sb.AppendLine (e.Data);
			}, (s, e) => {
				if (!e.Data.IsNullOrEmpty ())
					sb.AppendLine (e.Data);
			});
			var versionInfo = sb.ToString ();
			var versionNumberMatch = versionRegex.Match (versionInfo);
			Version? versionNumber;
			if (versionNumberMatch.Success && Version.TryParse (versionNumberMatch.Groups ["version"]?.Value, out versionNumber)) {
				BuildEngine4.RegisterTaskObject (key, versionNumber, RegisteredTaskObjectLifetime.AppDomain, allowEarlyCollection: false);
				JdkVersion = versionNumberMatch.Groups ["version"].Value;
				return versionNumber;
			} else {
				Log.LogCodedWarning ("XA0033", Properties.Resources.XA0033, javaExe, versionInfo);
				return null;
			}
		}

		bool GetVersionFromFile (string javaExe, out Version version)
		{
			var path = Path.Combine (Path.GetDirectoryName (javaExe), "..", "release");
			if (!File.Exists (path)) {
				path = Path.Combine (JavaSdkPath, "release");
			}
			if (!File.Exists (path)) {
				Log.LogDebugMessage ($"{path} does not exist, falling back to `java -version`");
				version = new ();
				return false;
			}

			using var stream = File.OpenRead (path);
			using var reader = new StreamReader (stream);

			string? line;
			while ((line = reader.ReadLine ()) != null) {
				const string JAVA_VERSION = "JAVA_VERSION=\"";
				int index = line.IndexOf (JAVA_VERSION, StringComparison.OrdinalIgnoreCase);
				if (index != -1) {
					var versionString = line.Substring (index + JAVA_VERSION.Length);
					index = versionString.IndexOf ('"');
					if (index != -1) {
						versionString = versionString.Substring (0, index);
					}
					if (Version.TryParse (versionString, out version)) {
						Log.LogDebugMessage ($"{path} contains JAVA_VERSION.");
						return true;
					}
				}
			}

			Log.LogDebugMessage ($"{path} did not contain a valid JAVA_VERSION.");
			version = new ();
			return false;
		}
	}
}
