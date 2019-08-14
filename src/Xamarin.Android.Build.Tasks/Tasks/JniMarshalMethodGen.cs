using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Invokes jnimarshalmethod-gen.exe
	/// </summary>
	public class JniMarshalMethodGen : ToolTask
	{
		protected override string ToolName => "jnimarshalmethod-gen";

		public override string ToolExe {
			get { return OS.IsWindows ? "mono-sgen.exe" : "mono"; }
			set { base.ToolExe = value; }
		}

		[Required]
		public string MonoAndroidLibDirectory { get; set; }

		[Required]
		public string MonoAndroidBinDirectory { get; set; }

		[Required]
		public string MonoAndroidToolsDirectory { get; set; }

		[Required]
		public string [] TargetFrameworkDirectories { get; set; }

		[Required]
		public string [] ResolvedAssemblies { get; set; }

		[Required]
		public string [] Assemblies { get; set; }

		[Required]
		public string JdkJvmPath { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		public string ExtraArguments { get; set; }

		public override bool Execute ()
		{
			var mono_path = new List<string> {
				Path.Combine (MonoAndroidBinDirectory, "bcl"),
				Path.Combine (MonoAndroidBinDirectory, "bcl", "Facades"),
			};
			mono_path.AddRange (TargetFrameworkDirectories);

			foreach (var assembly in Assemblies) {
				var dir = Path.GetDirectoryName (assembly);
				if (!mono_path.Contains (dir)) {
					mono_path.Add (dir);
				}
			}

			EnvironmentVariables = new [] {
				"DYLD_LIBRARY_PATH=" + MonoAndroidLibDirectory,
				"MONO_CONFIG=" + Path.Combine (MonoAndroidBinDirectory, "mono.config"),
				"MONO_PATH=" + string.Join (Path.PathSeparator.ToString (), mono_path),
			};

			return base.Execute ();
		}

		protected override string GenerateFullPathToTool () => Path.Combine (MonoAndroidBinDirectory, ToolExe);

		protected override string GenerateCommandLineCommands ()
		{
			var cmd = new CommandLineBuilder ();

			cmd.AppendSwitch ("--debug");
			cmd.AppendFileNameIfNotNull (Path.Combine (MonoAndroidToolsDirectory, ToolName + ".exe"));
			cmd.AppendSwitchIfNotNull ("--jvm ", JdkJvmPath);
			cmd.AppendSwitchIfNotNull ("--o ", OutputDirectory);

			foreach (var assembly in ResolvedAssemblies) {
				cmd.AppendSwitchIfNotNull ("--r ", assembly);
			}

			if (!string.IsNullOrEmpty (ExtraArguments))
				cmd.AppendSwitch (ExtraArguments);

			foreach (var assembly in Assemblies) {
				cmd.AppendFileNameIfNotNull (assembly);
			}

			return cmd.ToString ();
		}
	}
}
