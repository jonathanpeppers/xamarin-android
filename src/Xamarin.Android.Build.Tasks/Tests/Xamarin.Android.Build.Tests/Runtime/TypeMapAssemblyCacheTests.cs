using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

using Java.Interop;
using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TypeMapAssemblyCacheTests
{
	static Assembly TestAssembly => typeof (TypeMapAssemblyCacheTests).Assembly;
	static string TestAssemblyName => TestAssembly.GetName ().Name
		?? throw new InvalidOperationException ("Test assembly name is unavailable.");

	[Test]
	public void ReusesAssemblyForDifferentTypeTokens ()
	{
		int loads = 0;
		var cache = new TypeMapAssemblyCache (name => {
			loads++;
			return Assembly.Load (name);
		});
		Type[] types = [typeof (TypeMapAssemblyCacheTests), typeof (TypeMapAssemblyCache)];
		foreach (var type in types) {
			var assembly = cache.GetOrLoad (TestAssemblyName);
			Assert.AreSame (TestAssembly, assembly);
			Assert.AreSame (type, assembly.ManifestModule.ResolveType (type.MetadataToken));
		}
		Assert.AreEqual (1, loads);
	}

	[Test]
	public void LoadFailuresAreNotCached ()
	{
		int loads = 0;
		var failure = new FileNotFoundException ("Missing typemap assembly.");
		var cache = new TypeMapAssemblyCache (_ => {
			if (++loads == 1)
				throw failure;
			return TestAssembly;
		});
		Assert.AreSame (failure, Assert.Throws<FileNotFoundException> (() => cache.GetOrLoad (TestAssemblyName)));
		Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
		Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
		Assert.AreEqual (2, loads);
	}

	[Test]
	public void InvalidTypeTokenStillThrowsWithoutRepeatingAssemblyLoad ()
	{
		int loads = 0;
		var cache = new TypeMapAssemblyCache (_ => {
			loads++;
			return TestAssembly;
		});
		for (int i = 0; i < 2; i++)
			Assert.Throws<ArgumentException> (() => cache.GetOrLoad (TestAssemblyName).ManifestModule.ResolveType (0));
		Assert.AreEqual (1, loads);
	}

	[Test]
	public void ContextualReflectionBypassesAndDoesNotReplaceCachedAssembly ()
	{
		var context = new AssemblyLoadContext ("typemap-contextual", isCollectible: true);
		try {
			var alternate = context.LoadFromAssemblyPath (TestAssembly.Location);
			int loads = 0;
			var cache = new TypeMapAssemblyCache (name => {
				loads++;
				return Assembly.Load (name);
			});
			Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
			using (context.EnterContextualReflection ()) {
				Assert.AreSame (alternate, cache.GetOrLoad (TestAssemblyName));
				Assert.AreSame (alternate, cache.GetOrLoad (TestAssemblyName));
			}
			Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
			Assert.AreEqual (3, loads);
		} finally {
			context.Unload ();
		}
	}

	[Test]
	public void AssemblyFromAnotherLoadContextIsNotCached ()
	{
		var context = new AssemblyLoadContext ("typemap-foreign", isCollectible: true);
		try {
			var alternate = context.LoadFromAssemblyPath (TestAssembly.Location);
			int loads = 0;
			var cache = new TypeMapAssemblyCache (_ => {
				loads++;
				return alternate;
			});
			Assert.AreSame (alternate, cache.GetOrLoad (TestAssemblyName));
			Assert.AreSame (alternate, cache.GetOrLoad (TestAssemblyName));
			Assert.AreEqual (2, loads);
		} finally {
			context.Unload ();
		}
	}

	[TestCase (AssemblyBuilderAccess.Run)]
	[TestCase (AssemblyBuilderAccess.RunAndCollect)]
	public void DynamicAssemblyIsNotCached (AssemblyBuilderAccess access)
	{
		var dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly (new AssemblyName ("typemap-dynamic"), access);
		int loads = 0;
		var cache = new TypeMapAssemblyCache (_ => {
			loads++;
			return dynamicAssembly;
		});
		Assert.AreSame (dynamicAssembly, cache.GetOrLoad ("typemap-dynamic"));
		Assert.AreSame (dynamicAssembly, cache.GetOrLoad ("typemap-dynamic"));
		Assert.AreEqual (2, loads);
	}

	[Test]
	public void ResolverAliasIsNotCached ()
	{
		int loads = 0;
		var cache = new TypeMapAssemblyCache (_ => {
			loads++;
			return TestAssembly;
		});
		cache.GetOrLoad ("typemap-alias");
		cache.GetOrLoad ("typemap-alias");
		Assert.AreEqual (2, loads);
	}

	[Test]
	public void QualifiedAssemblyNameIsNotCached ()
	{
		string fullName = TestAssembly.FullName
			?? throw new InvalidOperationException ("Test assembly full name is unavailable.");
		int loads = 0;
		var cache = new TypeMapAssemblyCache (name => {
			loads++;
			return Assembly.Load (name);
		});
		Assert.AreSame (TestAssembly, cache.GetOrLoad (fullName));
		Assert.AreSame (TestAssembly, cache.GetOrLoad (fullName));
		Assert.AreEqual (2, loads);
	}

	[Test]
	public void DefaultContextualReflectionAlsoBypassesCache ()
	{
		int loads = 0;
		var cache = new TypeMapAssemblyCache (name => {
			loads++;
			return Assembly.Load (name);
		});
		using (AssemblyLoadContext.Default.EnterContextualReflection ()) {
			Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
			Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
		}
		Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
		Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
		Assert.AreEqual (3, loads);
	}

	[Test]
	public void CacheInAnotherLoadContextDoesNotCacheDefaultAssemblies ()
	{
		var context = new AssemblyLoadContext ("typemap-cache-owner", isCollectible: true);
		try {
			var alternate = context.LoadFromAssemblyPath (TestAssembly.Location);
			var cacheType = alternate.GetType (typeof (TypeMapAssemblyCache).FullName
				?? throw new InvalidOperationException ("Cache type name is unavailable."), throwOnError: true)
				?? throw new InvalidOperationException ("Cache type is unavailable.");
			var constructor = cacheType.GetConstructor (BindingFlags.Instance | BindingFlags.NonPublic,
				binder: null, [typeof (Func<string, Assembly>)], modifiers: null)
				?? throw new InvalidOperationException ("Cache constructor is unavailable.");
			int loads = 0;
			Func<string, Assembly> loader = _ => {
				loads++;
				return TestAssembly;
			};
			var cache = constructor.Invoke ([loader]);
			var getOrLoad = cacheType.GetMethod ("GetOrLoad", BindingFlags.Instance | BindingFlags.NonPublic)
				?? throw new InvalidOperationException ("Cache lookup is unavailable.");
			Assert.AreSame (TestAssembly, getOrLoad.Invoke (cache, [TestAssemblyName]));
			Assert.AreSame (TestAssembly, getOrLoad.Invoke (cache, [TestAssemblyName]));
			Assert.AreEqual (2, loads);
		} finally {
			context.Unload ();
		}
	}

	[Test]
	public void CacheHitsDoNotAllocate ()
	{
		var cache = new TypeMapAssemblyCache (Assembly.Load);
		string name = TestAssemblyName;
		for (int i = 0; i < 1000; i++)
			cache.GetOrLoad (name);
		long before = GC.GetAllocatedBytesForCurrentThread ();
		for (int i = 0; i < 1000; i++)
			cache.GetOrLoad (name);
		long allocated = GC.GetAllocatedBytesForCurrentThread () - before;
		Assert.AreEqual (0L, allocated);
	}

	[Test]
	public void ConcurrentLookupsPreserveIdentity ()
	{
		int loads = 0;
		var cache = new TypeMapAssemblyCache (_ => {
			Interlocked.Increment (ref loads);
			return TestAssembly;
		});
		Parallel.For (0, 32, _ => Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName)));
		int completedLoads = loads;
		Assert.AreSame (TestAssembly, cache.GetOrLoad (TestAssemblyName));
		Assert.AreEqual (completedLoads, loads);
		Assert.That (loads, Is.InRange (1, 32));
	}

	[Test]
	public void ResolutionDoesNotHoldCacheLock ()
	{
		TypeMapAssemblyCache? cache = null;
		cache = new TypeMapAssemblyCache (name => {
			if (name == "typemap-alias") {
				var current = cache ?? throw new InvalidOperationException ("Cache was not initialized.");
				var nested = Task.Run (() => current.GetOrLoad (TestAssemblyName));
				Assert.IsTrue (nested.Wait (TimeSpan.FromSeconds (5)), "Resolution ran under the cache lock.");
				return nested.Result;
			}
			return TestAssembly;
		});
		Assert.AreSame (TestAssembly, cache.GetOrLoad ("typemap-alias"));
	}
}
