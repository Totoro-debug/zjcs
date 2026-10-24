using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    [Serializable]
    internal sealed class FantasyTowerRuntimeData
    {
        public string manifest;
        public FantasyTowerLayerInfo[] towerStages;
        public FantasyTowerEntryInfo[] entries;
        public Map10RuntimeStage[] stages;
        public Map10RuntimeRoster[] monsterRosters;
        public Map10RuntimeBattleScene[] battleScenes;
        public FantasyTowerRewardRule[] rewardRules;
        public Map10RuntimeLevelNumber[] levelNumbers;
    }

    [Serializable]
    internal sealed class FantasyTowerSourceManifest
    {
        public string manifest;
        public string runtimeOutputSha256;
    }

    [Serializable]
    public sealed class FantasyTowerLayerInfo
    {
        public int Id;
        public int EntryIndex;
        public int Layer;
        public int[] CombatRating;
    }

    [Serializable]
    internal sealed class FantasyTowerEntryInfo
    {
        public int EntryIndex;
        public int[] StageIds;
        public int LayerCount;
    }

    [Serializable]
    internal sealed class FantasyTowerRewardRule
    {
        public int Id, Order, LevelCountId;
        public float Percent;
        public int[] IdList, WeightList, SubRankCountId;
        public float[] CountList;
    }

    public sealed class OfficialFantasyTowerCatalog
    {
        private const string ResourcePath = "TowerPush/tower-battles";
        private readonly Dictionary<int, FantasyTowerLayerInfo> layers;
        private readonly Dictionary<int, Map10RuntimeStage> stages;
        private readonly Dictionary<int, Map10RuntimeRoster> rosters;
        private readonly Dictionary<int, Map10RuntimeBattleScene> scenes;
        private readonly Dictionary<int, List<FantasyTowerRewardRule>> rewardRules;
        private readonly Dictionary<int, float[]> levelNumbers;

        private OfficialFantasyTowerCatalog(FantasyTowerRuntimeData data)
        {
            if (data.manifest != "854_156383" || data.towerStages == null || data.stages == null
                || data.monsterRosters == null || data.battleScenes == null)
                throw new InvalidOperationException("Official Fantasy Tower runtime data is incomplete.");
            layers = data.towerStages.Where(value => value.Layer > 0)
                .ToDictionary(value => value.Id);
            stages = data.stages.ToDictionary(value => value.Id);
            rosters = data.monsterRosters.ToDictionary(value => value.StageId);
            scenes = data.battleScenes.ToDictionary(value => value.GameId);
            rewardRules = (data.rewardRules ?? Array.Empty<FantasyTowerRewardRule>())
                .GroupBy(value => value.Id).ToDictionary(group => group.Key,
                    group => group.OrderBy(value => value.Order < 0 ? int.MaxValue : value.Order).ToList());
            levelNumbers = (data.levelNumbers ?? Array.Empty<Map10RuntimeLevelNumber>())
                .ToDictionary(value => value.id, value => value.values ?? Array.Empty<float>());
            int[] expectedCounts = { 110, 115, 160, 200 };
            for (int entry = 1; entry <= 4; entry++)
            {
                int count = layers.Values.Count(value => value.EntryIndex == entry);
                if (count != expectedCounts[entry - 1])
                    throw new InvalidOperationException("Official Fantasy Tower entry layer count is invalid: " + entry);
            }
            if (layers.Count != 585 || stages.Count != 585 || rosters.Count != 585)
                throw new InvalidOperationException("Official Fantasy Tower battle data must contain all 585 layers.");
            foreach (FantasyTowerLayerInfo layer in layers.Values)
            {
                if (!stages.ContainsKey(layer.Id) || !rosters.ContainsKey(layer.Id))
                    throw new InvalidOperationException("Official Fantasy Tower battle stage is missing: " + layer.Id);
                if (!scenes.ContainsKey(stages[layer.Id].GameId))
                    throw new InvalidOperationException("Official Fantasy Tower battle scene is missing: " + layer.Id);
            }
        }

        public static OfficialFantasyTowerCatalog LoadDefault()
        {
            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            if (source == null) throw new InvalidOperationException("Official Fantasy Tower data is not installed.");
            TextAsset sourceManifest = Resources.Load<TextAsset>("TowerPush/tower-battle-source-manifest");
            if (sourceManifest == null)
                throw new InvalidOperationException("Official Fantasy Tower source manifest is not installed.");
            FantasyTowerSourceManifest manifest = JsonUtility.FromJson<FantasyTowerSourceManifest>(sourceManifest.text);
            using (SHA256 sha256 = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(source.text)))
                    .Replace("-", string.Empty).ToLowerInvariant();
                if (manifest == null || manifest.manifest != "854_156383"
                    || !string.Equals(manifest.runtimeOutputSha256, hash, StringComparison.Ordinal))
                    throw new InvalidOperationException("Official Fantasy Tower runtime data hash is invalid.");
            }
            FantasyTowerRuntimeData data = JsonUtility.FromJson<FantasyTowerRuntimeData>(source.text);
            if (data == null) throw new InvalidOperationException("Official Fantasy Tower data could not be parsed.");
            return new OfficialFantasyTowerCatalog(data);
        }

        public FantasyTowerLayerInfo GetLayer(int entryIndex, int layer)
        {
            if (entryIndex < 1 || entryIndex > 4 || layer < 1)
                throw new ArgumentOutOfRangeException(nameof(layer));
            FantasyTowerLayerInfo result = layers.Values.FirstOrDefault(value =>
                value.EntryIndex == entryIndex && value.Layer == layer);
            if (result == null) throw new ArgumentOutOfRangeException(nameof(layer));
            return result;
        }

        public int LayerCount(int entryIndex)
        {
            if (entryIndex < 1 || entryIndex > 4) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            return layers.Values.Count(value => value.EntryIndex == entryIndex);
        }

        public bool TryGetBattle(int stageId, out Map10RuntimeStage stage,
            out Map10RuntimeRoster roster, out Map10RuntimeBattleScene scene)
        {
            stage = null;
            roster = null;
            scene = null;
            if (!stages.TryGetValue(stageId, out stage) || !rosters.TryGetValue(stageId, out roster)) return false;
            return scenes.TryGetValue(stage.GameId, out scene);
        }

        public int CurrentLayer(OfflineFantasyTowerSession progress, int entryIndex)
        {
            FantasyTowerLayerInfo last = layers.Values.Where(value => value.EntryIndex == entryIndex)
                .OrderBy(value => value.Layer).Last();
            int cleared = progress.GetEntry(entryIndex).clearedLayer;
            return cleared >= last.Layer ? 0 : cleared + 1;
        }

        public List<FantasyTowerReward> RollRewards(int stageId, int playerLevel, int subRank,
            ref ulong randomState)
        {
            Map10RuntimeStage stage = stages[stageId];
            var counts = new SortedDictionary<int, long>();
            foreach (int ruleId in stage.Awards ?? Array.Empty<int>())
            {
                if (!rewardRules.TryGetValue(ruleId, out List<FantasyTowerRewardRule> rows))
                    throw new InvalidOperationException("Official Fantasy Tower reward rule is missing: " + ruleId);
                FantasyTowerRewardRule rule = rows.FirstOrDefault();
                if (rule.IdList == null || rule.WeightList == null || rule.CountList == null
                    || rule.IdList.Length != rule.WeightList.Length || rule.IdList.Length != rule.CountList.Length)
                    throw new InvalidOperationException("Official Fantasy Tower reward rule is malformed: " + ruleId);
                long totalWeight = rule.WeightList.Where(value => value > 0).Sum(value => (long)value);
                if (totalWeight == 0 || rule.Percent <= 0f) continue;
                for (int index = 0; index < rule.IdList.Length; index++)
                {
                    int itemId = rule.IdList[index], weight = rule.WeightList[index];
                    float amount = rule.CountList[index];
                    if (itemId <= 0 || weight <= 0 || amount <= 0f) continue;
                    if (rule.LevelCountId > 0) amount *= LevelNumber(rule.LevelCountId, playerLevel);
                    if (rule.SubRankCountId != null && index < rule.SubRankCountId.Length
                        && rule.SubRankCountId[index] > 0) amount *= LevelNumber(rule.SubRankCountId[index], subRank);
                    double probability = rule.Percent * weight / totalWeight;
                    if (probability < 1d && !Roll(probability, ref randomState)) continue;
                    long count = (long)amount;
                    if (count <= 0) continue;
                    counts[itemId] = counts.TryGetValue(itemId, out long prior) ? checked(prior + count) : count;
                }
            }
            return counts.Select(value => new FantasyTowerReward { itemId = value.Key, count = value.Value }).ToList();
        }

        private float LevelNumber(int id, int level)
        {
            if (!levelNumbers.TryGetValue(id, out float[] values) || values.Length == 0)
                throw new InvalidOperationException("Official Fantasy Tower reward multiplier is missing: " + id);
            return values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
        }

        private static bool Roll(double probability, ref ulong state)
        {
            state = state == 0 ? 0x9e3779b97f4a7c15UL : state;
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return (state & 0x00ffffffUL) / 16777216d < Math.Min(1d, probability);
        }
    }
}
