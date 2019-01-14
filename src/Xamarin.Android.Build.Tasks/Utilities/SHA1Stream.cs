using System;
using System.IO;
using System.Security.Cryptography;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A simple class to wrap MemoryStream and calculate a SHA1 hash as the Stream is written.
	/// </summary>
	public class SHA1Stream : Stream
	{
		readonly MemoryStream memoryStream;
		HashAlgorithm hash;

		public SHA1Stream ()
		{
			memoryStream = new MemoryStream ();
			hash = new SHA1Managed ();
		}

		public void Reuse ()
		{
			memoryStream.SetLength (0);
			hash.Dispose ();
			hash = new SHA1Managed ();
			HasFlushedFinalBlock = false;
		}

		public byte [] Hash => hash.Hash;

		public bool HasFlushedFinalBlock { get; private set; } = false;

		/// <summary>
		/// Calls HashAlgorithm.TransformFinalBlock if needed
		/// </summary>
		public void FlushFinalBlock ()
		{
			if (HasFlushedFinalBlock)
				return;
			HasFlushedFinalBlock = true;
			hash.TransformFinalBlock (new byte [0], 0, 0);
		}

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => true;

		public override long Length => memoryStream.Length;

		public override long Position {
			get => memoryStream.Position;
			set => memoryStream.Position = value;
		}

		public override void Flush () => memoryStream.Flush ();

		public override int Read (byte [] buffer, int offset, int count) => memoryStream.Read (buffer, offset, count);

		public override long Seek (long offset, SeekOrigin origin) => memoryStream.Seek (offset, origin);

		public override void SetLength (long value) => memoryStream.SetLength (0);

		public override void Write (byte [] buffer, int offset, int count)
		{
			if (HasFlushedFinalBlock)
				throw new InvalidOperationException ($"Cannot write to stream after {nameof (FlushFinalBlock)} has been called!");
			hash.TransformBlock (buffer, offset, count, buffer, offset);
			memoryStream.Write (buffer, offset, count);
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				memoryStream.Dispose ();
				hash.Dispose ();
			}
		}
	}
}
