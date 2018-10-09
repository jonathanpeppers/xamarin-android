using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task invokes r8 in order to:
	/// - Compile to dex format + code shrinking (replacement for proguard)
	/// </summary>
	public class R8 : D8
	{
		// used for proguard configuration settings
		[Required]
		public string AndroidSdkDirectory { get; set; }
		public string AcwMapFile { get; set; }
		public string ProguardGeneratedReferenceConfiguration { get; set; }
		public string ProguardGeneratedApplicationConfiguration { get; set; }
		public string ProguardCommonXamarinConfiguration { get; set; }
		public string ProguardConfigurationFiles { get; set; }

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();

			if (!string.IsNullOrEmpty (AcwMapFile)) {
				var acwLines = File.ReadAllLines (AcwMapFile);
				using (var appcfg = File.CreateText (ProguardGeneratedApplicationConfiguration)) {
					for (int i = 0; i + 2 < acwLines.Length; i += 3) {
						try {
							var line = acwLines [i + 2];
							var java = line.Substring (line.IndexOf (';') + 1);
							appcfg.WriteLine ("-keep class " + java + " { *; }");
						} catch {
							// skip invalid lines
						}
					}
				}
			}
			if (!string.IsNullOrWhiteSpace (ProguardCommonXamarinConfiguration))
				using (var xamcfg = File.Create (ProguardCommonXamarinConfiguration))
					GetType ().Assembly.GetManifestResourceStream ("proguard_xamarin.cfg").CopyTo (xamcfg);
			if (!string.IsNullOrEmpty (ProguardConfigurationFiles)) {
				var configs = ProguardConfigurationFiles
					.Replace ("{sdk.dir}", AndroidSdkDirectory + Path.DirectorySeparatorChar)
					.Replace ("{intermediate.common.xamarin}", ProguardCommonXamarinConfiguration)
					.Replace ("{intermediate.references}", ProguardGeneratedReferenceConfiguration)
					.Replace ("{intermediate.application}", ProguardGeneratedApplicationConfiguration)
					.Replace ("{project}", string.Empty) // current directory anyways.
					.Split (';')
					.Select (s => s.Trim ())
					.Where (s => !string.IsNullOrWhiteSpace (s));
				foreach (var file in configs) {
					if (File.Exists (file))
						cmd.AppendSwitchIfNotNull ("--pg-conf ", file);
					else
						Log.LogCodedWarning ("XA4304", file, 0, "Proguard configuration file '{0}' was not found.", file);
				}
			}

			return cmd;
		}
	}
	
}
