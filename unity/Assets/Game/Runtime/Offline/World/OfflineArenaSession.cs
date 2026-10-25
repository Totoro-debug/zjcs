using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.State;

namespace Zjcs.Offline.World
{
    [Serializable]
    public sealed class ArenaRuntimeData
    {
        public ArenaRuntimeSettings settings;
        public ArenaRuntimeCatalog runtime;
        public ArenaRuntimeFunctionLimit[] function_limits;
        public ArenaRuntimeScene arena_scene;
    }

    [Serializable]
    public sealed class ArenaRuntimeFunctionLimit
    {
        public int Type;
        public int Key0;
        public int Key1;
        public ArenaRuntimeWorldMapProgressCondition Condition;
    }

    [Serializable]
    public sealed class ArenaRuntimeWorldMapProgressCondition
    {
        public string _type;
        public int MapClassId;
        public int Region;
        public int Progress;
    }

    [Serializable]
    public sealed class ArenaRuntimeSettings
    {
        public int ChallengeCountId;
        public int CoinId;
        public long AttackWinCoinReward;
        public long AttackLoseCoinReward;
        public long InitRefreshCount;
        public long InitChallengeCount;
        public int BuyChallengeCountCostItem;
        public long ChallengeCountBuyOnce;
        public long[] BuyChallengeCountPrice;
        public int StageId;
        public int FightPos;
        public long ChallengeRedDotRequireCount;
        public int ArenaCombatRatingDirectWinPercent;
        public int GuardSkillCombatRatinScaleId;
    }

    [Serializable]
    public sealed class ArenaRuntimeCatalog
    {
        public int refreshCostItemId;
        public long refreshCost;
        public ArenaRuntimeGuard[] guards;
        public ArenaRuntimeRankTemplate[] rankTemplates;
        public ArenaRuntimeMatchRule[] matchRules;
        public ArenaRuntimeMatchStrategy[] matchStrategies;
    }

    [Serializable]
    public sealed class ArenaRuntimeGuard
    {
        public int id;
        public string nameKey;
        public int profession;
        public int monsterId;
        public int monsterGroupId;
        public int level;
        public int subRank;
        public ArenaRuntimeAppearanceSet[] appendAppearances;
        public ArenaRuntimeSkill[] activeSkills;
        public ArenaRuntimeSkill[] passiveSkills;
    }

    [Serializable]
    public sealed class ArenaRuntimeAppearanceSet
    {
        public int sex;
        public ArenaRuntimeSkin[] skins;
    }

    [Serializable]
    public sealed class ArenaRuntimeSkin
    {
        public int skinType;
        public int skinId;
    }

    [Serializable]
    public sealed class ArenaRuntimeSkill
    {
        public int skillId;
        public int level;
        public int rank;
    }

    [Serializable]
    public sealed class ArenaRuntimeRankTemplate
    {
        public int lastRank;
        public ArenaRuntimeWeight[] weights;
    }

    [Serializable]
    public sealed class ArenaRuntimeWeight
    {
        public int templateId;
        public int weight;
    }

    [Serializable]
    public sealed class ArenaRuntimeMatchRule
    {
        public int id;
        public int type;
        public bool isAhead;
        public int[] percent;
        public int[] range;
    }

    [Serializable]
    public sealed class ArenaRuntimeMatchStrategy
    {
        public int rank;
        public ArenaRuntimeRuleCount[] rules;
    }

    [Serializable]
    public sealed class ArenaRuntimeRuleCount
    {
        public int ruleId;
        public int count;
    }

    [Serializable]
    public sealed class ArenaRuntimeScene
    {
        public int stageId;
        public int gameId;
        public string sceneName;
        public int gridWidth;
        public int gridHeight;
        public float[] gridOrigin;
        public ArenaRuntimeCell[] cells;
        public ArenaRuntimeFaction[] factions;
    }

    [Serializable]
    public sealed class ArenaRuntimeCell
    {
        public int x;
        public int y;
        public int groundType;
        public int height;
    }

