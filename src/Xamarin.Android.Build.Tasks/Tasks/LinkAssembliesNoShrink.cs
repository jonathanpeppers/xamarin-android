using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;

namespace Xamarin.Android.Tasks
{
	public class LinkAssembliesNoShrink : Task
	{
		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; }

		[Required]
		public string OutputDirectory { get; set; }

		public override bool Execute ()
		{
			var readerParameters = new ReaderParameters {
				ReadSymbols = true,
				SymbolReaderProvider = new DefaultSymbolReaderProvider (throwIfNoSymbol: false),
			};
			var writerParameters = new WriterParameters {
				WriteSymbols = true,
				SymbolWriterProvider = new DefaultSymbolWriterProvider (),
			};

			using (var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true, loadReaderParameters: readerParameters)) {
				// Add search directories
				foreach (var assembly in ResolvedAssemblies) {
					var path = Path.GetFullPath (Path.GetDirectoryName (assembly.ItemSpec));
					if (!resolver.SearchDirectories.Contains (path))
						resolver.SearchDirectories.Add (path);
				}

				// Run the FixAbstractMethodsStep
				var step = new FixAbstractMethodsStep (resolver, Log);
				foreach (var assembly in ResolvedAssemblies) {
					var destination = Path.Combine (OutputDirectory, Path.GetFileName (assembly.ItemSpec));

					// Only run the step on "MonoAndroid" assemblies
					if (!MonoAndroidHelper.IsSharedRuntimeAssembly (assembly.ItemSpec) && MonoAndroidHelper.IsMonoAndroidAssembly (assembly)) {
						Log.LogDebugMessage ($"Running FixAbstractMethods: {assembly.ItemSpec}");
						var assemblyDefinition = resolver.GetAssembly (assembly.ItemSpec);
						if (step.FixAbstractMethods (assemblyDefinition)) {
							Log.LogDebugMessage ($"Saving modified assembly: {assembly.ItemSpec}");
							assemblyDefinition.Write (destination, writerParameters);
							continue;
						}
					}

					Log.LogDebugMessage ($"Copying: {assembly.ItemSpec}");
					MonoAndroidHelper.CopyAssemblyAndSymbols (assembly.ItemSpec, destination);
				}
			}

			return !Log.HasLoggedErrors;
		}

		class FixAbstractMethodsStep : MonoDroid.Tuner.FixAbstractMethodsStep
		{
			readonly DirectoryAssemblyResolver resolver;
			readonly TaskLoggingHelper logger;

			public FixAbstractMethodsStep (DirectoryAssemblyResolver resolver, TaskLoggingHelper logger)
			{
				this.resolver = resolver;
				this.logger = logger;
			}

			protected override AssemblyDefinition GetMonoAndroidAssembly ()
			{
				return resolver.GetAssembly ("Mono.Android.dll");
			}

			public override void LogMessage (string message, params object [] values)
			{
				logger.LogDebugMessage (message, values);
			}
		}
	}
}
