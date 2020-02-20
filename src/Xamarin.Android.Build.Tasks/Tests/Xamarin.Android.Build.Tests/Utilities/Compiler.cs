extern alias system;
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using system::System.CodeDom.Compiler;

namespace Xamarin.Android.Build.Tests
{
	static class Compiler
	{
		const string RoslynEnvironmentVariable = "ROSLYN_COMPILER_LOCATION";
		static string unitTestFrameworkAssemblyPath = typeof (Assert).Assembly.Location;

		static CodeDomProvider GetCodeDomProvider ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				//NOTE: there is an issue where Roslyn's csc.exe isn't copied to output for non-ASP.NET projects
				// Comments on this here: https://stackoverflow.com/a/40311406/132442
				// They added an environment variable as a workaround: https://github.com/aspnet/RoslynCodeDomProvider/pull/12
				if (string.IsNullOrEmpty (Environment.GetEnvironmentVariable (RoslynEnvironmentVariable, EnvironmentVariableTarget.Process))) {
					string roslynPath = Path.GetFullPath (Path.Combine (unitTestFrameworkAssemblyPath, "..", "..", "..", "packages", "microsoft.codedom.providers.dotnetcompilerplatform", "2.0.1", "tools", "RoslynLatest"));
					Environment.SetEnvironmentVariable (RoslynEnvironmentVariable, roslynPath, EnvironmentVariableTarget.Process);
				}

				return new Microsoft.CodeDom.Providers.DotNetCompilerPlatform.CSharpCodeProvider ();
			} else {
				return new system::Microsoft.CSharp.CSharpCodeProvider ();
			}
		}

		public static void Compile (string source, string outputPath, params string[] references)
		{
			var parameters = new CompilerParameters {
				GenerateExecutable = false,
				IncludeDebugInformation = false,
				OutputAssembly = outputPath,
				CompilerOptions = "/optimize+",
			};
			if (references != null)
				parameters.ReferencedAssemblies.AddRange (references);
			using (var codeProvider = GetCodeDomProvider ()) {
				var results = codeProvider.CompileAssemblyFromSource (parameters, source);
				if (results.Errors.Count > 0) {
					var builder = new StringBuilder ();
					foreach (CompilerError message in results.Errors) {
						builder.AppendLine (message.ToString ());
					}
					Assert.Fail (builder.ToString ());
				}
			}
		}
	}
}
