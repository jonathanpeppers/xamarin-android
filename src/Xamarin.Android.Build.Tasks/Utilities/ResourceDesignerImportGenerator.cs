using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class ResourceDesignerImportGenerator
	{
		CodeTypeDeclaration primary;
		CodeMemberMethod method;
		string primary_name;
		HashSet<string> resourceFields = new HashSet<string> ();
		TaskLoggingHelper Log;

		string CreateIdentifier (string type, string field)
		{
			return $"{primary_name}.{type}.{field}";
		}

		public ResourceDesignerImportGenerator (string ns, CodeTypeDeclaration applicationResourceDesigner, TaskLoggingHelper log)
		{
			primary = applicationResourceDesigner;
			primary_name = ns + (ns.Length > 0 ? "." : "") + primary.Name;
			Log = log;

			method = new CodeMemberMethod () { Name = "UpdateIdValues", Attributes = MemberAttributes.Public | MemberAttributes.Static };
			primary.Members.Add (method);

			foreach (CodeTypeMember type in primary.Members) {
				var decl = type as CodeTypeDeclaration;
				if (decl == null)
					continue;
				foreach (CodeTypeMember field in decl.Members) 
					resourceFields.Add (CreateIdentifier (type.Name, field.Name));
			}
		}

		public void CreateImportMethod (MetadataReader reader)
		{
			var assembly = reader.GetAssemblyDefinition ();
			string resourceDesignerName = null;
			foreach (var handle in assembly.GetCustomAttributes ()) {
				var attribute = reader.GetCustomAttribute (handle);
				if (reader.GetCustomAttributeFullName (attribute) == "Android.Runtime.ResourceDesignerAttribute") {
					var value = attribute.GetCustomAttributeArguments ();
					// application resource IDs are constants, cannot merge.
					if ((bool)value.NamedArguments.First (p => p.Name == "IsApplication").Value)
						return;
					if (value.FixedArguments.Length > 0) {
						resourceDesignerName = value.FixedArguments [0].Value as string;
						break;
					}
				}
			}
			if (string.IsNullOrEmpty (resourceDesignerName))
				return;

			var otherTypes = new List<Type> ();
			foreach (var handle in reader.TypeDefinitions) {
				var type = new Type (reader, handle);
				if (type.FullName == resourceDesignerName) {
					var nested = type.Definition.GetNestedTypes ();
					if (nested.Length > 0)
						CreateImportFor (reader, true, nested.Select (n => new Type (reader, n, type)));
				} else if (type.FullName.StartsWith (resourceDesignerName, StringComparison.Ordinal)) {
					// F# has no nested types, so we need special care.
					otherTypes.Add (type);
				}
			}

			CreateImportFor (reader, false, otherTypes);
		}

		void CreateImportFor (MetadataReader reader, bool isNestedSrc, IEnumerable<Type> types)
		{
			foreach (var type in types) {
				// If the library was written in F#, those resource ID classes are not nested but rather combined with '_'.
				var srcClassRef = new CodeTypeReferenceExpression (
					new CodeTypeReference (primary_name + (isNestedSrc ? '.' : '_') + type.Name, CodeTypeReferenceOptions.GlobalReference));
				// destination language may not support nested types, but they should take care of such types by themselves.
				var dstClassRef = new CodeTypeReferenceExpression (
					new CodeTypeReference (type.FullName.Replace ('/', '.'), CodeTypeReferenceOptions.GlobalReference));
				foreach (var handle in type.Definition.GetFields ()) {
					var field = reader.GetFieldDefinition (handle);
					var name = reader.GetString (field.Name);
					var dstField = new CodeFieldReferenceExpression (dstClassRef, name);
					var srcField = new CodeFieldReferenceExpression (srcClassRef, name);
					var fieldName = CreateIdentifier (type.Name, name);
					if (!resourceFields.Contains (fieldName)) {
						Log.LogWarning (subcategory: null,
							warningCode: "XA0106",
							helpKeyword: null,
							file: null,
							lineNumber: 0,
							columnNumber: 0,
							endLineNumber: 0,
							endColumnNumber: 0,
							message: $"Skipping {fieldName}. Please check that your Nuget Package versions are compatible."
						);
						continue;
					}
					// This simply assigns field regardless of whether it is int or int[].
					method.Statements.Add (new CodeAssignStatement (dstField, srcField));
				}
			}
		}

		class Type
		{
			public Type (MetadataReader reader, TypeDefinitionHandle handle, Type parent = null)
			{
				Definition = reader.GetTypeDefinition (handle);
				Name = reader.GetString (Definition.Name);
				if (parent != null) {
					Name = parent.Name + "." + Name;
				}
				FullName = reader.GetString (Definition.Namespace) + "." + Name;
			}

			public TypeDefinition Definition { get; private set; }
			public string Name { get; private set; }
			public string FullName { get; private set; }
		}
	}
}

