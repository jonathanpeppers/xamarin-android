#nullable enable
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tools;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Since GenerateJavaStubs can get skipped on incremental builds, this task parses the merged AndroidManifest.xml for values needed later in the build.
	/// </summary>
	public class ReadAndroidManifest : AndroidTask
	{
		public override string TaskPrefix => "RAM";

		[Required]
		public string ManifestFile { get; set; } = "";

		[Required]
		public string AndroidSdkDirectory { get; set; } = "";

		[Required]
		public string AndroidApiLevel { get; set; } = "";

		/// <summary>
		/// True if //manifest/application/[@android:extractNativeLibs='false']. False otherwise.
		/// </summary>
		[Output]
		public bool EmbeddedDSOsEnabled { get; set; }

		[Output]
		public ITaskItem []? UsesLibraries { get; set; }

		[Output]
		public bool UseEmbeddedDex { get; set; } = false;

		[Output]
		public bool IsTestOnly { get; set; } = false;

		public override bool RunTask ()
		{
			var androidNs = AndroidAppManifest.AndroidXNamespace;
			var manifest = AndroidAppManifest.Load (ManifestFile, MonoAndroidHelper.SupportedVersions);
			var app = manifest.Document.Element ("manifest")?.Element ("application");

			if (app != null) {
				string? text = app.Attribute (androidNs + "extractNativeLibs")?.Value;
				if (bool.TryParse (text, out bool value)) {
					EmbeddedDSOsEnabled = !value;
				}

				text = app.Attribute (androidNs + "useEmbeddedDex")?.Value;
				if (bool.TryParse (text, out value)) {
					UseEmbeddedDex = value;
				}

				text = app.Attribute (androidNs + "testOnly")?.Value;
				if (bool.TryParse (text, out value)) {
					IsTestOnly = value;
				}

				var libraries = new List<ITaskItem> ();
				foreach (var uses_library in app.Elements ("uses-library")) {
					var attribute = uses_library.Attribute (androidNs + "name");
					if (attribute != null && !attribute.Value.IsNullOrEmpty ()) {
						var required = uses_library.Attribute (androidNs + "required")?.Value;
						var path = Path.Combine (AndroidSdkDirectory, "platforms", $"android-{AndroidApiLevel}", "optional", $"{attribute.Value}.jar");
						if (File.Exists (path)) {
							libraries.Add (new TaskItem (path));
						} else {
							if (!bool.TryParse (required, out bool isRequired))
								isRequired = true;
							if (isRequired)
								Log.LogWarningForXmlNode ("XA4218", ManifestFile, attribute, Properties.Resources.XA4218, path);
						}
					}
				}
				UsesLibraries = libraries.ToArray ();
			}

			return !Log.HasLoggedErrors;
		}
	}
}
