// Copyright (C) 2011, Xamarin Inc.
// Copyright (C) 2010, Novell Inc.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Xamarin.Android.Tasks
{
	public class ResolveAssemblies : ResolveAssembliesNET5
	{
		public override string TaskPrefix => "RSA";

		public string ProjectAssetFile { get; set; }

		public string TargetMoniker { get; set; }

		protected override void Execute (MetadataResolver resolver)
		{
			var assemblies = new Dictionary<string, ITaskItem> (Assemblies.Length);
			var topAssemblyReferences = new List<string> (Assemblies.Length);
			var logger = new NuGetLogger ((s) => {
				LogDebugMessage ("{0}", s);
			});

			LockFile lockFile = null;
			if (!string.IsNullOrEmpty (ProjectAssetFile) && File.Exists (ProjectAssetFile)) {
				lockFile = LockFileUtilities.GetLockFile (ProjectAssetFile, logger);
			}

			try {
				foreach (var assembly in Assemblies) {
					// Add each user assembly and all referenced assemblies (recursive)
					string resolved_assembly = resolver.Resolve (assembly.ItemSpec);
					if (IsReferenceAssembly (assembly, resolved_assembly) || MonoAndroidHelper.IsReferenceAssembly (resolved_assembly)) {
						// Resolve "runtime" library
						if (lockFile != null)
							resolved_assembly = ResolveRuntimeAssemblyForReferenceAssembly (lockFile, assembly.ItemSpec);
						if (lockFile == null || resolved_assembly == null) {
							var file = resolved_assembly ?? assembly.ItemSpec;
							LogCodedWarning ("XA0107", file, 0, "Ignoring Reference Assembly `{0}`.", file);
							continue;
						}
					}
					LogDebugMessage ($"Adding {resolved_assembly} to topAssemblyReferences");
					topAssemblyReferences.Add (resolved_assembly);
					resolver.AddSearchDirectory (Path.GetDirectoryName (resolved_assembly));
					var taskItem = new TaskItem (assembly) {
						ItemSpec = Path.GetFullPath (resolved_assembly),
					};
					if (string.IsNullOrEmpty (taskItem.GetMetadata ("ReferenceAssembly"))) {
						taskItem.SetMetadata ("ReferenceAssembly", taskItem.ItemSpec);
					}
					string assemblyName = Path.GetFileNameWithoutExtension (resolved_assembly);
					assemblies [assemblyName] = taskItem;
				}
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return;
			}
			try {
				foreach (var assembly in topAssemblyReferences)
					AddAssemblyReferences (resolver, assemblies, assembly, null);
			} catch (Exception ex) {
				LogError ("Exception while loading assemblies: {0}", ex);
				return;
			}

			AddI18nAssemblies (resolver, assemblies);
			WarnForTargetFrameworkVersions ();
			SetOutputs (assemblies);
		}

		int indent = 2;

		string ResolveRuntimeAssemblyForReferenceAssembly (LockFile lockFile, string assemblyPath)
		{
			if (string.IsNullOrEmpty(TargetMoniker)) 
				return null;

			var framework = NuGetFramework.Parse (TargetMoniker);
			if (framework == null) {
				LogCodedWarning ("XA0118", $"Could not parse '{TargetMoniker}'");
				return null;
			}
			var target = lockFile.GetTarget (framework, string.Empty);
			if (target == null) {
				LogCodedWarning ("XA0118", $"Could not resolve target for '{TargetMoniker}'");
				return null;
			}
			foreach (var folder in lockFile.PackageFolders) {
				var path = assemblyPath.Replace (folder.Path, string.Empty);
				if (path.StartsWith ($"{Path.DirectorySeparatorChar}"))
					path = path.Substring (1);
				var libraryPath = lockFile.Libraries.FirstOrDefault (x => path.StartsWith (x.Path.Replace('/', Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
				if (libraryPath == null)
					continue;
				var library = target.Libraries.FirstOrDefault (x => String.Compare (x.Name, libraryPath.Name, StringComparison.OrdinalIgnoreCase) == 0);
				if (libraryPath == null)
					continue;
				var runtime = library.RuntimeAssemblies.FirstOrDefault ();
				if (runtime == null)
					continue;
				path = Path.Combine (folder.Path, libraryPath.Path, runtime.Path).Replace('/', Path.DirectorySeparatorChar);
				if (!File.Exists (path))
					continue;
				// _._ means its provided by the framework. However if we get here
				// its NOT. So lets use what we got in the first place.
				if (Path.GetFileName (path) == "_._")
					return assemblyPath;
				return path;
			}
			return null;
		}

		void AddAssemblyReferences (MetadataResolver resolver, Dictionary<string, ITaskItem> assemblies, string assemblyPath, List<string> resolutionPath)
		{
			var reader = resolver.GetAssemblyReader (assemblyPath);
			var assembly = reader.GetAssemblyDefinition ();
			var assemblyName = reader.GetString (assembly.Name);

			// Don't repeat assemblies we've already done
			bool topLevel = resolutionPath == null;
			if (!topLevel && assemblies.ContainsKey (assemblyName))
				return;

			if (resolutionPath == null)
				resolutionPath = new List<string>();

			CheckAssemblyAttributes (assembly, reader, out string targetFrameworkIdentifier);

			LogMessage ("{0}Adding assembly reference for {1}, recursively...", new string (' ', indent), assemblyName);
			resolutionPath.Add (assemblyName);
			indent += 2;

			// Add this assembly
			ITaskItem assemblyItem;
			if (topLevel) {
				if (assemblies.TryGetValue (assemblyName, out assemblyItem)) {
					if (!string.IsNullOrEmpty (targetFrameworkIdentifier) && string.IsNullOrEmpty (assemblyItem.GetMetadata ("TargetFrameworkIdentifier"))) {
						assemblyItem.SetMetadata ("TargetFrameworkIdentifier", targetFrameworkIdentifier);
					}
				}
			} else {
				assemblies [assemblyName] = 
					assemblyItem = CreateAssemblyTaskItem (assemblyPath, targetFrameworkIdentifier);
			}

			// Recurse into each referenced assembly
			foreach (var handle in reader.AssemblyReferences) {
				var reference = reader.GetAssemblyReference (handle);
				string reference_assembly;
				try {
					var referenceName = reader.GetString (reference.Name);
					if (assemblyItem != null && referenceName == "Mono.Android") {
						assemblyItem.SetMetadata ("HasMonoAndroidReference", "True");
					}
					reference_assembly = resolver.Resolve (referenceName);
				} catch (FileNotFoundException ex) {
					var references = new StringBuilder ();
					for (int i = 0; i < resolutionPath.Count; i++) {
						if (i != 0)
							references.Append (" > ");
						references.Append ('`');
						references.Append (resolutionPath [i]);
						references.Append ('`');
					}

					string missingAssembly = ex.FileName;
					if (missingAssembly.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
						missingAssembly = Path.GetFileNameWithoutExtension (missingAssembly);
					}
					string message = $"Can not resolve reference: `{missingAssembly}`, referenced by {references}.";
					if (MonoAndroidHelper.IsFrameworkAssembly (ex.FileName)) {
						LogCodedError ("XA2002", $"{message} Perhaps it doesn't exist in the Mono for Android profile?");
					} else {
						LogCodedError ("XA2002", $"{message} Please add a NuGet package or assembly reference for `{missingAssembly}`, or remove the reference to `{resolutionPath [0]}`.");
					}
					return;
				}
				AddAssemblyReferences (resolver, assemblies, reference_assembly, resolutionPath);
			}

			indent -= 2;
			resolutionPath.RemoveAt (resolutionPath.Count - 1);
		}
	}
}
