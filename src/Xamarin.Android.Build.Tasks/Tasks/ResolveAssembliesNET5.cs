using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using MonoDroid.Tuner;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssembliesNET5 : AndroidAsyncTask
	{
		public override string TaskPrefix => "RASN";

		protected readonly List<string> do_not_package_atts = new List<string> ();
		protected readonly Dictionary<string, int> api_levels = new Dictionary<string, int> ();

		// The user's assemblies to package
		[Required]
		public ITaskItem [] Assemblies { get; set; }

		[Required]
		public string TargetFrameworkVersion { get; set; }

		[Required]
		public string ProjectFile { get; set; }

		[Required]
		public string ReferenceAssembliesDirectory { get; set; }

		public string I18nAssemblies { get; set; }

		// The user's assemblies, and all referenced assemblies
		[Output]
		public ITaskItem [] ResolvedAssemblies { get; set; }

		[Output]
		public ITaskItem [] ResolvedUserAssemblies { get; set; }

		[Output]
		public ITaskItem [] ResolvedFrameworkAssemblies { get; set; }

		[Output]
		public ITaskItem [] ResolvedSymbols { get; set; }

		[Output]
		public string [] ResolvedDoNotPackageAttributes { get; set; }

		public override System.Threading.Tasks.Task RunTaskAsync ()
		{
			using (var resolver = new MetadataResolver ()) {
				foreach (var dir in ReferenceAssembliesDirectory.Split (new char [] { ';' }, StringSplitOptions.RemoveEmptyEntries))
					resolver.AddSearchDirectory (dir);

				Execute (resolver);
			}
			return Done;
		}

		protected virtual void Execute (MetadataResolver resolver)
		{
			var assemblies = new Dictionary<string, ITaskItem> (Assemblies.Length);

			foreach (var assembly in Assemblies) {
				string assemblyPath = resolver.Resolve (assembly.ItemSpec);
				string assemblyName = Path.GetFileNameWithoutExtension (assemblyPath);
				if (assemblies.ContainsKey (assemblyName)) {
					Log.LogDebugMessage ($"Skipping {assembly.ItemSpec}, as a duplicate.");
					continue;
				}
				if (IsReferenceAssembly (assembly, assemblyPath)) {
					Log.LogDebugMessage ($"Skipping {assembly.ItemSpec}, it is a reference assembly.");
					continue;
				}
				using (var stream = File.OpenRead (assemblyPath))
				using (var pe = new PEReader (stream)) {
					var reader = pe.GetMetadataReader ();
					var assemblyDefinition = reader.GetAssemblyDefinition ();
					if (MonoAndroidHelper.IsReferenceAssembly (reader, assemblyDefinition)) {
						Log.LogDebugMessage ($"Skipping {assembly.ItemSpec}, it is a reference assembly.");
						continue;
					}
					CheckAssemblyAttributes (assemblyDefinition, reader, out _);

					var taskItem = new TaskItem (assembly) {
						ItemSpec = Path.GetFullPath (assemblyPath),
					};
					if (string.IsNullOrEmpty (taskItem.GetMetadata ("ReferenceAssembly"))) {
						taskItem.SetMetadata ("ReferenceAssembly", taskItem.ItemSpec);
					}
					assemblies [assemblyName] = taskItem;
				}
			}

			AddI18nAssemblies (resolver, assemblies);
			WarnForTargetFrameworkVersions ();
			SetOutputs (assemblies);
		}

		protected static bool IsReferenceAssembly(ITaskItem assembly, string assemblyPath) =>
			!string.IsNullOrEmpty (assembly.GetMetadata ("NuGetPackageId")) && assemblyPath.Contains ($"{Path.DirectorySeparatorChar}ref{Path.DirectorySeparatorChar}");

		protected void WarnForTargetFrameworkVersions ()
		{
			var mainapiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (TargetFrameworkVersion);
			foreach (var item in api_levels.Where (x => mainapiLevel < x.Value)) {
				var itemOSVersion = MonoAndroidHelper.SupportedVersions.GetFrameworkVersionFromApiLevel (item.Value);
				LogCodedWarning ("XA0105", ProjectFile, 0,
					"The $(TargetFrameworkVersion) for {0} ({1}) is greater than the $(TargetFrameworkVersion) for your project ({2}). " +
					"You need to increase the $(TargetFrameworkVersion) for your project.", Path.GetFileName (item.Key), itemOSVersion, TargetFrameworkVersion);
			}
		}

		protected void SetOutputs (Dictionary<string, ITaskItem> assemblies)
		{
			var resolvedAssemblies = new List<ITaskItem> (assemblies.Count);
			var resolvedSymbols = new List<ITaskItem> (assemblies.Count);
			var resolvedFrameworkAssemblies = new List<ITaskItem> (assemblies.Count);
			var resolvedUserAssemblies = new List<ITaskItem> (assemblies.Count);
			foreach (var assembly in assemblies.Values) {
				var mdb = assembly + ".mdb";
				var pdb = Path.ChangeExtension (assembly.ItemSpec, "pdb");
				if (File.Exists (mdb))
					resolvedSymbols.Add (new TaskItem (mdb));
				if (File.Exists (pdb) && Files.IsPortablePdb (pdb))
					resolvedSymbols.Add (new TaskItem (pdb));
				resolvedAssemblies.Add (assembly);
				if (MonoAndroidHelper.IsFrameworkAssembly (assembly.ItemSpec, checkSdkPath: true)) {
					resolvedFrameworkAssemblies.Add (assembly);
				} else {
					resolvedUserAssemblies.Add (assembly);
				}
			}
			ResolvedAssemblies = resolvedAssemblies.ToArray ();
			ResolvedSymbols = resolvedSymbols.ToArray ();
			ResolvedFrameworkAssemblies = resolvedFrameworkAssemblies.ToArray ();
			ResolvedUserAssemblies = resolvedUserAssemblies.ToArray ();
			ResolvedDoNotPackageAttributes = do_not_package_atts.ToArray ();
		}

		protected void CheckAssemblyAttributes (AssemblyDefinition assembly, MetadataReader reader, out string targetFrameworkIdentifier)
		{
			targetFrameworkIdentifier = null;

			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				switch (reader.GetCustomAttributeFullName (attribute)) {
					case "Java.Interop.DoNotPackageAttribute": {
							var arguments = attribute.GetCustomAttributeArguments ();
							if (arguments.FixedArguments.Length > 0) {
								string file = arguments.FixedArguments [0].Value?.ToString ();
								if (string.IsNullOrWhiteSpace (file))
									LogError ("In referenced assembly {0}, Java.Interop.DoNotPackageAttribute requires non-null file name.", assembly.GetAssemblyName ().FullName);
								do_not_package_atts.Add (Path.GetFileName (file));
							}
						}
						break;
					case "System.Runtime.Versioning.TargetFrameworkAttribute": {
							var arguments = attribute.GetCustomAttributeArguments ();
							foreach (var p in arguments.FixedArguments) {
								// Of the form "MonoAndroid,Version=v8.1"
								var value = p.Value?.ToString ();
								if (!string.IsNullOrEmpty (value)) {
									int commaIndex = value.IndexOf (",", StringComparison.Ordinal);
									if (commaIndex != -1) {
										targetFrameworkIdentifier = value.Substring (0, commaIndex);
										if (targetFrameworkIdentifier == "MonoAndroid") {
											const string match = "Version=";
											var versionIndex = value.IndexOf (match, commaIndex, StringComparison.Ordinal);
											if (versionIndex != -1) {
												versionIndex += match.Length;
												string version = value.Substring (versionIndex, value.Length - versionIndex);
												var apiLevel = MonoAndroidHelper.SupportedVersions.GetApiLevelFromFrameworkVersion (version);
												if (apiLevel != null) {
													var assemblyName = reader.GetString (assembly.Name);
													LogDebugMessage ("{0}={1}", assemblyName, apiLevel);
													api_levels [assemblyName] = apiLevel.Value;
												}
											}
										}
									}
								}
							}
						}
						break;
					default:
						break;
				}
			}
		}

		protected void AddI18nAssemblies (MetadataResolver resolver, Dictionary<string, ITaskItem> assemblies)
		{
			var i18n = Linker.ParseI18nAssemblies (I18nAssemblies);

			// Check if we should add any I18N assemblies
			if (i18n == Mono.Linker.I18nAssemblies.None)
				return;

			ResolveI18nAssembly (resolver, "I18N", assemblies);

			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.CJK))
				ResolveI18nAssembly (resolver, "I18N.CJK", assemblies);

			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.MidEast))
				ResolveI18nAssembly (resolver, "I18N.MidEast", assemblies);

			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Other))
				ResolveI18nAssembly (resolver, "I18N.Other", assemblies);

			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.Rare))
				ResolveI18nAssembly (resolver, "I18N.Rare", assemblies);

			if (i18n.HasFlag (Mono.Linker.I18nAssemblies.West))
				ResolveI18nAssembly (resolver, "I18N.West", assemblies);
		}

		protected void ResolveI18nAssembly (MetadataResolver resolver, string name, Dictionary<string, ITaskItem> assemblies)
		{
			var assembly = resolver.Resolve (name);
			assemblies [name] = CreateAssemblyTaskItem (assembly);
		}

		protected static ITaskItem CreateAssemblyTaskItem (string assembly, string targetFrameworkIdentifier = null)
		{
			var assemblyFullPath = Path.GetFullPath (assembly);
			var dictionary = new Dictionary<string, string> (2) {
				{ "ReferenceAssembly", assemblyFullPath },
			};
			if (!string.IsNullOrEmpty (targetFrameworkIdentifier))
				dictionary.Add ("TargetFrameworkIdentifier", targetFrameworkIdentifier);
			return new TaskItem (assemblyFullPath, dictionary);
		}
	}
}
