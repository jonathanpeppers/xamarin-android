using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Microsoft.Build.Framework;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A base class for sharing a single DirectoryAssemblyResolver instance across MSBuild tasks
	/// </summary>
	public abstract class DirectoryAssemblyResolverTask : AndroidTask
	{
		static readonly string ResolverKey = $"{nameof (DirectoryAssemblyResolver)}_Resolver";
		static readonly string JavaTypesKey = $"{nameof (DirectoryAssemblyResolver)}_JavaTypes";
		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;

		internal const string AndroidSkipJavaStubGeneration = "AndroidSkipJavaStubGeneration";

		readonly Lazy<List<string>> assemblies;

		public DirectoryAssemblyResolverTask ()
		{
			assemblies = new Lazy<List<string>> (GetAssemblies);
		}

		[Required]
		public ITaskItem [] ResolvedAssemblies { get; set; }

		[Required]
		public ITaskItem [] ResolvedUserAssemblies { get; set; }

		[Required]
		public ITaskItem [] FrameworkDirectories { get; set; }

		public bool ErrorOnCustomJavaObject { get; set; }

		/// <summary>
		/// Creates a new DirectoryAssemblyResolver instance with loadDebugSymbols=true
		/// </summary>
		protected DirectoryAssemblyResolver NewResolver ()
		{
			var res = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true);

			foreach (var dir in FrameworkDirectories) {
				if (Directory.Exists (dir.ItemSpec))
					res.SearchDirectories.Add (dir.ItemSpec);
			}
			// Put every assembly we'll need in the resolver
			foreach (var assembly in ResolvedAssemblies) {
				if (bool.TryParse (assembly.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {assembly.ItemSpec}");
					continue;
				}
				res.Load (assembly.ItemSpec);
			}

			BuildEngine4.RegisterTaskObject (ResolverKey, res, Lifetime, allowEarlyCollection: false);
			return res;
		}

		/// <summary>
		/// Retrieves a cached DirectoryAssemblyResolver, but creates a new one if needed
		/// </summary>
		protected DirectoryAssemblyResolver GetResolver ()
		{
			var res = BuildEngine4.GetRegisteredTaskObject (ResolverKey, Lifetime) as DirectoryAssemblyResolver;
			if (res != null) {
				Log.LogDebugMessage ($"Using cached value for {nameof (GetResolver)}");
				return res;
			}
			return NewResolver ();
		}

		/// <summary>
		/// Gets a list of TypeDefinition suitable for GenerateJavaStubs
		/// </summary>
		protected List<TypeDefinition> GetJavaTypes (DirectoryAssemblyResolver resolver = null)
		{
			var all_java_types = BuildEngine4.GetRegisteredTaskObject (JavaTypesKey, Lifetime) as List<TypeDefinition>;
			if (all_java_types != null) {
				Log.LogDebugMessage ($"Using cached value for {nameof (GetJavaTypes)}");
				return all_java_types;
			}

			if (resolver == null) {
				resolver = GetResolver ();
			}
			var scanner = new JavaTypeScanner (this.CreateTaskLogger ()) {
				ErrorOnCustomJavaObject = ErrorOnCustomJavaObject,
			};
			all_java_types = scanner.GetJavaTypes (Assemblies, resolver);
			BuildEngine4.RegisterTaskObject (JavaTypesKey, all_java_types, Lifetime, allowEarlyCollection: false);
			return all_java_types;
		}

		/// <summary>
		/// The list of all loaded assemblies
		/// </summary>
		protected List<string> Assemblies => assemblies.Value;

		List<string> GetAssemblies ()
		{
			// We only want to look for JLO types in user code
			List<string> assemblies = new List<string> (ResolvedUserAssemblies.Length);
			foreach (var asm in ResolvedUserAssemblies) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				if (!assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (asm.ItemSpec)))
					continue;
				Log.LogDebugMessage ($"Adding {asm.ItemSpec} to assemblies.");
				assemblies.Add (asm.ItemSpec);
			}
			foreach (var asm in MonoAndroidHelper.GetFrameworkAssembliesToTreatAsUserAssemblies (ResolvedAssemblies)) {
				if (bool.TryParse (asm.GetMetadata (AndroidSkipJavaStubGeneration), out bool value) && value) {
					Log.LogDebugMessage ($"Skipping Java Stub Generation for {asm.ItemSpec}");
					continue;
				}
				if (!assemblies.All (x => Path.GetFileName (x) != Path.GetFileName (asm.ItemSpec)))
					continue;
				Log.LogDebugMessage ($"Adding {asm.ItemSpec} to assemblies.");
				assemblies.Add (asm.ItemSpec);
			}
			return assemblies;
		}

		/// <summary>
		/// Unregisters any cached values, and disposes the DirectoryAssemblyResolver
		/// </summary>
		protected void UnregisterAll ()
		{
			var engine = BuildEngine4;
			engine.UnregisterTaskObject (JavaTypesKey, Lifetime);

			var res = engine.UnregisterTaskObject (ResolverKey, Lifetime) as DirectoryAssemblyResolver;
			if (res != null) {
				Log.LogDebugMessage ($"Disposing {nameof (DirectoryAssemblyResolver)}");
				res.Dispose ();
			}
		}
	}
}
