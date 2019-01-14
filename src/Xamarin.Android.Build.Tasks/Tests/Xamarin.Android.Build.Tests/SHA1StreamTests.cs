using System;
using System.IO;
using NUnit.Framework;
using Xamarin.Android.Tasks;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class SHA1StreamTests
	{
		[Test]
		public void HashStreamMatches ()
		{
			string expected, actual;

			using (var memory = new MemoryStream ())
			using (var writer = new StreamWriter (memory)) {
				writer.Write ("foo");
				writer.Flush ();
				expected = MonoAndroidHelper.HashStream (memory);
			}

			using (var sha1 = new SHA1Stream ())
			using (var writer = new StreamWriter (sha1)) {
				writer.Write ("foo");
				writer.Flush ();
				sha1.FlushFinalBlock ();
				actual = BitConverter.ToString (sha1.Hash);
			}

			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void ReuseEqual ()
		{
			string expected, actual;

			using (var sha1 = new SHA1Stream ())
			using (var writer = new StreamWriter (sha1)) {
				writer.Write ("foo");
				writer.Flush ();
				sha1.FlushFinalBlock ();
				expected = BitConverter.ToString (sha1.Hash);

				sha1.Reuse ();

				writer.Write ("foo");
				writer.Flush ();
				sha1.FlushFinalBlock ();
				actual = BitConverter.ToString (sha1.Hash);
			}

			Assert.AreEqual (expected, actual);
		}

		[Test]
		public void ReuseNotEqual ()
		{
			string expected, actual;

			using (var sha1 = new SHA1Stream ())
			using (var writer = new StreamWriter (sha1)) {
				writer.Write ("foo");
				writer.Flush ();
				sha1.FlushFinalBlock ();
				expected = BitConverter.ToString (sha1.Hash);

				sha1.Reuse ();

				writer.Write ("bar");
				writer.Flush ();
				sha1.FlushFinalBlock ();
				actual = BitConverter.ToString (sha1.Hash);
			}

			Assert.AreNotEqual (expected, actual);
		}

		[Test]
		public void HashStreamMatchesReused ()
		{
			string expected, actual;

			using (var memory = new MemoryStream ())
			using (var writer = new StreamWriter (memory)) {
				writer.Write ("bar");
				writer.Flush ();
				expected = MonoAndroidHelper.HashStream (memory);
			}

			using (var sha1 = new SHA1Stream ())
			using (var writer = new StreamWriter (sha1)) {
				writer.Write ("foo");
				writer.Flush ();
				sha1.FlushFinalBlock ();

				sha1.Reuse ();

				writer.Write ("bar");
				writer.Flush ();
				sha1.FlushFinalBlock ();
				actual = BitConverter.ToString (sha1.Hash);
			}

			Assert.AreEqual (expected, actual);
		}
	}
}
