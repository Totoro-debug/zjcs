using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.EntityIndex;
using Zjcs.Offline.World.NativeMap14SharedEntities;
namespace Zjcs.Offline.World.NativeMap14EntityLoading
{
    public sealed class NativeMap14EntityIndexContext : IEntityIndexContext
    {
        [Serializable] private sealed class Binding
        {
            public string OriginalPath;
            public string Resource;
            public string Sha256;
            public int Directories;
            public int Identifiers;
            public string DependencyResource;
            public string DependencySha256;
            public string DependencyModuleId;
        }
        private readonly Binding binding;
        private readonly MethodInfo decode;
        public int DirectoryCount => binding.Directories;
        public int IdentifierCount => binding.Identifiers;
        public NativeMap14EntityIndexContext(NativeMap14SharedAssemblies shared)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            var asset = Resources.Load<TextAsset>("Map14/EntityLoading/index-binding");
            if (!asset) throw new InvalidOperationException("Original entity index binding is absent.");
            binding = JsonUtility.FromJson<Binding>(asset.text);
            var dependency = Resources.Load<TextAsset>(binding.DependencyResource);
            if (!dependency) throw new InvalidOperationException("Original JSON platform dependency is absent.");
            NativeMap14SharedAssemblies.Verify(dependency.bytes, binding.DependencySha256);
            var existing = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == "LiteDB").ToArray();
            if (existing.Any(assembly => assembly.ManifestModule.ModuleVersionId.ToString() != binding.DependencyModuleId))
                throw new InvalidOperationException("Another LiteDB platform dependency version is loaded.");
            var loaded = existing.FirstOrDefault() ?? Assembly.Load(dependency.bytes);
            if (loaded.FullName != "LiteDB, Version=5.0.20.0, Culture=neutral, PublicKeyToken=4ee40123013c9f27" ||
                loaded.ManifestModule.ModuleVersionId.ToString() != binding.DependencyModuleId)
                throw new InvalidOperationException("JSON platform dependency identity differs.");
            decode = shared.CommonType("JsonUtils").GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "FromJson" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(Dictionary<string, int[]>));
        }
        public string LoadText(string originalPath)
        {
            if (originalPath != binding.OriginalPath) throw new InvalidOperationException("Unbound original entity index path: " + originalPath);
            var asset = Resources.Load<TextAsset>(binding.Resource);
            if (!asset) throw new InvalidOperationException("Original entity index payload is absent.");
            NativeMap14SharedAssemblies.Verify(asset.bytes, binding.Sha256);
            return asset.text;
        }
        public Dictionary<string, int[]> Decode(string context) =>
            (Dictionary<string, int[]>)decode.Invoke(null, new object[] { context });
    }
}
