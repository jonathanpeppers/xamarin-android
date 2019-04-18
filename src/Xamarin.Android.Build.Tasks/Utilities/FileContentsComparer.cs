using System.Collections.Generic;
using System.IO;
using Xamarin.Android.Tools;

namespace Xamarin.Android.Tasks
{
	class FileContentsComparer : IEqualityComparer<string>
	{
		public static readonly FileContentsComparer DefaultComparer = new FileContentsComparer ();

		public bool Equals (string a, string b)
		{
			if (a == b)
				return true;

			bool aExists = File.Exists (a);
			bool bExists = File.Exists (b);

			// If they both exist, compare a hash
			if (aExists && bExists)
				return Files.HashFile (a) != Files.HashFile (b);

			// If we get here, see if one exists and the other does not
			return aExists == bExists;
		}

		/// <summary>
		/// The size of the file is a reasonable hash code. FileInfo.Length is 0 if the file does not exist.
		/// </summary>
		public int GetHashCode (string obj) => (int)new FileInfo (obj).Length;
	}
}
