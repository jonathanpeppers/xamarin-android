using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// A replacement for DirectoryAssemblyResolver, using System.Reflection.Metadata
	/// </summary>
	public class MetadataResolver : IDisposable
	{
		readonly Dictionary<string, PEReader> readerCache = new Dictionary<string, PEReader> ();
		readonly List<string> searchDirectories = new List<string> ();
		readonly Dictionary<Tuple<string, string>, TypeInfo> typeCache = new Dictionary<Tuple<string, string>, TypeInfo> ();

		public MetadataReader GetAssemblyReader (string assemblyName)
		{
			var assemblyPath = Resolve (assemblyName);
			if (!readerCache.TryGetValue (assemblyPath, out PEReader reader)) {
				readerCache.Add (assemblyPath, reader = new PEReader (File.OpenRead (assemblyPath)));
			}
			return reader.GetMetadataReader ();
		}

		public void AddSearchDirectory (string directory)
		{
			directory = Path.GetFullPath (directory);
			if (!searchDirectories.Contains (directory))
				searchDirectories.Add (directory);
		}

		public string Resolve (string assemblyName)
		{
			string assemblyPath = assemblyName;
			if (!assemblyPath.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
				assemblyPath += ".dll";
			}
			if (File.Exists (assemblyPath)) {
				return assemblyPath;
			}
			foreach (var dir in searchDirectories) {
				var path = Path.Combine (dir, assemblyPath);
				if (File.Exists (path))
					return path;
			}

			throw new FileNotFoundException ($"Could not load assembly '{assemblyName}'.", assemblyName);
		}

		public IEnumerable<TypeInfo> EnumerateTypes (string assemblyName)
		{
			var reader = GetAssemblyReader (assemblyName);
			var assemblyDefinition = reader.GetAssemblyDefinition ();
			var resolvedAssemblyName = reader.GetString (assemblyDefinition.Name);
			foreach (var handle in reader.TypeDefinitions) {
				yield return ToTypeInfo (reader, resolvedAssemblyName, handle);
			}
		}

		public IEnumerable<TypeInfo> EnumerateBaseTypes (TypeInfo typeInfo)
		{
			var handle = typeInfo.TypeDefinition.BaseType;
			while (!handle.IsNil) {
				switch (handle.Kind) {
					case HandleKind.TypeReference:
						yield return typeInfo = ResolveTypeReference (typeInfo, (TypeReferenceHandle) handle);
						break;
					case HandleKind.TypeDefinition:
						yield return typeInfo = ToTypeInfo (typeInfo.Reader, typeInfo.AssemblyName, (TypeDefinitionHandle) handle);
						break;
					default:
						break;
				}
				handle = typeInfo.TypeDefinition.BaseType;
			}
		}

		TypeInfo ResolveTypeReference (TypeInfo inheritingType, TypeReferenceHandle handle)
		{
			var reader = inheritingType.Reader;
			var typeReference = reader.GetTypeReference (handle);
			var assemblyReference = reader.GetAssemblyReference ((AssemblyReferenceHandle) typeReference.ResolutionScope);
			var assemblyName = reader.GetString (assemblyReference.Name);
			var typeName = reader.GetString (typeReference.Namespace) + "." + reader.GetString (typeReference.Name);
			if (typeCache.TryGetValue (new Tuple<string, string> (assemblyName, typeName), out TypeInfo cachedType)) {
				return cachedType;
			}
			TypeInfo found = null;
			foreach (var typeInfo in EnumerateTypes (assemblyName)) {
				if (typeInfo.FullName == typeName) {
					// Store the result, but continue caching the entire assembly
					found = typeInfo;
				}
			}
			if (found == null)
				throw new Exception ($"Unable to find base type of '{assemblyName}, {typeName}' from type '{inheritingType}'.");
			return found;
		}

		TypeInfo ToTypeInfo (MetadataReader reader, string resolvedAssemblyName, TypeDefinitionHandle handle)
		{
			var typeDefinition = reader.GetTypeDefinition (handle);
			var typeInfo = new TypeInfo (typeDefinition, reader, resolvedAssemblyName);
			typeCache [new Tuple<string, string> (resolvedAssemblyName, typeInfo.FullName)] = typeInfo;
			return typeInfo;
		}

		public bool IsApplicationOrInstrumentation (TypeInfo typeInfo)
		{
			foreach (var type in EnumerateBaseTypes (typeInfo)) {
				if (type.FullName == "Android.App.Application" ||
					type.FullName == "Android.App.Instrumentation") {
					return true;
				}
			}
			return false;
		}

		public void Dispose ()
		{
			foreach (var provider in readerCache.Values) {
				provider.Dispose ();
			}
			readerCache.Clear ();
		}
	}

	public class TypeInfo
	{
		internal readonly TypeDefinition TypeDefinition;
		internal readonly MetadataReader Reader; 

		public TypeInfo (TypeDefinition type, MetadataReader reader, string assemblyName)
		{
			TypeDefinition = type;
			Reader = reader;
			Name = reader.GetString (type.Name);
			Namespace = reader.GetString (type.Namespace);
			FullName = Namespace + "." + Name;
			AssemblyName = assemblyName;
		}

		public string Name { get; private set; }

		public string Namespace { get; private set; }

		public string FullName { get; private set; }

		public string AssemblyName { get; private set; }

		public override string ToString ()
		{
			return $"{AssemblyName}, {FullName}";
		}
	}
}
