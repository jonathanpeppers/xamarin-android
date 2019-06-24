using Mono.Cecil;
using Mono.Linker;
using Mono.Linker.Steps;

namespace MonoDroid.Tuner
{
	public class RemoveRegisterAttribute : BaseStep
	{
		const string RegisterAttribute = "Android.Runtime.RegisterAttribute";

		protected override void ProcessAssembly (AssemblyDefinition assembly)
		{
			if (!Annotations.HasAction (assembly))
				return;
			var action = Annotations.GetAction (assembly);
			if (action == AssemblyAction.Skip || action == AssemblyAction.Delete)
				return;

			bool assembly_modified = false;
			foreach (var type in assembly.MainModule.Types) {
				assembly_modified |= ProcessType (type);
			}
			if (assembly_modified && action == AssemblyAction.Copy) {
				Annotations.SetAction (assembly, AssemblyAction.Save);
			}
		}

		static bool ProcessType (TypeDefinition type)
		{
			bool assembly_modified = false;
			if (type.HasFields) {
				foreach (var field in type.Fields) {
					assembly_modified |= ProcessAttributeProvider (field);
				}
			}
			if (type.HasMethods) {
				foreach (var method in type.Methods) {
					assembly_modified |= ProcessAttributeProvider (method);
				}
			}
			return assembly_modified;
		}

		static bool ProcessAttributeProvider (ICustomAttributeProvider provider)
		{
			if (!provider.HasCustomAttributes)
				return false;

			bool assembly_modified = false;
			for (int i = 0; i < provider.CustomAttributes.Count; i++) {
				if (!IsRegisterAttribute (provider.CustomAttributes [i]))
					continue;

				assembly_modified = true;
				provider.CustomAttributes.RemoveAt (i--);
			}
			return assembly_modified;
		}

		static bool IsRegisterAttribute (CustomAttribute attribute)
		{
			return attribute.Constructor.DeclaringType.FullName == RegisterAttribute;
		}
	}
}
