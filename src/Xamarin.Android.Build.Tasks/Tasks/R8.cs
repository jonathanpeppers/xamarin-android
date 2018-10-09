using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task invokes r8, which has multiple functions:
	/// - Compile to dex format + code shrinking (replacement for proguard)
	/// - Create a multidex.keep file (replacement for CreateMultiDexMainDexClassList/proguard)
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
		public string ProguardMappingOutput { get; set; }
		/// <summary>
		/// When non-empty, indicates we are invoking r8 to generate a multidex.keep file
		/// </summary>
		public string OutputMainDexListFile { get; set; }
		public ITaskItem [] CustomMainDexListFiles { get; set; }

		public override bool Execute ()
		{
			try {
				if (!string.IsNullOrEmpty (OutputMainDexListFile)) {
					if (string.IsNullOrEmpty (MultiDexMainDexListFile)) {
						Log.LogCodedError ("XA4305", $"MultiDex is enabled, but '{nameof (MultiDexMainDexListFile)}' was not specified.");
						return false;
					}

					var content = new List<string> ();
					if (CustomMainDexListFiles != null) {
						foreach (var file in CustomMainDexListFiles) {
							if (File.Exists (file.ItemSpec)) {
								content.Add (File.ReadAllText (file.ItemSpec));
							} else {
								Log.LogCodedError ("XA4305", file.ItemSpec, 0, $"'MultiDexMainDexList' file '{file.ItemSpec}' does not exist.");
							}
						}
					}
					if (Log.HasLoggedErrors)
						return false;
					content.Add (Environment.NewLine);
					File.WriteAllText (MultiDexMainDexListFile, string.Concat (content));
				}

				return base.Execute ();
			} finally {
				//NOTE: in the case of using OutputMainDexListFile, MultiDexMainDexListFile is used as a temporary input file
				if (!string.IsNullOrEmpty (OutputMainDexListFile)) {
					File.Delete (MultiDexMainDexListFile);
				}
			}
		}

		protected override CommandLineBuilder GetCommandLineBuilder ()
		{
			var cmd = base.GetCommandLineBuilder ();

			cmd.AppendSwitchIfNotNull ("--main-dex-list-output ", OutputMainDexListFile);

			// generating proguard application configuration
			if (string.IsNullOrEmpty (OutputMainDexListFile)) {
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
				cmd.AppendSwitchIfNotNull ("--pg-map-output ", ProguardMappingOutput);
			}

			return cmd;
		}
	}
	
}
