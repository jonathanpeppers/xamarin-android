using System.IO;
using System.Runtime.CompilerServices;
using Mono.Cecil;

namespace internalsvisibleto
{
	class Program
	{
		const string PublicKey =
			"00240000048000009400000006020000" +
			"00240000525341310004000011000000" +
			"438AC2A5ACFBF16CBD2B2B47A62762F2" +
			"73DF9CB2795CECCDF77D10BF508E69E7" +
			"A362EA7A45455BBF3AC955E1F2E2814F" +
			"144E5D817EFC4C6502CC012DF3107833" +
			"48304E3AE38573C6D658C234025821FD" +
			"A87A0BE8A0D504DF564E2C93B2B87892" +
			"5F42503E9D54DFEF9F9586D9E6F38A30" +
			"5769587B1DE01F6C0410328B2C9733DB";

		static readonly string InternalsVisibleTo = $"Mono.Android, PublicKey={PublicKey}";

		static void Main (string [] args)
		{
			var dir = Path.GetDirectoryName (typeof (Program).Assembly.Location);
			dir = Path.GetFullPath (Path.Combine (dir, "..", "..", "..", ".."));
			var path = Path.Combine (dir, "bin", "Debug", "lib", "xamarin.android", "xbuild-frameworks", "MonoAndroid", "v1.0", "mscorlib.dll");
			var rp = new ReaderParameters {
				InMemory = true,
			};
			using (var assembly = AssemblyDefinition.ReadAssembly (path, rp)) {
				var ctor = assembly.MainModule.ImportReference (typeof (InternalsVisibleToAttribute).GetConstructor (new [] { typeof (string) }));
				var customAttribute = new CustomAttribute (ctor);
				customAttribute.ConstructorArguments.Add (new CustomAttributeArgument (assembly.MainModule.TypeSystem.String, InternalsVisibleTo));
				assembly.CustomAttributes.Add (customAttribute);
				assembly.Write (path);
			}
		}
	}
}