    [Serializable]
    public sealed class ArenaRuntimeFaction
    {
        public int id;
        public int[][] entryPositions;
        public int[] entryDirections;
    }

    [Serializable]
    public sealed class ArenaOpponentSave
    {
        public int rank;
        public int guardTemplateId;
    }

    [Serializable]
    public sealed class ArenaSave
    {
        public int schema = 1;
        public string localDate;
        public int rank = 5000;
        public int tickets;
        public int purchasedTickets;
        public int refreshesUsed;
        public int wins;
        public int losses;
        public int guardAppearanceCount;
        public List<ArenaOpponentSave> opponents = new List<ArenaOpponentSave>();
        public int activeOpponentRank;
        public int activeGuardTemplateId;
        public int activeGuardSex;
        public string activeBattleCheckpoint = string.Empty;
        public string activeChallengeId = string.Empty;
    }

    public sealed class OfflineArenaSession
    {
        private readonly string path;
        private ArenaSave save;
        private uint randomState;

        public ArenaRuntimeData Catalog { get; }
        public ArenaSave Save => save;
        public int Rank => save.rank;
        public int Tickets => save.tickets;
        public int Wins => save.wins;
        public int Losses => save.losses;
        public int RefreshesUsed => save.refreshesUsed;
        public int PurchasedTickets => save.purchasedTickets;
        public long NextTicketPrice => (Catalog.settings.BuyChallengeCountPrice ?? Array.Empty<long>())
            .ElementAtOrDefault(save.purchasedTickets);
        public IReadOnlyList<ArenaOpponentSave> Opponents => save.opponents;
        public int ActiveOpponentRank => save.activeOpponentRank;
        public int ActiveGuardTemplateId => save.activeGuardTemplateId;
        public int ActiveGuardSex => save.activeGuardSex;
        public string ActiveBattleCheckpoint => save.activeBattleCheckpoint;
        public bool HasActiveChallenge => !string.IsNullOrEmpty(save.activeChallengeId);

        private OfflineArenaSession(string path, ArenaRuntimeData catalog, ArenaSave state)
        {
            this.path = path;
            Catalog = catalog;
            save = state;
            randomState = Seed(state.localDate + ":" + state.refreshesUsed + ":" + state.rank);
            if (save.opponents == null || save.opponents.Count == 0) ReplaceOpponents();
        }

        public static ArenaRuntimeData LoadCatalog()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dungeons/arena-rules");
            if (asset == null) throw new InvalidDataException("Pinned official arena runtime catalog is missing.");
            ArenaRuntimeData data = JsonUtility.FromJson<ArenaRuntimeData>(asset.text);
            ValidateCatalog(data);
            return data;
        }

        public static OfflineArenaSession Open(ProfileSlotCatalog slots, string slotId,
            ArenaRuntimeData catalog = null)
        {
            if (slots == null || !slots.Contains(slotId))
                throw new ArgumentException("A valid profile slot is required.", nameof(slotId));
            catalog = catalog ?? LoadCatalog();
            string path = Path.Combine(slots.GetSlotDirectory(slotId), "arena-save.json");
            SaveReadResult result = LocalJsonSaveFile.Read(path, IsValidSave);
            if (result.Status == SaveReadStatus.Corrupt)
                throw new InvalidDataException("The selected profile arena save is corrupt.");
            ArenaSave state = result.Json == null
                ? NewSave(DateTime.Now, checked((int)catalog.settings.InitChallengeCount))
                : JsonUtility.FromJson<ArenaSave>(result.Json);
            Normalize(state);
            var session = new OfflineArenaSession(path, catalog, state);
            session.ResetDaily(DateTime.Now);
            session.Persist(session.save);
            return session;
        }

        public ArenaRuntimeGuard FindGuard(int id) => Catalog.runtime.guards.FirstOrDefault(value => value.id == id);

        public bool IsFunctionLimitMet(int type, int key0, Func<int, int, int> regionProgress)
        {
            ArenaRuntimeFunctionLimit limit = (Catalog.function_limits ?? Array.Empty<ArenaRuntimeFunctionLimit>())
                .FirstOrDefault(value => value.Type == type && value.Key0 == key0);
            ArenaRuntimeWorldMapProgressCondition condition = limit?.Condition;
            return condition != null && condition._type == "WorldMapProgressCondition"
                && regionProgress != null && regionProgress(condition.MapClassId, condition.Region) >= condition.Progress;
        }

