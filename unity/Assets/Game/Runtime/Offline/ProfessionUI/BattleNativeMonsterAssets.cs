using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class BattleNativeMonsterAssets : ScriptableObject
    {
        public const string ResourcePath = "Combat/NativeMonsters/catalog";
        private const string SkillActorFolder = "Combat/NativeMonsters/SkillActors";
        [Serializable] public sealed class Binding { public string File; public Object Asset; }
        [Serializable] public sealed class ShaderBinding { public string Name; public Shader Asset; }
        [SerializeField] private TextAsset inventory;
        [SerializeField] private Shader shader;
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        [SerializeField] private ShaderBinding[] shaderBindings = Array.Empty<ShaderBinding>();
        [SerializeField] private string presentationResourceRoot;
        [SerializeField] private BattleNativeMonsterAssets replacesCommonCatalog;
        [SerializeField] private int[] replacesCommonEntities = Array.Empty<int>();
        private Dictionary<string, Object> files;
        private Dictionary<int, Dictionary<string, object>> models;
        private string manifest;
        public string SourceManifest { get { Initialize(); return manifest; } }
        public string PresentationResource(string name) => string.IsNullOrEmpty(presentationResourceRoot)
            ? null : presentationResourceRoot + "/" + name;
        public Shader Shader => shader ? shader : throw new InvalidDataException("Original monster shader is unbound.");
        public Shader ResolveShader(string name)
        {
            if (Shader.name == name) return Shader;
            foreach (var binding in shaderBindings)
                if (binding.Name == name && binding.Asset) return binding.Asset;
            throw new InvalidDataException("Original monster material shader is unbound: " + name);
        }
        public IEnumerable<int> EntityIds { get { Initialize(); return models.Keys; } }
        public static BattleNativeMonsterAssets Load() => Resources.Load<BattleNativeMonsterAssets>(ResourcePath)
            ?? throw new InvalidDataException("Original native monster assets are unbound.");
        public static BattleNativeMonsterAssets Locate(int entityId)
        {
            var common = Resources.Load<BattleNativeMonsterAssets>(ResourcePath);
            bool hasCommon = common && common.Find(entityId) != null;
            BattleNativeMonsterAssets match = null;
            foreach (var candidate in Resources.LoadAll<BattleNativeMonsterAssets>(SkillActorFolder))
            {
                if (candidate.Find(entityId) == null) continue;
                if (match) throw new InvalidDataException("Duplicate original skill actor binding: " + entityId);
                match = candidate;
            }
            if (!match) return hasCommon ? common : null;
            if (hasCommon && (match.replacesCommonCatalog != common
                || Array.IndexOf(match.replacesCommonEntities, entityId) < 0))
                throw new InvalidDataException("Original skill actor requires an explicit common binding replacement: " + entityId);
            return match;
        }
        public Dictionary<string, object> Find(int entityId)
        { Initialize(); return models.TryGetValue(entityId, out var row) ? row : null; }
        public T Resolve<T>(string file) where T : Object
        {
            Initialize();
            return files.TryGetValue(file, out var value) && value is T typed && typed
                ? typed : throw new InvalidDataException("Original monster resource is unbound: " + file);
        }
        private void Initialize()
        {
            if (files != null) return;
            if (!inventory || !shader) throw new InvalidDataException("Original monster catalog is incomplete.");
            var source = (Dictionary<string, object>)OfficialEquipmentJson.Parse(inventory.text);
            string nextManifest = source["manifest"] as string;
            if (string.IsNullOrWhiteSpace(nextManifest)) throw new InvalidDataException("Original monster source identity is missing.");
            var nextFiles = new Dictionary<string, Object>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.File) || !binding.Asset)
                    throw new InvalidDataException("Invalid original monster resource binding: " + binding?.File);
                nextFiles.Add(binding.File, binding.Asset);
            }
            var nextModels = new Dictionary<int, Dictionary<string, object>>();
            foreach (var value in (List<object>)source["models"])
            {
                var row = (Dictionary<string, object>)value;
                int id = Convert.ToInt32(row["entityId"]);
                if (id <= 0) throw new InvalidDataException("Invalid original monster identity.");
                nextModels.Add(id, row);
            }
            var replacements = new HashSet<int>();
            foreach (int id in replacesCommonEntities)
                if (!replacesCommonCatalog || replacesCommonCatalog == this || !nextModels.ContainsKey(id)
                    || replacesCommonCatalog != Resources.Load<BattleNativeMonsterAssets>(ResourcePath)
                    || !replacements.Add(id) || replacesCommonCatalog.Find(id) == null)
                    throw new InvalidDataException("Invalid original skill actor common binding replacement: " + id);
            manifest = nextManifest;
            files = nextFiles; models = nextModels;
        }
    }
}
