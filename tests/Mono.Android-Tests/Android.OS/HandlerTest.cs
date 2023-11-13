using System;
using System.Threading;
using Android.OS;
using Android.Runtime;
using Java.InteropTests;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class HandlerTest {

		[Test]
		public void RemoveDisposedInstance ()
		{
      using (var t = new HandlerThread ("RemoveDisposedInstance")) {
        t.Start ();
        using (var h = new Handler (t.Looper)) {
    			var e = new ManualResetEvent (false);
          Java.Lang.Runnable r = null;
          r = new Java.Lang.Runnable (() => {
            e.Set ();
            r.Dispose ();
          });
          h.Post (r.Run);
          e.WaitOne ();
        }
        
        t.QuitSafely ();
			}
		}

		[Test]
		public void Post ()
		{
			WeakReference reference = null;

			FinalizerHelpers.PerformNoPinAction (() => {
				object o = new object ();
				reference = new (o);
				var handler = new Handler (EmptyLooper.Create ());
				var result = handler.Post(() => o.ToString());
				Assert.IsFalse (result, "Post() should return false!");
			});

			Assert.NotNull (reference, "`reference` should not be null!");
			Assert.IsFalse (reference.IsAlive, "`o` should not be alive!");
		}

		class EmptyLooper : Looper
		{
			public static EmptyLooper Create ()
			{
				var looper = Looper.MainLooper;
				return new EmptyLooper (looper.Handle, JniHandleOwnership.DoNotTransfer);
			}

			protected EmptyLooper (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer) { }

			public override MessageQueue Queue => null;
		}
	}
}