        public bool TryBuyChallenge(Func<string, int, long, bool> consumeCurrency, out string error)
        {
            error = null;
            if (HasActiveChallenge) { error = "竞技挑战正在进行。"; return false; }
            long[] prices = Catalog.settings.BuyChallengeCountPrice ?? Array.Empty<long>();
            if (save.purchasedTickets >= prices.Length) { error = "今日购买次数已用完。"; return false; }
            long cost = prices[save.purchasedTickets];
            string receipt = "arena:buy-ticket:" + save.localDate + ":" + save.purchasedTickets;
            if (cost <= 0 || consumeCurrency == null
                || !consumeCurrency(receipt, Catalog.settings.BuyChallengeCountCostItem, cost))
            { error = "晨星不足，无法购买挑战次数。"; return false; }
            ArenaSave next = Clone(save);
            next.purchasedTickets++;
            next.tickets = checked(next.tickets + (int)Catalog.settings.ChallengeCountBuyOnce);
            Persist(next);
            return true;
        }

        public bool TryRefresh(Func<string, int, long, bool> consumeCurrency, out string error)
        {
            error = null;
            if (HasActiveChallenge) { error = "竞技挑战正在进行。"; return false; }
            int refreshIndex = save.refreshesUsed;
            if (refreshIndex >= Catalog.settings.InitRefreshCount)
            {
                string receipt = "arena:refresh-cost:" + save.localDate + ":" + refreshIndex;
                if (consumeCurrency == null || !consumeCurrency(receipt,
                    Catalog.runtime.refreshCostItemId, Catalog.runtime.refreshCost))
                { error = "晨星不足，无法刷新对手。"; return false; }
            }
            ArenaSave next = Clone(save);
            next.refreshesUsed++;
            randomState = Seed(next.localDate + ":" + next.refreshesUsed + ":" + next.rank);
            next.opponents = CreateOpponents(next.rank);
            Persist(next);
            return true;
        }

        public bool TryBeginChallenge(int index, out string error)
        {
            error = null;
            if (HasActiveChallenge) { error = "已有竞技挑战正在进行。"; return false; }
            if (save.tickets <= 0) { error = "今日挑战次数不足。"; return false; }
            if (index < 0 || index >= save.opponents.Count) return false;
            ArenaOpponentSave opponent = save.opponents[index];
            ArenaRuntimeGuard guard = FindGuard(opponent.guardTemplateId);
            ArenaRuntimeAppearanceSet[] appearances = guard?.appendAppearances
                ?? Array.Empty<ArenaRuntimeAppearanceSet>();
            if (guard == null || opponent.rank <= 0 || appearances.Length == 0) return false;
            ArenaSave next = Clone(save);
            next.tickets--;
            next.activeOpponentRank = opponent.rank;
            next.activeGuardTemplateId = opponent.guardTemplateId;
            uint appearanceChoice = Seed(next.localDate + ":guard:" + next.guardAppearanceCount);
            next.activeGuardSex = appearances[(int)(appearanceChoice % (uint)appearances.Length)].sex;
            if (next.activeGuardSex < 0 || next.activeGuardSex > 1) return false;
            next.guardAppearanceCount = checked(next.guardAppearanceCount + 1);
            next.activeChallengeId = Guid.NewGuid().ToString("N");
            next.activeBattleCheckpoint = string.Empty;
            Persist(next);
            return true;
        }

        public bool SaveBattleCheckpoint(string checkpoint)
        {
            if (!HasActiveChallenge || string.IsNullOrWhiteSpace(checkpoint)) return false;
            ArenaSave next = Clone(save);
            next.activeBattleCheckpoint = checkpoint;
            Persist(next);
            return true;
        }

