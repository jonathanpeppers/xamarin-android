using System.Collections.Generic;
using Java.Interop.Tools.Cecil;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A base class for sharing a single DirectoryAssemblyResolver instance across MSBuild tasks
	/// </summary>
	public abstract class DirectoryAssemblyResolverTask : AndroidTask
	{
		static readonly string ResolverKey = $"{nameof (DirectoryAssemblyResolver)}_Resolver";
		static readonly string ManifestProvidersKey = $"{nameof (DirectoryAssemblyResolver)}_ManifestProviders";
		const RegisteredTaskObjectLifetime Lifetime = RegisteredTaskObjectLifetime.Build;

		/// <summary>
		/// Creates a new DirectoryAssemblyResolver instance with loadDebugSymbols=true
		/// </summary>
		public DirectoryAssemblyResolver NewResolver ()
		{
			Log.LogDebugMessage ($"Creating new {nameof (DirectoryAssemblyResolver)}");
			var resolver = new DirectoryAssemblyResolver (this.CreateTaskLogger (), loadDebugSymbols: true);
			BuildEngine4.RegisterTaskObject (ResolverKey, resolver, Lifetime, allowEarlyCollection: false);
			return resolver;
		}

		public DirectoryAssemblyResolver GetResolver () =>
			BuildEngine4.GetRegisteredTaskObject (ResolverKey, Lifetime) as DirectoryAssemblyResolver;

		public void CacheManifestProviders (IList<string> providers)
		{
			Log.LogDebugMessage ($"{nameof (CacheManifestProviders)}:");
			if (providers != null) {
				foreach (var provider in providers) {
					Log.LogDebugMessage ($"    {provider}");
				}
			}
			BuildEngine4.RegisterTaskObject (ManifestProvidersKey, providers, Lifetime, allowEarlyCollection: false);
		}

		public IList<string> GetCachedManifestProviders () =>
			BuildEngine4.GetRegisteredTaskObject (ManifestProvidersKey, Lifetime) as IList<string>;

		/// <summary>
		/// Unregisters any cached values, and disposes the DirectoryAssemblyResolver
		/// </summary>
		public void UnregisterAll ()
		{
			var engine = BuildEngine4;
			engine.UnregisterTaskObject (ManifestProvidersKey, Lifetime);

			var resolver = engine.UnregisterTaskObject (ResolverKey, Lifetime) as DirectoryAssemblyResolver;
			if (resolver != null) {
				Log.LogDebugMessage ($"Disposing {nameof (DirectoryAssemblyResolver)}");
				resolver.Dispose ();
			}
		}
	}
}
