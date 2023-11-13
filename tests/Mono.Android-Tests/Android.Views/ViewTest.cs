using System;
using System.Threading;
using Android.App;
using Android.Views;
using Java.InteropTests;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests
{
	[TestFixture]
	public class ViewTest
	{
		//[Test]
		public void Post ()
		{
			WeakReference reference = null;

			FinalizerHelpers.PerformNoPinAction (() => {
				object o = new object ();
				reference = new (o);
				var view = new View (Application.Context);
				var result = view.Post(() => o.ToString());
				Assert.IsFalse (result, "Post() should return false!");
			});

			Assert.NotNull (reference, "`reference` should not be null!");
			Assert.IsFalse (reference.IsAlive, "`o` should not be alive!");
		}
	}
}