        public bool CompleteChallenge(bool won, Action<string, int, long> grantReward)
        {
            if (!HasActiveChallenge || grantReward == null) return false;
            ArenaSave completed = Clone(save);
            if (won)
            {
                completed.wins++;
                completed.rank = completed.activeOpponentRank;
            }
            else completed.losses++;
            int amount = checked((int)(won ? Catalog.settings.AttackWinCoinReward
                : Catalog.settings.AttackLoseCoinReward));
            grantReward("arena:challenge-result:" + completed.activeChallengeId,
                Catalog.settings.CoinId, amount);
            completed.activeOpponentRank = 0;
            completed.activeGuardTemplateId = 0;
            completed.activeGuardSex = 0;
            completed.activeBattleCheckpoint = string.Empty;
            completed.activeChallengeId = string.Empty;
            completed.opponents = CreateOpponents(completed.rank);
            Persist(completed);
            return true;
        }

        public void RecordBattleCheckpoint(string value)
        {
            if (!SaveBattleCheckpoint(value))
                throw new InvalidOperationException("Arena battle checkpoint could not be saved.");
        }

        private List<ArenaOpponentSave> CreateOpponents(int rank)
        {
            ArenaRuntimeMatchStrategy strategy = (Catalog.runtime.matchStrategies ?? Array.Empty<ArenaRuntimeMatchStrategy>())
                .Where(value => value != null && value.rank >= rank).OrderBy(value => value.rank).FirstOrDefault()
                ?? Catalog.runtime.matchStrategies.OrderBy(value => value.rank).Last();
            var result = new List<ArenaOpponentSave>();
            foreach (ArenaRuntimeRuleCount count in strategy.rules ?? Array.Empty<ArenaRuntimeRuleCount>())
            {
                ArenaRuntimeMatchRule rule = Catalog.runtime.matchRules.FirstOrDefault(value => value.id == count.ruleId);
                if (rule == null) throw new InvalidDataException("Official arena match rule is missing: " + count.ruleId);
                foreach (int targetRank in SelectRanks(rule, rank, count.count))
                    result.Add(new ArenaOpponentSave { rank = targetRank, guardTemplateId = SelectGuard(targetRank) });
            }
            return result;
        }

        private IEnumerable<int> SelectRanks(ArenaRuntimeMatchRule rule, int rank, int count)
        {
            int low;
            int high;
            if (rule.isAhead)
            {
                low = rule.type == 1 ? rank * (100 - rule.percent[0]) / 100 : rank - rule.range[0];
                high = rule.type == 1 ? rank * (100 - rule.percent[1]) / 100 + 1 : rank - rule.range[1] + 1;
                if (low >= rank) low = rank - 1;
            }
            else
            {
                high = rule.type == 1 ? rank * (100 + rule.percent[0]) : rank + rule.range[0];
                low = rule.type == 1 ? rank * (100 + rule.percent[1]) / 100 - 1 : rank + rule.range[1] - 1;
                if (high <= rank) high = rank + 1;
            }
            low = Mathf.Clamp(low, 1, Catalog.settings.FightPos);
            high = Mathf.Clamp(high, 1, Catalog.settings.FightPos);
            if (low < high) yield break;
            int size = low - high + 1;
            int selected = Math.Min(size, Math.Max(0, count));
            var ranks = Enumerable.Range(high, size).OrderBy(_ => NextUInt()).Take(selected).ToList();
            if (ranks.Count < count && ranks.Count > 0)
                while (ranks.Count < count) ranks.Add(ranks[0]);
            foreach (int value in ranks) yield return value;
        }

        private int SelectGuard(int rank)
        {
            ArenaRuntimeRankTemplate row = Catalog.runtime.rankTemplates
                .Where(value => value.lastRank >= rank).OrderBy(value => value.lastRank).FirstOrDefault()
                ?? Catalog.runtime.rankTemplates.OrderBy(value => value.lastRank).Last();
            ArenaRuntimeWeight[] weights = row.weights ?? Array.Empty<ArenaRuntimeWeight>();
            int total = weights.Where(value => value.weight > 0).Sum(value => value.weight);
            if (total <= 0) throw new InvalidDataException("Official arena guard template band has no weights.");
            int choice = (int)(NextUInt() % (uint)total);
            foreach (ArenaRuntimeWeight weight in weights)
            {
                if (weight.weight <= 0) continue;
                if (choice < weight.weight) return weight.templateId;
                choice -= weight.weight;
            }
            throw new InvalidDataException("Official arena guard weight selection failed.");
        }

