using System;
using System.IO;
using System.Linq;
using Xamarin.Tools.Zip;

namespace Xamarin.Android.Tasks
{
	public class ZipArchiveEx : IDisposable
	{
		static int ZipFlushLimit = 50;

		int count = 0;
		ZipArchive zip;
		string archive;

		public ZipArchive Archive => zip;

		public ZipArchiveEx (string archive) : this (archive, FileMode.CreateNew)
		{
		}

		public ZipArchiveEx (string archive, FileMode filemode)
		{
			this.archive = archive;
			zip = ZipArchive.Open(archive, filemode);
		}

		public void Flush ()
		{
			if (zip != null) {
				zip.Close ();
				zip.Dispose ();
				zip = null;
			}
			count = 0;
			zip = ZipArchive.Open (archive, FileMode.Open);
		}

		string ArchiveNameForFile (string filename, string directoryPathInZip)
		{
			if (string.IsNullOrEmpty (filename)) {
				throw new ArgumentNullException (nameof (filename));
			}
			string pathName;
			if (string.IsNullOrEmpty (directoryPathInZip)) {
				pathName = Path.GetFileName (filename);
			}
			else {
				pathName = Path.Combine (directoryPathInZip, Path.GetFileName (filename));
			}
			return pathName.Replace ("\\", "/");
		}

		void AddFiles (string folder, string folderInArchive, CompressionMethod method)
		{
			foreach (string fileName in Directory.GetFiles (folder, "*.*", SearchOption.TopDirectoryOnly)) {
				var fi = new FileInfo (fileName);
				if ((fi.Attributes & FileAttributes.Hidden) != 0)
					continue;
				var archiveFileName = ArchiveNameForFile (fileName, folderInArchive);
				long index = -1;
				if (zip.ContainsEntry (archiveFileName, out index)) {
					var e = zip.First (x => x.FullName == archiveFileName);
					if (e.ModificationTime < fi.LastWriteTimeUtc)
						AddFile (fileName, archiveFileName, compressionMethod: method);
				} else {
					AddFile (fileName, archiveFileName, compressionMethod: method);
				}
			}
		}

		public void AddEntry (string archivePath, Stream data, CompressionMethod compressionMethod)
		{
			zip.AddEntry (archivePath, data, compressionMethod: compressionMethod);
			if (++count == ZipFlushLimit) {
				Flush ();
			}
		}

		public void AddFile (string sourcePath, string archivePath, CompressionMethod compressionMethod)
		{
			zip.AddFile (sourcePath, archivePath, compressionMethod: compressionMethod);
			if (++count == ZipFlushLimit) {
				Flush ();
			}
		}

		public void RemoveFile (string folder, string file)
		{
			var archiveName = ArchiveNameForFile (file, Path.Combine (folder, Path.GetDirectoryName (file)));
			long index = -1;
			if (zip.ContainsEntry (archiveName, out index))
				zip.DeleteEntry ((ulong)index);
		}

		public void AddDirectory (string folder, string folderInArchive, CompressionMethod method = CompressionMethod.Default)
		{
			if (!string.IsNullOrEmpty (folder)) {
				folder = folder.Replace ('/', Path.DirectorySeparatorChar).Replace ('\\', Path.DirectorySeparatorChar);
				folder = Path.GetFullPath (folder);
				if (folder [folder.Length - 1] == Path.DirectorySeparatorChar) {
					folder = folder.Substring (0, folder.Length - 1);
				}
			}

			AddFiles (folder, folderInArchive, method);
			foreach (string dir in Directory.GetDirectories (folder, "*", SearchOption.AllDirectories)) {
				var di = new DirectoryInfo (dir);
				if ((di.Attributes & FileAttributes.Hidden) != 0)
					continue;
				var internalDir = dir.Replace (folder, string.Empty);
				string fullDirPath = folderInArchive + internalDir;
				try {
					zip.CreateDirectory (fullDirPath);
				} catch (ZipException) {
					
				}
				AddFiles (dir, fullDirPath, method);
			}
		}

		/// <summary>
		/// HACK: aapt2 is creating zip entries on Windows such as `assets\subfolder/asset2.txt`
		/// </summary>
		public void FixupWindowsPathSeparators (Action<string, string> onRename)
		{
			bool modified = false;
			foreach (var entry in zip) {
				if (entry.FullName.Contains ('\\')) {
					var name = entry.FullName.Replace ('\\', '/');
					onRename?.Invoke (entry.FullName, name);
					entry.Rename (name);
					modified = true;
				}
			}
			if (modified) {
				Flush ();
			}
		}

		public void Dispose ()
		{
			Dispose(true);
			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose(bool disposing) {
			if (disposing) {
				if (zip != null) {
					zip.Close ();
					zip.Dispose ();
					zip = null;
				}
			}	
		}
	}
}
