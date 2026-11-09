using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    [Serializable]
    internal sealed class NpcAdventureBattleRuntimeData
    {
        public string manifest;
        public int authorizedNodeCount;
        public int[] stageIds;
        public Map10RuntimeStage[] stages;
        public Map10RuntimeRoster[] monsterRosters;
        public Map10RuntimeBattleScene[] battleScenes;
        public NpcAdventureRewardRule[] rewardRules;
        public Map10RuntimeLevelNumber[] levelNumbers;
    }

    [Serializable]
    internal sealed class NpcAdventureBattleSourceManifest
    {
        public string manifest;
        public NpcAdventureBattleOutput output;
    }

    [Serializable]
    internal sealed class NpcAdventureBattleOutput
    {
        public string path;
        public string sha256;
        public long size;
    }

    [Serializable]
    internal sealed class NpcAdventureRewardRule
    {
        public int Id;
        public int Order;
        public float Percent;
        public int[] IdList;
        public int[] WeightList;
        public float[] CountList;
        public int LevelCountId;
        public int[] SubRankCountId;
    }

    [Serializable]
    public sealed class NpcAdventureRewardItem
    {
        public int ItemId;
        public long Count;
    }

    /// <summary>Verified selected-source battles and stage.Awards for Bond Adventure.</summary>
    public sealed class OfficialNpcAdventureBattleCatalog
    {
        public const int AuthorizedNodeCount = 720;
        private const string ExpectedManifest = "884_156692";
        private const string ResourcePath = "NpcAdventure/battle-stages";
        private readonly Dictionary<int, Map10RuntimeStage> stages;
        private readonly Dictionary<int, Map10RuntimeRoster> rosters;
        private readonly Dictionary<int, Map10RuntimeBattleScene> scenes;
        private readonly Dictionary<int, NpcAdventureRewardRule> rewardRules;
        private readonly Dictionary<int, float[]> levelNumbers;

        private OfficialNpcAdventureBattleCatalog(NpcAdventureBattleRuntimeData data)
        {
            if (data.manifest != ExpectedManifest || data.authorizedNodeCount != AuthorizedNodeCount
                || data.stageIds == null || data.stages == null || data.monsterRosters == null
                || data.battleScenes == null || data.rewardRules == null || data.levelNumbers == null)
                throw new InvalidOperationException("Official NPC Adventure runtime data is incomplete.");
            stages = data.stages.ToDictionary(value => value.Id);
            rosters = data.monsterRosters.ToDictionary(value => value.StageId);
            scenes = data.battleScenes.ToDictionary(value => value.GameId);
            rewardRules = data.rewardRules.ToDictionary(value => value.Id);
            levelNumbers = data.levelNumbers.ToDictionary(value => value.id,
                value => value.values ?? Array.Empty<float>());
            int[] authorizedIds = data.stageIds.Distinct().OrderBy(value => value).ToArray();
            if (authorizedIds.Length != AuthorizedNodeCount || stages.Count != AuthorizedNodeCount
                || rosters.Count != AuthorizedNodeCount || scenes.Count != 24
                || rewardRules.Count != 18 || levelNumbers.Count != 1
                || !authorizedIds.SequenceEqual(stages.Keys.OrderBy(value => value)))
                throw new InvalidOperationException("Official NPC Adventure battle data counts or stage scope differ.");
            foreach (int stageId in authorizedIds)
            {
                Map10RuntimeStage stage = stages[stageId];
                if (stage.Type != 41 || !rosters.ContainsKey(stageId)
                    || !scenes.ContainsKey(stage.GameId) || stage.Awards == null || stage.Awards.Length == 0)
                    throw new InvalidOperationException("Official NPC Adventure stage data is incomplete: " + stageId);
                foreach (int awardId in stage.Awards)
                    if (!rewardRules.ContainsKey(awardId))
                        throw new InvalidOperationException("Official NPC Adventure stage award is missing: " + awardId);
            }
        }

        public static OfficialNpcAdventureBattleCatalog LoadDefault()
        {
            TextAsset source = Resources.Load<TextAsset>(ResourcePath);
            TextAsset sourceManifest = Resources.Load<TextAsset>(ResourcePath + "-source-manifest");
            if (source == null || sourceManifest == null)
                throw new InvalidOperationException("Official NPC Adventure battle data is not installed.");
            NpcAdventureBattleSourceManifest manifest =
                JsonUtility.FromJson<NpcAdventureBattleSourceManifest>(sourceManifest.text);
            byte[] bytes = Encoding.UTF8.GetBytes(source.text);
            using (SHA256 sha256 = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                if (manifest == null || manifest.manifest != ExpectedManifest || manifest.output == null
                    || manifest.output.path != "unity/Assets/Game/Resources/NpcAdventure/battle-stages.json"
                    || manifest.output.size != bytes.Length
                    || !string.Equals(manifest.output.sha256, hash, StringComparison.Ordinal))
                    throw new InvalidOperationException("Official NPC Adventure battle data hash is invalid.");
            }
            NpcAdventureBattleRuntimeData data = JsonUtility.FromJson<NpcAdventureBattleRuntimeData>(source.text);
            if (data == null) throw new InvalidOperationException("Official NPC Adventure data could not be parsed.");
            return new OfficialNpcAdventureBattleCatalog(data);
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

        public List<NpcAdventureRewardItem> RollRewards(int stageId, int playerLevel, int playerSubRank,
            ref ulong randomState)
        {
            if (!stages.TryGetValue(stageId, out Map10RuntimeStage stage))
                throw new KeyNotFoundException("Official NPC Adventure stage is missing: " + stageId);
            var counts = new SortedDictionary<int, long>();
            foreach (int ruleId in stage.Awards)
            {
                NpcAdventureRewardRule rule = rewardRules[ruleId];
                if (rule.IdList == null || rule.WeightList == null || rule.CountList == null
                    || rule.IdList.Length != rule.WeightList.Length || rule.IdList.Length != rule.CountList.Length)
                    throw new InvalidOperationException("Official NPC Adventure reward rule is malformed: " + ruleId);
                long totalWeight = rule.WeightList.Where(value => value > 0).Sum(value => (long)value);
                if (totalWeight == 0 || rule.Percent <= 0f) continue;
                for (int index = 0; index < rule.IdList.Length; index++)
                {
                    int itemId = rule.IdList[index], weight = rule.WeightList[index];
                    float amount = rule.CountList[index];
                    if (itemId <= 0 || weight <= 0 || amount <= 0f) continue;
                    if (rule.LevelCountId > 0) amount *= LevelNumber(rule.LevelCountId, playerLevel);
                    if (rule.SubRankCountId != null && index < rule.SubRankCountId.Length
                        && rule.SubRankCountId[index] > 0)
                        amount *= LevelNumber(rule.SubRankCountId[index], playerSubRank);
                    double probability = rule.Percent * weight / totalWeight;
                    if (probability < 1d && !Roll(probability, ref randomState)) continue;
                    long count = (long)amount;
                    if (count <= 0) continue;
                    counts[itemId] = counts.TryGetValue(itemId, out long prior)
                        ? checked(prior + count) : count;
                }
            }
            return counts.Select(value => new NpcAdventureRewardItem
                { ItemId = value.Key, Count = value.Value }).ToList();
        }

        private float LevelNumber(int id, int level)
        {
            if (!levelNumbers.TryGetValue(id, out float[] values) || values.Length == 0)
                throw new InvalidOperationException("Official NPC Adventure reward multiplier is missing: " + id);
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
