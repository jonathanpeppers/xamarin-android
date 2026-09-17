using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Java.Interop;

internal sealed class TypeMapAssemblyCache
{
	readonly Dictionary<string, Assembly> assemblies = new (StringComparer.Ordinal);
	readonly Func<string, Assembly> loadAssembly;
	readonly bool defaultLoadContext = AssemblyLoadContext.GetLoadContext (typeof (TypeMapAssemblyCache).Assembly) == AssemblyLoadContext.Default;

	internal TypeMapAssemblyCache (Func<string, Assembly> loadAssembly)
	{
		ArgumentNullException.ThrowIfNull (loadAssembly);
		this.loadAssembly = loadAssembly;
	}

	internal Assembly GetOrLoad (string assemblyName)
	{
		// Contextual reflection can bind the same name to another assembly.
		if (!defaultLoadContext || AssemblyLoadContext.CurrentContextualReflectionContext != null)
			return loadAssembly (assemblyName);

		lock (assemblies) {
			if (assemblies.TryGetValue (assemblyName, out var cached))
				return cached;
		}

		// Resolution callbacks must not run under the cache lock, and failures are not cached.
		var assembly = loadAssembly (assemblyName);
		// Only retain exact simple-name bindings whose lifetime is already process-wide.
		if (!assembly.IsCollectible && !assembly.IsDynamic &&
				AssemblyLoadContext.GetLoadContext (assembly) == AssemblyLoadContext.Default &&
				string.Equals (assembly.GetName ().Name, assemblyName, StringComparison.Ordinal)) {
			lock (assemblies) {
				assemblies.TryAdd (assemblyName, assembly);
			}
		}
		return assembly;
	}
}
