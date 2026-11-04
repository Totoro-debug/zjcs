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
        [Serializable] public sealed class Binding { public string File; public Object Asset; }
        [SerializeField] private TextAsset inventory;
        [SerializeField] private Shader shader;
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        private Dictionary<string, Object> files;
        private Dictionary<int, Dictionary<string, object>> models;
        public Shader Shader => shader ? shader : throw new InvalidDataException("Original monster shader is unbound.");
        public IEnumerable<int> EntityIds { get { Initialize(); return models.Keys; } }
        public static BattleNativeMonsterAssets Load() => Resources.Load<BattleNativeMonsterAssets>(ResourcePath)
            ?? throw new InvalidDataException("Original native monster assets are unbound.");
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
            if (!Equals(source["manifest"], "884_156692")) throw new InvalidDataException("Original monster version differs.");
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
            files = nextFiles; models = nextModels;
        }
    }
}
