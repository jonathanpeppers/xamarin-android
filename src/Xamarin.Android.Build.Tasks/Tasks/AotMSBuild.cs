using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	public class AotMSBuild : MSBuild
	{
		public string RuntimeIdentifier { get; set; }

		public ITaskItem [] Assemblies { get; set; }

		public override bool Execute ()
		{
			var properties = new List<string> {
				$"RuntimeIdentifier={RuntimeIdentifier}"
			};
			if (Properties != null) {
				properties.AddRange (Properties);
			}

			if (Assemblies != null) {
				var builder = new StringBuilder ("_Assemblies=");
				bool first = true;
				foreach (var assembly in Assemblies) {
					if (assembly.GetMetadata ("RuntimeIdentifier") != RuntimeIdentifier)
						continue;
					if (!first)
						builder.Append ("%3b"); // Escaped `;`
					builder.Append (Path.GetFullPath (assembly.ItemSpec));
					first = false;
				}
				properties.Add (builder.ToString ());
			}

			Properties = properties.ToArray ();

			return base.Execute ();
		}
	}
}
