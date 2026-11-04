using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.ProfessionUI
{
    // Explicit local references keep original packages and material textures in
    // Windows builds without interpreting source paths as runtime file paths.
    public sealed class PartnerNativeBattleAssetCatalog : ScriptableObject
    {
        public const string ResourcePath = "Combat/NativePartnerAssets/catalog";
        [Serializable] public sealed class Binding { public string File; public Object Asset; }
        [SerializeField] private TextAsset inventory;
        [SerializeField] private Shader shader;
        [SerializeField] private PartnerNativeBattleAnimatorCatalog controllers;
        [SerializeField] private Binding[] bindings = Array.Empty<Binding>();
        private Dictionary<string, Object> files;
        private Dictionary<int, Dictionary<string, object>> fighters;

        public Shader Shader => shader ? shader : throw new InvalidDataException("Original partner shader is unbound.");
        public PartnerNativeBattleAnimatorCatalog Controllers => controllers
            ? controllers : throw new InvalidDataException("Original partner Animator catalog is unbound.");
        public IEnumerable<int> NpcIds { get { Initialize(); return fighters.Keys; } }
        public static PartnerNativeBattleAssetCatalog Load() => Resources.Load<PartnerNativeBattleAssetCatalog>(ResourcePath)
            ?? throw new InvalidDataException("Original partner battle assets are unbound.");

        public Dictionary<string, object> Find(int npcId)
        {
            Initialize();
            return fighters.TryGetValue(npcId, out var row) ? row : null;
        }

        public T Resolve<T>(string file) where T : Object
        {
            Initialize();
            return files.TryGetValue(file, out Object asset) && asset is T typed && typed
                ? typed : throw new InvalidDataException("Original partner battle resource is unbound: " + file);
        }

        private void Initialize()
        {
            if (files != null) return;
            if (!inventory || !shader || !controllers) throw new InvalidDataException("Original partner battle catalog is incomplete.");
            var source = (Dictionary<string, object>)OfficialEquipmentJson.Parse(inventory.text);
            if (!Equals(source["manifest"], "884_156692")) throw new InvalidDataException("Original partner battle version differs.");
            var nextFiles = new Dictionary<string, Object>(StringComparer.Ordinal);
            foreach (Binding binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.File) || !binding.Asset || nextFiles.ContainsKey(binding.File))
                    throw new InvalidDataException("Original partner battle resource binding is invalid.");
                nextFiles.Add(binding.File, binding.Asset);
            }
            var nextFighters = new Dictionary<int, Dictionary<string, object>>();
            foreach (object value in (List<object>)source["fighters"])
            {
                var row = (Dictionary<string, object>)value;
                int id = Convert.ToInt32(row["npcId"]);
                if (id <= 0 || nextFighters.ContainsKey(id) || !controllers.Find(id))
                    throw new InvalidDataException("Original partner battle identity or controller is invalid.");
                nextFighters.Add(id, row);
            }
            if (nextFighters.Count != 34) throw new InvalidDataException("Original complete-skeleton partner inventory differs.");
            fighters = nextFighters; files = nextFiles;
        }
    }
}
