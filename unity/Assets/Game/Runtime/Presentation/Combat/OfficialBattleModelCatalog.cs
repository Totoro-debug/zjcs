using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Spine.Unity;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleModelBinding
    {
        public string key;
        public int monsterId;
        public string skin;
        public float scale = 1f;
        public OfficialBattleAnimationAlias[] animations = Array.Empty<OfficialBattleAnimationAlias>();
        public SkeletonDataAsset skeleton;
    }

    [Serializable]
    public sealed class OfficialBattleAnimationAlias
    {
        public string name;
        public string spineName;
    }

    [Serializable]
    public sealed class OfficialBattleModelSourceManifest
    {
        public string manifest;
        public string source_path;
        public string source_sha256;
        public string output_path;
        public string output_sha256;
    }

    [Serializable]
    public sealed class OfficialBattleModelIdentitySourceManifest
    {
        public string manifest;
        public string source_sha256;
        public string output_path;
        public string output_sha256;
    }

    [Serializable]
    public sealed class OfficialBattleMonsterModel
    {
        public int monster_id;
        public string key;
    }

    [Serializable]
    public sealed class OfficialBattleModelIdentityData
    {
        public string manifest;
        public string model_source_sha256;
        public string spine_source_manifest_sha256;
        public OfficialBattleMonsterModel[] monster_models;
    }

    [CreateAssetMenu(menuName = "Zjcs/Combat/Official Battle Model Catalog")]
    public sealed class OfficialBattleModelCatalog : ScriptableObject
    {
        public const string DefaultResourcePath = "Combat/OfficialBattleModelCatalog";

        [SerializeField] private TextAsset sourceModels;
        [SerializeField] private TextAsset sourceManifest;
        [SerializeField] private TextAsset identities;
        [SerializeField] private TextAsset identitiesManifest;
        [SerializeField] private OfficialBattleModelBinding[] bindings = Array.Empty<OfficialBattleModelBinding>();
        private Dictionary<string, OfficialBattleModelBinding> byKey;
        private Dictionary<int, string> monsterKeys;
        private Dictionary<int, string> npcKeys;

        public static OfficialBattleModelCatalog LoadDefault()
        {
            OfficialBattleModelCatalog catalog = Resources.Load<OfficialBattleModelCatalog>(DefaultResourcePath);
            if (catalog == null)
                throw new InvalidOperationException("Official battle model catalog is not installed.");
            catalog.ValidateSource();
            catalog.BuildIndex();
            return catalog;
        }

        public OfficialBattleModelBinding Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (byKey == null) BuildIndex();
            return byKey.TryGetValue(key, out OfficialBattleModelBinding binding) ? binding : null;
        }

        public int Count => bindings == null ? 0 : bindings.Length;

        public bool HasNpcBattleAnimations(int npcClassId)
        {
            string key = ResolveModelKey(new FightBattleUnitState { NpcClassId = npcClassId });
            OfficialBattleModelBinding binding = Find(key);
            return binding != null && binding.animations.Any(value => value.name == "idle_01")
                && binding.animations.Any(value => value.name == "run_01");
        }

        public string ResolveModelKey(FightBattleUnitState unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (unit.MonsterId > 0)
            {
                if (monsterKeys == null) BuildMonsterIndex();
                if (monsterKeys.TryGetValue(unit.MonsterId, out string key)) return key;
                if (unit.MonsterTemplateId > 0
                    && monsterKeys.TryGetValue(unit.MonsterTemplateId, out key)) return key;
            }
            if (unit.NpcClassId > 0)
            {
                if (npcKeys == null) BuildIndex();
                if (npcKeys.TryGetValue(unit.NpcClassId, out string key)) return key;
            }
            return null;
        }

        private void BuildIndex()
        {
            byKey = new Dictionary<string, OfficialBattleModelBinding>(StringComparer.Ordinal);
            if (bindings == null) throw new InvalidOperationException("Official battle model bindings are missing.");
            foreach (OfficialBattleModelBinding binding in bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.key) || binding.skeleton == null
                    || byKey.ContainsKey(binding.key))
                    throw new InvalidOperationException("Official battle model catalog contains an invalid binding.");
                if (binding.animations == null || binding.animations.Length == 0 || binding.scale <= 0)
                    throw new InvalidOperationException("Official battle model animation metadata is incomplete: " + binding.key);
                foreach (OfficialBattleAnimationAlias animation in binding.animations)
                    if (animation == null || string.IsNullOrEmpty(animation.name) || string.IsNullOrEmpty(animation.spineName))
                        throw new InvalidOperationException("Official battle animation binding is invalid: " + binding.key);
                byKey.Add(binding.key, binding);
            }
            npcKeys = new Dictionary<int, string>();
            foreach (OfficialBattleModelBinding binding in bindings)
            {
                int marker = binding.key.LastIndexOf("body_", StringComparison.Ordinal);
                if (marker < 0 || !int.TryParse(binding.key.Substring(marker + 5), out int npcClassId)
                    || npcClassId <= 0) continue;
                npcKeys.Add(npcClassId, binding.key);
            }
            BuildMonsterIndex();
        }

        private void BuildMonsterIndex()
        {
            if (identities == null || identitiesManifest == null)
                throw new InvalidOperationException("Official battle monster model identity data is not bound.");
            OfficialBattleModelIdentitySourceManifest manifest = JsonUtility.FromJson<OfficialBattleModelIdentitySourceManifest>(identitiesManifest.text);
            string identityHash = Sha256(Encoding.UTF8.GetBytes(identities.text));
            if (manifest == null || manifest.manifest != "854_156383"
                || manifest.output_path != "Assets/Official/Combat/Presentation/model-identities.json"
                || manifest.output_sha256 != identityHash)
                throw new InvalidOperationException("Official battle monster identity hash does not match.");
            OfficialBattleModelIdentityData data = JsonUtility.FromJson<OfficialBattleModelIdentityData>(identities.text);
            if (data == null || data.manifest != "854_156383" || data.monster_models == null
                || data.monster_models.Length != 343 || data.model_source_sha256 != ReadModelSourceHash()
                || data.spine_source_manifest_sha256 != manifest.source_sha256)
                throw new FormatException("Official battle monster identity catalog is incomplete.");
            monsterKeys = new Dictionary<int, string>();
            foreach (OfficialBattleMonsterModel row in data.monster_models)
            {
                if (row == null || row.monster_id <= 0 || string.IsNullOrEmpty(row.key)
                    || !byKey.ContainsKey(row.key) || monsterKeys.ContainsKey(row.monster_id))
                    throw new InvalidOperationException("Official battle monster model identity is invalid.");
                monsterKeys.Add(row.monster_id, row.key);
            }
        }

        private string ReadModelSourceHash()
        {
            OfficialBattleModelSourceManifest source = JsonUtility.FromJson<OfficialBattleModelSourceManifest>(sourceManifest.text);
            return source == null ? null : source.source_sha256;
        }

        private void ValidateSource()
        {
            if (sourceModels == null || sourceManifest == null)
                throw new InvalidOperationException("Official battle model source files are not bound.");
            OfficialBattleModelSourceManifest manifest = JsonUtility.FromJson<OfficialBattleModelSourceManifest>(sourceManifest.text);
            if (manifest == null || manifest.manifest != "854_156383"
                || manifest.source_path != "assets/official/battle-presentation/models.json"
                || manifest.output_path != "Assets/Official/Combat/Presentation/models.json"
                || manifest.source_sha256 != Sha256(sourceModels.bytes)
                || manifest.output_sha256 != manifest.source_sha256)
                throw new InvalidOperationException("Official battle model source hash does not match.");
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var value = new StringBuilder(64);
                foreach (byte item in sha.ComputeHash(bytes)) value.Append(item.ToString("x2"));
                return value.ToString();
            }
        }
    }
}
