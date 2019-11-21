using System;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tools.BootstrapTasks
{
	public class WatchLingeringProcesses : Task
	{
		[Required]
		public string[] ProcessNames { get; set; }

		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;

		public override bool Execute ()
		{
			foreach (var processName in ProcessNames) {
				var key = $"{nameof (WatchLingeringProcesses)}_{processName}";
				var existing = BuildEngine4.GetRegisteredTaskObject (key, Lifetime);
				if (existing == null) {
					var obj = new KillAtEndOfBuild { ProcessName = processName };
					BuildEngine4.RegisterTaskObject (key, obj, Lifetime, allowEarlyCollection: false);
				} else {
					Log.LogMessage (MessageImportance.Low, $"Already watching for {processName}");
				}
			}

			return true;
		}

		class KillAtEndOfBuild : IDisposable
		{
			public string ProcessName { get; set; }

			public void Dispose ()
			{
				foreach (var process in Process.GetProcessesByName (ProcessName)) {
					using (process) {
						process.Kill ();
					}
				}
			}
		}
	}
}