        private void ResetDaily(DateTime now)
        {
            string date = now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            if (save.localDate == date) return;
            ArenaSave next = Clone(save);
            next.localDate = date;
            next.tickets = checked((int)Catalog.settings.InitChallengeCount);
            next.purchasedTickets = 0;
            next.refreshesUsed = 0;
            randomState = Seed(next.localDate + ":0:" + next.rank);
            next.opponents = CreateOpponents(next.rank);
            Persist(next);
        }

        private void ReplaceOpponents() => save.opponents = CreateOpponents(save.rank);

        private uint NextUInt()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return randomState;
        }

        private static uint Seed(string value)
        {
            uint state = 2166136261;
            foreach (char c in value) state = unchecked((state ^ c) * 16777619);
            return state == 0 ? 0x9e3779b9 : state;
        }

        private static ArenaSave NewSave(DateTime now, int initialChallengeCount) => new ArenaSave
        {
            localDate = now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
            rank = 5000,
            tickets = initialChallengeCount
        };

        private static ArenaSave Clone(ArenaSave value) => JsonUtility.FromJson<ArenaSave>(JsonUtility.ToJson(value));

        private void Persist(ArenaSave next)
        {
            Normalize(next);
            if (!IsValidSave(JsonUtility.ToJson(next))) throw new InvalidDataException("Arena save is invalid.");
            LocalJsonSaveFile.Write(path, JsonUtility.ToJson(next, true), IsValidSave);
            save = next;
        }

        private static void ValidateCatalog(ArenaRuntimeData data)
        {
            if (data?.settings == null || data.runtime == null || data.runtime.guards == null
                || data.runtime.guards.Length != 500 || data.runtime.rankTemplates == null
                || data.runtime.rankTemplates.Length != 34 || data.runtime.matchRules == null
                || data.runtime.matchRules.Length != 10 || data.runtime.matchStrategies == null
                || data.runtime.matchStrategies.Length != 7 || data.arena_scene == null
                || data.function_limits == null || data.function_limits.Length != 2
                || data.arena_scene.stageId != data.settings.StageId || data.arena_scene.gameId != 911
                || data.arena_scene.cells == null || data.arena_scene.factions == null)
                throw new InvalidDataException("Pinned official arena catalog failed validation.");
        }

        private static bool IsValidSave(string json)
        {
            try
            {
                ArenaSave data = JsonUtility.FromJson<ArenaSave>(json);
                if (data == null || data.schema != 1 || data.rank <= 0 || data.tickets < 0
                    || data.purchasedTickets < 0 || data.refreshesUsed < 0 || data.wins < 0 || data.losses < 0
                    || data.guardAppearanceCount < 0 || data.activeGuardSex < 0 || data.activeGuardSex > 1
                    || data.opponents == null || data.activeChallengeId == null || data.activeBattleCheckpoint == null)
                    return false;
                if (data.opponents.Any(value => value == null || value.rank <= 0 || value.guardTemplateId <= 0)) return false;
                return string.IsNullOrEmpty(data.activeChallengeId)
                    ? data.activeOpponentRank == 0 && data.activeGuardTemplateId == 0 && data.activeGuardSex == 0
                    : data.activeOpponentRank > 0 && data.activeGuardTemplateId > 0;
            }
            catch { return false; }
        }

        private static void Normalize(ArenaSave data)
        {
            if (data.localDate == null) data.localDate = string.Empty;
            if (data.opponents == null) data.opponents = new List<ArenaOpponentSave>();
            if (data.activeBattleCheckpoint == null) data.activeBattleCheckpoint = string.Empty;
            if (data.activeChallengeId == null) data.activeChallengeId = string.Empty;
        }
    }
}
