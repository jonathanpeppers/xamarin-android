// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using System;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Collections.Generic;
using Xamarin.Android.Tools;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{

	public abstract class JavaCompileToolTask : JavaToolTask
	{
		[Required]
		public string TargetFrameworkDirectory { get; set; }

		public ITaskItem [] JavaSourceFiles { get; set; }

		public string JavaPlatformJarPath { get; set; }

		public string JavacTargetVersion { get; set; }

		public string JavacSourceVersion { get; set; }

		protected override string ToolName {
			get { return OS.IsWindows ? "javac.exe" : "javac"; }
		}

		public override string DefaultErrorCode => "JAVAC0000";

		internal string TemporarySourceListFile;

		/// <summary>
		/// Calls javac to compile to .class files
		/// </summary>
		protected virtual bool Compile (string stubSourceDirectory, string classesOutputDirectory, IEnumerable<string> classPath)
		{
			Directory.CreateDirectory (classesOutputDirectory);

			try {
				GenerateResponseFile (classesOutputDirectory, stubSourceDirectory, classPath);
				return base.RunTask ();
			} finally {
				try {
					if (!string.IsNullOrEmpty (TemporarySourceListFile))
						File.Delete (TemporarySourceListFile);
				} catch {
					// Ignore exception, a tiny temp file will get left on the user's system
				}
			}
		}

		/// <summary>
		/// Uses libZipSharp to create a zip/jar file
		/// </summary>
		protected virtual void Compress (string classesOutputDirectory, string classesZip)
		{
			if (!string.IsNullOrEmpty (classesZip)) {
				Directory.CreateDirectory (Path.GetDirectoryName (classesZip));
				using (var zip = new ZipArchiveEx (classesZip, FileMode.OpenOrCreate)) {
					zip.AddDirectory (classesOutputDirectory, "", CompressionMethod.Store);
				}
			}
		}

		protected override string GenerateCommandLineCommands ()
		{
			//   Running command: C:\Program Files (x86)\Java\jdk1.6.0_20\bin\javac.exe
			//     "-J-Dfile.encoding=UTF8"
			//     "-d" "bin\classes"
			//     "-classpath" "C:\Users\Jonathan\Documents\Visual Studio 2010\Projects\AndroidMSBuildTest\AndroidMSBuildTest\obj\Debug\android\bin\mono.android.jar"
			//     "-bootclasspath" "C:\Program Files (x86)\Android\android-sdk-windows\platforms\android-8\android.jar"
			//     "-encoding" "UTF-8"
			//     "@C:\Users\Jonathan\AppData\Local\Temp\tmp79c4ac38.tmp"

			var cmd = new CommandLineBuilder ();
			cmd.AppendSwitchIfNotNull ("-J-Dfile.encoding=", "UTF8");
			cmd.AppendFileNameIfNotNull (string.Format ("@{0}", TemporarySourceListFile));
			cmd.AppendSwitchIfNotNull ("-target ", JavacTargetVersion);
			cmd.AppendSwitchIfNotNull ("-source ", JavacSourceVersion);

			return cmd.ToString ();
		}

		void GenerateResponseFile (string classesOutputDirectory, string stubSourceDirectory, IEnumerable<string> classPath)
		{
			TemporarySourceListFile = Path.GetTempFileName ();

			using (var sw = new StreamWriter (path: TemporarySourceListFile, append: false,
						encoding: new UTF8Encoding (encoderShouldEmitUTF8Identifier: false))) {

				sw.WriteLine ($"-d \"{classesOutputDirectory.Replace (@"\", @"\\")}\"");
				if (classPath != null && classPath.Any ()) {
					sw.WriteLine ("-classpath \"{0}\"", string.Join (Path.PathSeparator.ToString (), classPath.Select (c => c.Replace (@"\", @"\\"))));
				}
				sw.WriteLine ("-bootclasspath \"{0}\"", JavaPlatformJarPath.Replace (@"\", @"\\"));
				sw.WriteLine ($"-encoding UTF8");

				// Include any user .java files
				if (JavaSourceFiles != null)
					foreach (var file in JavaSourceFiles.Where (p => Path.GetExtension (p.ItemSpec) == ".java"))
						sw.WriteLine (string.Format ("\"{0}\"", file.ItemSpec.Replace (@"\", @"\\")));

				foreach (var file in Directory.GetFiles (stubSourceDirectory, "*.java", SearchOption.AllDirectories)) {
					// This makes sense.  BAD sense.  but sense.
					// Problem:
					//    A perfectly sensible path like "E:\tmp\a.java" generates a
					//    javac error that "E:       mp.java" can't be found.
					// Cause:
					//    javac uses java.io.StreamTokenizer to parse @response files, and
					//    the docs for StreamTokenizer.quoteChar(int) [0] say:
					//      The usual escape sequences such as "\n" and "\t" are recognized
					//      and converted to single characters as the string is parsed.
					//    i.e. '\' is an escape character!
					// Solution:
					//    Since '\' is an escape character, we need to escape it.
					// [0] http://download.oracle.com/javase/1.4.2/docs/api/java/io/StreamTokenizer.html#quoteChar(int)
					sw.WriteLine (string.Format ("\"{0}\"",
								file.Replace (@"\", @"\\").Normalize (NormalizationForm.FormC)));
				}
			}
		}
	}
}
