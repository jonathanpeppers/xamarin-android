using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Xamarin.Android.Tasks
{
	public class JavaDaemonClient : IDisposable
	{
		const int Timeout = 3000;

		readonly JsonSerializerSettings settings = new JsonSerializerSettings {
			NullValueHandling = NullValueHandling.Ignore,
		};
		Process process;

		public bool IsConnected => process != null && !process.HasExited;

		public int? ProcessId => process?.Id;

		/// <summary>
		/// A callback for logging purposes
		/// </summary>
		public Action<string> Log { get; set; }

		/// <summary>
		/// Starts the java daemon process
		/// </summary>
		/// <param name="fileName">Full path to java/java.exe</param>
		/// <param name="arguments">Command-line arguments for java</param>
		public void Connect (string fileName, string arguments)
		{
			if (IsConnected)
				return;

			Log?.Invoke ($"Starting java: {fileName} {arguments}");

			var info = new ProcessStartInfo (fileName, arguments) {
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
			};
			process = Process.Start (info);
		}

		/// <summary>
		/// Using the connected Java daemon, invokes the Main method on a specific Java class in a jar file
		/// </summary>
		/// <param name="className">Java class name, including package name</param>
		/// <param name="jar">Full path to the jar file on disk</param>
		/// <param name="arguments">additional arguments to the command</param>
		/// <returns></returns>
		public (int exitCode, string stdout, string stderr) Invoke (string className, string jar, string arguments)
		{
			if (!IsConnected)
				throw new InvalidOperationException ("Not connected to Java daemon");

			Write (new {
				className,
				jar,
				arguments,
			});

			var jsonOutput = Read ();
			return (jsonOutput.exitCode, jsonOutput.stdout, jsonOutput.stderr);
		}

		void Write (object value)
		{
			string json = JsonConvert.SerializeObject (value, settings);
			Log?.Invoke ("Send: " + json);
			process.StandardInput.WriteLine (json);
		}

		Response Read ()
		{
			string line = process.StandardOutput.ReadLine ();
			Log?.Invoke ("Receive: " + line);
			return JsonConvert.DeserializeObject<Response> (line, settings);
		}

		/// <summary>
		/// A quick class for parsing the response
		/// </summary>
		class Response
		{
			public int exitCode { get; set; }

			public string stdout { get; set; }

			public string stderr { get; set; }
		}

		public void Dispose ()
		{
			if (process == null)
				return;

			if (process.HasExited) {
				process.Dispose ();
				process = null;
				return;
			}

			Write (new { exit = true });

			if (!process.WaitForExit (Timeout)) {
				process.Kill ();
			}
			process.Dispose ();
			process = null;
		}
	}
}
