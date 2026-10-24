using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class MaterialGatheringSourceManifest
    {
        public string manifest;
        public string output_sha256;
    }

    [Serializable]
    public sealed class MaterialGatheringCatalogData
    {
        public string manifest;
        public float damageFloat;
        public int maximumActionCount;
        public MaterialGatheringDungeon[] dungeons;
        public MaterialGatheringMaterial[] materials;
        public MaterialGatheringRewardRule[] rewards;
        public MaterialGatheringLevelNumber[] levelNumbers;
        public MaterialGatheringStageLocation[] stageLocations;
    }

    [Serializable]
    public sealed class MaterialGatheringDungeon
    {
        public int Id, Type, GenerateMaterialRuleId, CostItemId, AttackProp, CritRateProp, CritPowerProp;
        public int AddtionAwardProp;
        public long CostCount;
        public int[] UnlockStages, NewbieRewardMaterials;
    }

    [Serializable]
    public sealed class MaterialGatheringMaterial
    {
        public int ClassId, Durability;
        public bool CanCount, NeedCost;
        public int[] AttackRewardIds, BreakRewardIds;
    }

    [Serializable]
    public sealed class MaterialGatheringRewardRule
    {
        public int Id;
        public float Percent;
        public int[] IdList, WeightList, SubRankCountId, LevelCountId;
        public int[] IndexMininum;
        public float[] CountList;
    }

    [Serializable]
    public sealed class MaterialGatheringLevelNumber
    {
        public int Id;
        public float[] Nums;
    }

    [Serializable]
    public sealed class MaterialGatheringStageLocation
    {
        public int StageId, MapClassId, Region;
    }

    public sealed class OfficialMaterialGatheringCatalog
    {
        public const string ExpectedManifest = "871_156598";
        public const string DefaultResourcePath = "Official/Progression/material-gathering";
        public const string DefaultSourceManifestResourcePath =
            "Official/Progression/material-gathering-source-manifest";

        private readonly Dictionary<int, MaterialGatheringDungeon> dungeons;
        private readonly Dictionary<int, MaterialGatheringMaterial> materials;
        private readonly Dictionary<int, MaterialGatheringRewardRule> rewards;
        private readonly Dictionary<int, MaterialGatheringLevelNumber> levels;
        private readonly Dictionary<int, MaterialGatheringStageLocation[]> stageLocations;

        private OfficialMaterialGatheringCatalog(MaterialGatheringCatalogData data)
        {
            DamageFloat = data.damageFloat;
            MaximumActionCount = data.maximumActionCount;
            dungeons = Index(data.dungeons, row => row.Type, "dungeon");
            materials = Index(data.materials, row => row.ClassId, "material");
            rewards = Index(data.rewards, row => row.Id, "reward");
            levels = Index(data.levelNumbers, row => row.Id, "level-number");
            stageLocations = (data.stageLocations ?? Array.Empty<MaterialGatheringStageLocation>())
                .Where(row => row != null && row.StageId > 0 && row.MapClassId > 0 && row.Region >= 0)
                .GroupBy(row => row.StageId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            if (float.IsNaN(DamageFloat) || float.IsInfinity(DamageFloat)
                || DamageFloat < 0f || DamageFloat >= 1f
                || MaximumActionCount <= 0 || MaximumActionCount > 64 || dungeons.Count != 4
                || materials.Count < 17 || rewards.Count == 0)
                throw new FormatException("Official material-gathering catalog is incomplete.");
            foreach (MaterialGatheringDungeon dungeon in dungeons.Values)
            {
                if (dungeon.Id <= 0 || dungeon.GenerateMaterialRuleId <= 0 || dungeon.CostItemId <= 0
                    || dungeon.CostCount <= 0 || dungeon.UnlockStages == null || dungeon.UnlockStages.Length == 0
                    || dungeon.NewbieRewardMaterials == null || dungeon.NewbieRewardMaterials.Length == 0)
                    throw new FormatException("Official material-gathering dungeon row is invalid.");
                foreach (int materialId in dungeon.NewbieRewardMaterials)
                    if (!materials.ContainsKey(materialId))
                        throw new FormatException("Official newbie material is missing: " + materialId);
            }
            foreach (MaterialGatheringMaterial material in materials.Values)
            {
                ValidateRewardIds(material.AttackRewardIds);
                ValidateRewardIds(material.BreakRewardIds);
                if (material.ClassId <= 0 || material.Durability < 0 || material.NeedCost && material.Durability <= 0)
                    throw new FormatException("Official material row is invalid: " + material.ClassId);
            }
            foreach (MaterialGatheringDungeon dungeon in dungeons.Values)
            {
                if (!rewards.TryGetValue(dungeon.GenerateMaterialRuleId, out MaterialGatheringRewardRule rule)
                    || rule.IdList == null || rule.WeightList == null || rule.IdList.Length == 0
                    || rule.IdList.Length != rule.WeightList.Length)
                    throw new FormatException("Official material-generation rule is invalid: " + dungeon.GenerateMaterialRuleId);
                for (int index = 0; index < rule.IdList.Length; index++)
                    if (!materials.ContainsKey(rule.IdList[index]) || rule.WeightList[index] < 0)
                        throw new FormatException("Official material-generation candidate is invalid: " + rule.IdList[index]);
            }
        }

        public float DamageFloat { get; }
        public int MaximumActionCount { get; }
        public IReadOnlyCollection<MaterialGatheringDungeon> Dungeons => dungeons.Values;

        public static OfficialMaterialGatheringCatalog LoadDefault()
        {
            TextAsset payload = Resources.Load<TextAsset>(DefaultResourcePath);
            TextAsset manifest = Resources.Load<TextAsset>(DefaultSourceManifestResourcePath);
            if (payload == null || manifest == null)
                throw new InvalidOperationException("Official material-gathering resources are not installed.");
            return Load(payload.text, manifest.text);
        }

        public static OfficialMaterialGatheringCatalog Load(string json, string sourceManifestJson)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(sourceManifestJson))
                throw new ArgumentException("Official material-gathering payload and manifest are required.");
            MaterialGatheringSourceManifest manifest =
                JsonUtility.FromJson<MaterialGatheringSourceManifest>(sourceManifestJson);
            if (manifest == null || manifest.manifest != ExpectedManifest)
                throw new InvalidOperationException("Material-gathering data is not from the active official manifest.");
            string hash;
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            if (!string.Equals(hash, manifest.output_sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Official material-gathering payload hash is invalid.");
            MaterialGatheringCatalogData data = JsonUtility.FromJson<MaterialGatheringCatalogData>(json);
            if (data == null || data.manifest != ExpectedManifest || data.dungeons == null
                || data.materials == null || data.rewards == null || data.levelNumbers == null)
                throw new FormatException("Official material-gathering payload is invalid.");
            return new OfficialMaterialGatheringCatalog(data);
        }

        public bool TryGetDungeon(int type, out MaterialGatheringDungeon row) => dungeons.TryGetValue(type, out row);
        public bool TryGetMaterial(int id, out MaterialGatheringMaterial row) => materials.TryGetValue(id, out row);
        public bool TryGetReward(int id, out MaterialGatheringRewardRule row) => rewards.TryGetValue(id, out row);

        public IReadOnlyList<MaterialGatheringStageLocation> StageLocations(int stageId)
        {
            return stageLocations.TryGetValue(stageId, out MaterialGatheringStageLocation[] rows)
                ? rows : Array.Empty<MaterialGatheringStageLocation>();
        }

        public float LevelMultiplier(int id, int subRank)
        {
            if (id <= 0) return 1f;
            if (!levels.TryGetValue(id, out MaterialGatheringLevelNumber row) || row.Nums == null || row.Nums.Length == 0)
                throw new InvalidOperationException("Official material-gathering level row is missing: " + id);
            return row.Nums[Math.Min(Math.Max(0, subRank), row.Nums.Length - 1)];
        }

        private void ValidateRewardIds(int[] ids)
        {
            foreach (int id in ids ?? Array.Empty<int>())
                if (!rewards.ContainsKey(id)) throw new FormatException("Official material reward is missing: " + id);
        }

        private static Dictionary<int, T> Index<T>(T[] rows, Func<T, int> id, string kind)
        {
            var result = new Dictionary<int, T>();
            foreach (T row in rows ?? Array.Empty<T>())
                if (row == null || id(row) <= 0 || !result.TryAdd(id(row), row))
                    throw new FormatException("Invalid official material-gathering " + kind + " rows.");
            return result;
        }
    }
}
