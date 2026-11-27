using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class TreasureRuleItemAmount
    {
        public int ItemId;
        public long Count;
    }

    [Serializable]
    public sealed class TreasureCommonMaterialSetting
    {
        public int Quality;
        public int ItemClassId;
        public int Item2PieceCount;
    }

    [Serializable]
    public sealed class TreasurePieceSplitSetting
    {
        public int Quality;
        public int GroupCount;
        public List<TreasureRuleItemAmount> Award = new List<TreasureRuleItemAmount>();
    }

    [Serializable]
    public sealed class TreasureRuleSettings
    {
        public int TreasureMaxSlotCount = 4;
        public int[] TreasureSlotElementTypes = Array.Empty<int>();
        public float TreasureSlotPropAddRate = 0.2f;
        public int TreasureMaxLevel = 999;
        public int TreasurePlanMaxCount = 8;
        public List<TreasureCommonMaterialSetting> CommonMaterial2Piece = new List<TreasureCommonMaterialSetting>();
        public List<TreasurePieceSplitSetting> PieceSplitSetting = new List<TreasurePieceSplitSetting>();
    }

    [Serializable]
    public sealed class TreasureLevelMaterialRule
    {
        public int Level;
        public List<TreasureRuleItemAmount> Materials = new List<TreasureRuleItemAmount>();
    }

    [Serializable]
    public sealed class TreasureUpgradeMaterialRule
    {
        public int Quality;
        public int Star;
        public int PieceCount;
        public int SpecialPieceCount;
        public List<TreasureRuleItemAmount> Materials = new List<TreasureRuleItemAmount>();
    }

    [Serializable]
    public sealed class TreasureBlessRule
    {
        public int Rank;
        public int BlessLevel;
        public List<TreasureRuleItemAmount> LevelCostItems = new List<TreasureRuleItemAmount>();
    }

    [Serializable]
    public sealed class TreasureRandomExchangeRule
    {
        public int ItemId;
        public int RequireCount;
        public int[] RewardRuleIds = Array.Empty<int>();
    }

    [Serializable]
    public sealed class TreasureSeasonLevelPropEntry
    {
        public int SeasonId;
        public int PropId;
    }

    [Serializable]
    public sealed class TreasureRuleConfig
    {
        public int ClassId;
        public int TreasurePieceId;
        public int StarMax;
        public int[] StarPropTypes = Array.Empty<int>();
        public int StarScalePropId;
        public int[] LevelPropTypes = Array.Empty<int>();
        public int[] EffectPropTypes = Array.Empty<int>();
        public List<TreasureSeasonLevelPropEntry> LevelPropId = new List<TreasureSeasonLevelPropEntry>();
        public bool ObtainedShow;
        public bool LimitUseCommonMaterial;
        public bool IsLimited;
        public string EffectType = string.Empty;
        public int EffectPerCount;
        public int EffectMaxHitCount;

        public int UpgradeMaxStar => StarMax - 1;
        public int InitialHitCount => string.Equals(EffectType, "TreasureEffectTask", StringComparison.Ordinal) ? 0 : 1;

        public int GetLevelPropId(int seasonId)
        {
            if (seasonId < 0) seasonId = 0;
            TreasureSeasonLevelPropEntry exact = LevelPropId.FirstOrDefault(e => e.SeasonId == seasonId);
            if (exact != null) return exact.PropId;
            Debug.LogError("treasure.csv classId: " + ClassId + ",seasonId: " + seasonId + " 无配置");
            return LevelPropId.Count == 0 ? 0 : LevelPropId[0].PropId;
        }
    }

    [Serializable]
    public sealed class TreasureSuitPropEntry
    {
        public int Condition;
        public int PropId;
        public int[] PropTypes = Array.Empty<int>();
    }

    [Serializable]
    public sealed class TreasureSuitRuleConfig
    {
        public int ClassId;
        public int[] Treasures = Array.Empty<int>();
        public List<TreasureSuitPropEntry> Props = new List<TreasureSuitPropEntry>();
    }

    [Serializable]
    public sealed class OfflineTreasureCatalog
    {
        public string Manifest;
        public TreasureRuleSettings Settings = new TreasureRuleSettings();
        public List<TreasureLevelMaterialRule> LevelUpMaterials = new List<TreasureLevelMaterialRule>();
        public List<TreasureUpgradeMaterialRule> UpgradeMaterials = new List<TreasureUpgradeMaterialRule>();
        public List<TreasureBlessRule> BlessInfos = new List<TreasureBlessRule>();
        public List<TreasureRandomExchangeRule> RandomExchanges = new List<TreasureRandomExchangeRule>();
        public List<TreasureRuleConfig> Treasures = new List<TreasureRuleConfig>();
        public List<TreasureSuitRuleConfig> Suits = new List<TreasureSuitRuleConfig>();

        private Dictionary<int, TreasureRuleConfig> treasuresById;
        private Dictionary<int, int> treasureIdByPieceId;
        private Dictionary<int, TreasureSuitRuleConfig> suitsById;
        private Dictionary<int, TreasureSuitRuleConfig> suitByTreasureId;
        private Dictionary<int, TreasureLevelMaterialRule> levelUpByLevel;
        private Dictionary<(int Quality, int Star), TreasureUpgradeMaterialRule> upgradeByKey;
        private Dictionary<(int Rank, int BlessLevel), TreasureBlessRule> blessByKey;
        private Dictionary<int, TreasureCommonMaterialSetting> commonByQuality;
        private HashSet<int> promoteMaterialItemIds;

        private static OfflineTreasureCatalog cached;

        public static OfflineTreasureCatalog Load()
        {
            if (cached != null) return cached;
            TextAsset asset = Resources.Load<TextAsset>("Treasure/treasure-rules");
            if (asset == null)
                throw new InvalidOperationException("Missing official Treasure/treasure-rules resource.");
            cached = Parse(asset.text);
            return cached;
        }

        public static OfflineTreasureCatalog Parse(string json)
        {
            OfflineTreasureCatalog catalog = JsonUtility.FromJson<OfflineTreasureCatalog>(json ?? string.Empty);
            if (catalog == null || catalog.Treasures == null || catalog.Treasures.Count == 0
                || catalog.Suits == null || catalog.Settings == null)
                throw new FormatException("Invalid official Treasure/treasure-rules payload.");
            catalog.BuildIndex();
            return catalog;
        }

        private void BuildIndex()
        {
            treasuresById = new Dictionary<int, TreasureRuleConfig>(Treasures.Count);
            treasureIdByPieceId = new Dictionary<int, int>(Treasures.Count);
            foreach (TreasureRuleConfig t in Treasures)
            {
                if (t == null || t.ClassId <= 0) continue;
                treasuresById[t.ClassId] = t;
                if (t.TreasurePieceId > 0) treasureIdByPieceId[t.TreasurePieceId] = t.ClassId;
            }

            suitsById = new Dictionary<int, TreasureSuitRuleConfig>(Suits.Count);
            suitByTreasureId = new Dictionary<int, TreasureSuitRuleConfig>();
            foreach (TreasureSuitRuleConfig s in Suits)
            {
                if (s == null || s.ClassId <= 0) continue;
                suitsById[s.ClassId] = s;
                if (s.Treasures == null) continue;
                foreach (int tid in s.Treasures) suitByTreasureId[tid] = s;
            }

            levelUpByLevel = new Dictionary<int, TreasureLevelMaterialRule>();
            promoteMaterialItemIds = new HashSet<int>();
            foreach (TreasureLevelMaterialRule r in LevelUpMaterials ?? new List<TreasureLevelMaterialRule>())
            {
                if (r == null) continue;
                levelUpByLevel[r.Level] = r;
                foreach (TreasureRuleItemAmount m in r.Materials ?? new List<TreasureRuleItemAmount>())
                    if (m != null && m.ItemId > 0) promoteMaterialItemIds.Add(m.ItemId);
            }

            upgradeByKey = new Dictionary<(int, int), TreasureUpgradeMaterialRule>();
            foreach (TreasureUpgradeMaterialRule r in UpgradeMaterials ?? new List<TreasureUpgradeMaterialRule>())
            {
                if (r == null) continue;
                upgradeByKey[(r.Quality, r.Star)] = r;
                foreach (TreasureRuleItemAmount m in r.Materials ?? new List<TreasureRuleItemAmount>())
                    if (m != null && m.ItemId > 0) promoteMaterialItemIds.Add(m.ItemId);
            }

            blessByKey = new Dictionary<(int, int), TreasureBlessRule>();
            foreach (TreasureBlessRule r in BlessInfos ?? new List<TreasureBlessRule>())
            {
                if (r == null) continue;
                blessByKey[(r.Rank, r.BlessLevel)] = r;
                foreach (TreasureRuleItemAmount m in r.LevelCostItems ?? new List<TreasureRuleItemAmount>())
                    if (m != null && m.ItemId > 0) promoteMaterialItemIds.Add(m.ItemId);
            }

            commonByQuality = new Dictionary<int, TreasureCommonMaterialSetting>();
            foreach (TreasureCommonMaterialSetting c in Settings.CommonMaterial2Piece ?? new List<TreasureCommonMaterialSetting>())
            {
                if (c == null) continue;
                commonByQuality[c.Quality] = c;
                if (c.ItemClassId > 0) promoteMaterialItemIds.Add(c.ItemClassId);
            }
        }

        private void EnsureIndex() { if (treasuresById == null) BuildIndex(); }

        public TreasureRuleConfig GetTreasure(int classId) { EnsureIndex(); return treasuresById.TryGetValue(classId, out var r) ? r : null; }
        public int GetTreasureIdByPieceId(int pieceId) { EnsureIndex(); return treasureIdByPieceId.TryGetValue(pieceId, out int tid) ? tid : 0; }
        public TreasureSuitRuleConfig GetSuit(int suitId) { EnsureIndex(); return suitsById.TryGetValue(suitId, out var s) ? s : null; }
        public TreasureSuitRuleConfig GetSuitByTreasureId(int treasureId) { EnsureIndex(); return suitByTreasureId.TryGetValue(treasureId, out var s) ? s : null; }
        public TreasureLevelMaterialRule GetLevelUpRule(int nextLevel) { EnsureIndex(); return levelUpByLevel.TryGetValue(nextLevel, out var r) ? r : null; }
        public TreasureUpgradeMaterialRule GetUpgradeRule(int quality, int star) { EnsureIndex(); return upgradeByKey.TryGetValue((quality, star), out var r) ? r : null; }
        public TreasureBlessRule GetBlessRule(int rank, int blessOffset) { EnsureIndex(); return blessByKey.TryGetValue((rank, blessOffset), out var r) ? r : null; }
        public TreasureCommonMaterialSetting GetCommonMaterial(int quality) { EnsureIndex(); return commonByQuality.TryGetValue(quality, out var c) ? c : null; }
        public bool IsPromoteMaterialItem(int itemId)
        {
            EnsureIndex();
            return treasureIdByPieceId.ContainsKey(itemId) || promoteMaterialItemIds.Contains(itemId);
        }
    }
}
