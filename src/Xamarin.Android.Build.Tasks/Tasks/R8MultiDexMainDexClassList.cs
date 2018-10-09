using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// This task invokes r8 in order to:
	/// - Create a multidex.keep file (replacement for CreateMultiDexMainDexClassList/proguard)
	/// </summary>
	public class R8MultiDexMainDexClassList : D8
	{
		[Required]
		public string OutputMainDexListFile { get; set; }
		[Required]
		public string AndroidSdkBuildToolsPath { get; set; }
		public ITaskItem [] CustomMainDexListFiles { get; set; }

		public R8MultiDexMainDexClassList ()
		{
			//Should be enabled
			EnableMultiDex = true;
		}

		public override bool Execute ()
		{
			try {
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
			cmd.AppendSwitchIfNotNull ("--main-dex-rules ", Path.Combine (AndroidSdkBuildToolsPath, "mainDexClasses.rules"));
			return cmd;
		}
	}
}
