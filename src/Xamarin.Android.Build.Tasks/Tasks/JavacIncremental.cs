using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// TODO: should use AsyncTask somehow and run in parallel
	/// </summary>
	public class JavacIncremental : JavaCompileToolTask
	{
		public override string TaskPrefix => "JVCI";

		[Required]
		public ITaskItem [] JavaStubStampFiles { get; set; }

		[Required]
		public string JavaSourceDirectory { get; set; }

		[Required]
		public string JavaClassDirectory { get; set; }

		[Required]
		public string JarDirectory { get; set; }

		public string [] Jars { get; set; }

		public override bool RunTask ()
		{
			var classPath = new List<string> (Jars);

			var needsCompilation = new HashSet<string> (JavaStubStampFiles.Length, StringComparer.OrdinalIgnoreCase);
			foreach (var stampFile in JavaStubStampFiles) {
				needsCompilation.Add (Path.GetFileNameWithoutExtension (stampFile.ItemSpec));
			}

			for (int i = 0; i < JavaStubStampFiles.Length; i++) {
				var stampFile = JavaStubStampFiles [i];
				var fileName = Path.GetFileNameWithoutExtension (stampFile.ItemSpec);
				var references = stampFile.GetMetadata ("AssemblyReferences");
				if (!string.IsNullOrEmpty (references)) {
					foreach (var reference in references.Split (';')) {
						if (needsCompilation.Contains (reference)) {
							Log.LogDebugMessage ($"Compiling {reference}.jar first, {fileName}.jar references it.");
							if (!CompileAndCompress (reference, needsCompilation, classPath))
								return false;
						}
					}
				}

				if (needsCompilation.Contains (fileName) && !CompileAndCompress (fileName, needsCompilation, classPath)) {
					return false;
				}
			}

			return !Log.HasLoggedErrors;
		}

		bool CompileAndCompress (string fileName, HashSet<string> needsCompilation, List<string> classPath)
		{
			var jar = Path.Combine (JarDirectory, $"{fileName}.jar");
			Log.LogDebugMessage ($"Compiling {jar}");

			string stubSourceDir = Path.Combine (JavaSourceDirectory, fileName);
			if (!Directory.Exists (stubSourceDir)) {
				Log.LogDebugMessage ($"Skipping nonexistent directory: {stubSourceDir}");
				return true;
			}

			string classesOutputDir = Path.Combine (JavaClassDirectory, fileName);
			if (!Compile (stubSourceDir, classesOutputDir, classPath))
				return false;

			Compress (classesOutputDir, jar);
			classPath.Add (jar); // We now can add to classPath
			needsCompilation.Remove (fileName); // Library already compiled
			return true;
		}
	}
}
