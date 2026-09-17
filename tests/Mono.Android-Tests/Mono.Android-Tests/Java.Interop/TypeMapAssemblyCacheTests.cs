using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Java.Interop;
using Microsoft.Android.Runtime;
using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	[Category ("TypeMapAssemblyCache")]
	public class TypeMapAssemblyCacheTests
	{
		[Test]
		[UnconditionalSuppressMessage ("Trimming", "IL2026", Justification = "Tokens come from directly referenced types at runtime, after trimming.")]
		public void ResolvesDifferentTokensFromOneAssembly ()
		{
			AssumeCoreClrTypeMap ();
			var expectedAssembly = typeof (Java.Lang.Object).Assembly;
			string name = expectedAssembly.GetName ().Name
				?? throw new InvalidOperationException ("Mono.Android assembly name is unavailable.");
			int loads = 0;
			var cache = new TypeMapAssemblyCache (assemblyName => {
				loads++;
				return Assembly.Load (assemblyName);
			});
			Type[] types = [typeof (Java.Lang.Object), typeof (Java.Lang.String), typeof (Android.Views.View)];
			foreach (var type in types) {
				var assembly = cache.GetOrLoad (name);
				Assert.AreSame (expectedAssembly, assembly);
				Assert.AreSame (type, assembly.ManifestModule.ResolveType (type.MetadataToken));
			}
			Assert.AreEqual (1, loads);
		}

		[Test]
		public void NativeTypeMapPreservesTypeIdentity ()
		{
			AssumeCoreClrTypeMap ();
			for (int i = 0; i < 2; i++) {
				Assert.AreSame (typeof (Java.Lang.Object), TypeManager.GetJavaToManagedType ("java/lang/Object"));
				Assert.AreSame (typeof (Java.Lang.String), TypeManager.GetJavaToManagedType ("java/lang/String"));
				Assert.AreSame (typeof (Android.Views.View), TypeManager.GetJavaToManagedType ("android/view/View"));
				Assert.IsNull (TypeManager.GetJavaToManagedType ("net/dot/android/test/MissingCacheType"));
			}
		}

		static void AssumeCoreClrTypeMap ()
		{
			if (!RuntimeFeature.IsCoreClrRuntime || RuntimeFeature.TrimmableTypeMap)
				Assert.Ignore ("Requires CoreCLR with the LLVM-IR typemap.");
		}
	}
}
