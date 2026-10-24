using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.Equipment
{
    public enum OfflineGemMergeResultType
    {
        Explode,
        DownAndExplode,
        Success,
        Fail
    }

    [Serializable]
    public sealed class OfflineGemMergeRule
    {
        public int Rank;
        public int RequireCount;
        public int NeedMaterialRank;
        public long CostMoney;
        public bool IsHighRankMerge;
        public double BaseRate;
        public double[] StepRate;
        public int PercentMininum;
        public int FailedResult;
    }

    [Serializable]
    public sealed class OfflineGemForgeRules
    {
        public string manifest;
        public OfflineGemMergeRule[] gem_merge;
        public int[] gem_merge_stories;
    }

    [Serializable]
    internal sealed class OfflineGemForgeSourceManifest
    {
        public string manifest;
        public OfflineGemForgeOutput output;
    }

    [Serializable]
    internal sealed class OfflineGemForgeOutput
    {
        public string path;
        public long bytes;
        public string sha256;
    }

    public sealed class OfflineGemMergeQuote
    {
        public int MainItemId { get; internal set; }
        public int ResultItemId { get; internal set; }
        public int MaterialItemId { get; internal set; }
        public int MaterialCount { get; internal set; }
        public long CostMoney { get; internal set; }
        public int TargetRank { get; internal set; }
        public bool IsHighRank { get; internal set; }
        public double SuccessRate { get; internal set; }
        public double Energy { get; internal set; }
        public bool IsReady { get; internal set; }
    }

    public sealed class OfflineGemMergeMaterialSource
    {
        public int ItemId { get; set; }
        public int Count { get; set; } = 1;
        public long EquipmentInstanceId { get; set; }
        public int GemSlot { get; set; } = -1;
        public bool IsInlaid => EquipmentInstanceId > 0 && GemSlot >= 0;
    }

    public sealed class OfflineGemMergeReceipt
    {
        public string Id { get; internal set; }
        public int MainItemId { get; internal set; }
        public int ResultItemId { get; internal set; }
        public int MaterialItemId { get; internal set; }
        public int MaterialCount { get; internal set; }
        public long CostMoney { get; internal set; }
        public OfflineGemMergeResultType Result { get; internal set; }
        public int PreviousMissCount { get; internal set; }
        public int MissCount { get; internal set; }
        public int StoryIndex { get; internal set; } = -1;
        public int RefundedMaterials { get; internal set; }
    }

    public sealed class OfflineGemForgeService
    {
        private const int MoneyItemIdFallback = 0;
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialEquipmentCatalog equipment;
        private readonly OfflineGemMergeRule[] rules;
        private readonly int storyCount;
        private readonly int moneyItemId;

        public OfflineGemForgeService(OfflineMainlineProgressSession session,
            OfficialEquipmentCatalog equipment, OfflineGemForgeRules sourceRules, int moneyItemId)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            if (sourceRules == null || sourceRules.manifest != "854_156383"
                || sourceRules.gem_merge == null || sourceRules.gem_merge.Length == 0)
                throw new ArgumentException("Gem forge rules must come from pinned manifest 854_156383.", nameof(sourceRules));
            rules = sourceRules.gem_merge.OrderBy(value => value.Rank).ToArray();
            storyCount = sourceRules.gem_merge_stories == null ? 0 : sourceRules.gem_merge_stories.Length;
            if (rules.Any(value => value == null || value.Rank <= 1 || value.RequireCount < 0
                || value.NeedMaterialRank <= 0 || value.CostMoney < 0 || value.PercentMininum < 0
                || value.FailedResult < 0 || value.FailedResult > 3
                || value.StepRate == null && value.IsHighRankMerge
                || value.StepRate != null && value.StepRate.Length == 0))
                throw new ArgumentException("Gem forge rules contain invalid fields.", nameof(sourceRules));
            this.moneyItemId = moneyItemId > 0 ? moneyItemId : MoneyItemIdFallback;
            if (session.Save.Player.Equipment.GemForge == null)
                session.Save.Player.Equipment.GemForge = new OfflineGemForgeProgress();
            Normalize(session.Save.Player.Equipment.GemForge);
        }

        public IReadOnlyList<OfflineGemMergeRule> Rules => rules;

        public OfflineGemMergeQuote Quote(int mainItemId, bool inlaid, int availableMainRankLimit = int.MaxValue,
            long mainEquipmentInstanceId = 0, int mainGemSlot = -1)
        {
            Dictionary<string, object> main = equipment.Find("item", mainItemId);
            Dictionary<string, object> gem = equipment.Find("gem", mainItemId);
            if (main == null || gem == null) return null;
            int rank = Number(main, "Rank");
            OfflineGemMergeRule rule = FindRule(rank + 1);
            if (rule == null || rank >= availableMainRankLimit) return null;
            int materialId = FindGemId(Number(gem, "GemType"), rule.NeedMaterialRank);
            if (materialId <= 0) return null;
            int required = rule.RequireCount + 1;
            bool mainCountsAsMaterial = mainItemId == materialId;
            int materialCount = Math.Max(0, required - (mainCountsAsMaterial ? 1 : 0));
            long availableMaterials = AvailableMaterialCount(materialId, mainItemId, inlaid,
                mainEquipmentInstanceId, mainGemSlot);
            int missCount = MissCount(session.Save.Player.Equipment.GemForge, rule.Rank);
            double rate = SuccessRate(rule, missCount,
                session.Save.Player.Equipment.GemForge.GuaranteedStoryIndex);
            return new OfflineGemMergeQuote
            {
                MainItemId = mainItemId,
                ResultItemId = FindGemId(Number(gem, "GemType"), rule.Rank),
                MaterialItemId = materialId,
                MaterialCount = materialCount,
                CostMoney = rule.CostMoney,
                TargetRank = rule.Rank,
                IsHighRank = rule.IsHighRankMerge,
                SuccessRate = rate / 100.0,
                Energy = rule.IsHighRankMerge && rule.PercentMininum > 0
                    ? Math.Round(Math.Min(1.0, (double)missCount / rule.PercentMininum), 4) : 0,
                IsReady = availableMaterials >= materialCount
                    && session.InventoryCount(mainItemId) >= (inlaid ? 0 : 1)
                    && session.InventoryCount(moneyItemId) >= rule.CostMoney
            };
        }

        public OfflineGemMergeReceipt Merge(string receiptId, int mainItemId, bool mainIsInlaid,
            int availableMainRankLimit, Func<double> random01 = null,
            long inlaidEquipmentInstanceId = 0, int inlaidGemSlot = -1,
            IReadOnlyList<OfflineGemMergeMaterialSource> selectedMaterials = null)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return null;
            if (mainIsInlaid && (inlaidEquipmentInstanceId <= 0 || inlaidGemSlot < 0)) return null;
            if (mainIsInlaid && !session.Save.Player.Equipment.Equipped.Any(value =>
                value.InstanceId == inlaidEquipmentInstanceId)) return null;
            OfflineGemMergeQuote quote = Quote(mainItemId, mainIsInlaid, availableMainRankLimit,
                inlaidEquipmentInstanceId, inlaidGemSlot);
            if (quote == null || !quote.IsReady) return null;
            List<OfflineGemMergeMaterialSource> materials = selectedMaterials == null
                ? SelectMaterialSources(quote, mainIsInlaid, inlaidEquipmentInstanceId, inlaidGemSlot)
                : selectedMaterials.ToList();
            if (!ValidMaterialSources(materials, quote, mainIsInlaid,
                inlaidEquipmentInstanceId, inlaidGemSlot)) return null;
            OfflineGemMergeRule rule = FindRule(quote.TargetRank);
            OfflineGemForgeProgress forge = session.Save.Player.Equipment.GemForge;
            int priorMiss = MissCount(forge, rule.Rank);
            double probability = quote.SuccessRate * 100.0;
            long randomState = forge.RandomState == 0 ? DateTime.UtcNow.Ticks ^ mainItemId : forge.RandomState;
            double roll = (random01 == null ? NextRandom(ref randomState) : random01()) * 10000.0;
            if (random01 != null) randomState = forge.RandomState;
            if (double.IsNaN(roll) || roll < 0 || roll >= 10000) return null;
            bool success = roll < probability;
            OfflineGemMergeResultType result = success ? OfflineGemMergeResultType.Success
                : (OfflineGemMergeResultType)rule.FailedResult;
            int nextMiss = success ? 0 : Math.Min(int.MaxValue, priorMiss + 1);
            int storyIndex = -1;
            if (!success && forge.GuaranteedStoryIndex < storyCount)
            {
                bool downStory = result == OfflineGemMergeResultType.DownAndExplode && nextMiss >= rule.PercentMininum;
                bool failStory = result != OfflineGemMergeResultType.Success && nextMiss > rule.PercentMininum;
                if (downStory || failStory) storyIndex = forge.GuaranteedStoryIndex;
            }
            int refund = storyIndex >= 0 && forge.GuaranteedStoryIndex < storyCount ? quote.MaterialCount : 0;
            var merged = new OfflineGemMergeReceipt
            {
                Id = receiptId,
                MainItemId = mainItemId,
                ResultItemId = success ? quote.ResultItemId : mainItemId,
                MaterialItemId = quote.MaterialItemId,
                MaterialCount = quote.MaterialCount,
                CostMoney = quote.CostMoney,
                Result = result,
                PreviousMissCount = priorMiss,
                MissCount = success ? 0 : nextMiss,
                StoryIndex = storyIndex,
                RefundedMaterials = refund,
            };
            ProgressionResult committed = session.ApplyInventoryTransaction("gem-forge:" + receiptId, save =>
            {
                OfflinePlayerProgress player = save.Player;
                OfflineGemForgeProgress state = player.Equipment.GemForge;
                if (state.ClaimedReceipts.Contains(receiptId)) throw new InvalidOperationException("Receipt already applied.");
                foreach (IGrouping<int, OfflineGemMergeMaterialSource> inventoryMaterials in materials
                    .Where(value => !value.IsInlaid).GroupBy(value => value.ItemId))
                    Consume(player, inventoryMaterials.Key, inventoryMaterials.Sum(value => (long)value.Count));
                foreach (OfflineGemMergeMaterialSource inlaidMaterial in materials.Where(value => value.IsInlaid))
                {
                    OfflineEquipmentInstance source = player.Equipment.Instances.FirstOrDefault(value =>
                        value.InstanceId == inlaidMaterial.EquipmentInstanceId);
                    OfflineEquipmentGem gemSource = source == null || source.Gems == null ? null : source.Gems.FirstOrDefault(value =>
                        value.Slot == inlaidMaterial.GemSlot && value.ItemId == inlaidMaterial.ItemId);
                    if (gemSource == null || mainIsInlaid && source.InstanceId == inlaidEquipmentInstanceId
                        && gemSource.Slot == inlaidGemSlot || !player.Equipment.Equipped.Any(value =>
                            value.InstanceId == inlaidMaterial.EquipmentInstanceId))
                        throw new InvalidOperationException("A selected inlaid material gem no longer exists.");
                    source.Gems.Remove(gemSource);
                }
                if (quote.CostMoney > 0) Consume(player, moneyItemId, quote.CostMoney);
                if (success && !mainIsInlaid) Consume(player, mainItemId, 1);
                if (success && mainIsInlaid)
                {
                    OfflineEquipmentInstance instance = player.Equipment.Instances
                        .FirstOrDefault(value => value.InstanceId == inlaidEquipmentInstanceId);
                    OfflineEquipmentGem inlay = instance == null || instance.Gems == null ? null : instance.Gems
                        .FirstOrDefault(value => value.Slot == inlaidGemSlot && value.ItemId == mainItemId);
                    if (inlay == null) throw new InvalidOperationException("The selected inlaid gem no longer exists.");
                    inlay.ItemId = quote.ResultItemId;
                }
                if (success && !mainIsInlaid) Grant(player, quote.ResultItemId, 1);
                if (refund > 0) Grant(player, quote.MaterialItemId, refund);
                state.RandomState = randomState;
                SetMissCount(state, rule.Rank, nextMiss);
                if (success && rule.IsHighRankMerge) SetMissCount(state, rule.Rank, 0);
                if (storyIndex >= 0) state.GuaranteedStoryIndex = Math.Min(storyCount, state.GuaranteedStoryIndex + 1);
                state.ClaimedReceipts.Add(receiptId);
            });
            return committed.IsApplied ? merged : null;
        }

        public static double SuccessRate(OfflineGemMergeRule rule, int missCount, int storyIndex)
        {
            if (rule == null) return 0;
            if (!rule.IsHighRankMerge) return 10000.0;
            if (missCount >= rule.PercentMininum && storyIndex >= 3) return 10000.0;
            int index = Math.Min(rule.StepRate.Length - 1, Math.Min(rule.PercentMininum, missCount));
            return rule.BaseRate + rule.StepRate[index];
        }

        private int FindGemId(int gemType, int rank)
        {
            return equipment.Rows("gem").FirstOrDefault(value => Number(value, "GemType") == gemType
                && Number(equipment.Find("item", Number(value, "ClassId")), "Rank") == rank) is Dictionary<string, object> row
                ? Number(row, "ClassId") : 0;
        }

        private OfflineGemMergeRule FindRule(int rank) => rules.FirstOrDefault(value => value.Rank == rank);

        private long AvailableMaterialCount(int itemId, int mainItemId, bool mainInlaid,
            long mainEquipmentInstanceId, int mainGemSlot)
        {
            HashSet<long> equipped = new HashSet<long>(session.Save.Player.Equipment.Equipped
                .Where(value => value != null).Select(value => value.InstanceId));
            long inventory = session.InventoryCount(itemId) - (!mainInlaid && mainItemId == itemId ? 1 : 0);
            long inlaid = session.Save.Player.Equipment.Instances
                .Where(instance => instance != null && instance.Gems != null && equipped.Contains(instance.InstanceId))
                .SelectMany(instance => instance.Gems.Where(gem => gem != null && gem.ItemId == itemId
                    && !(mainInlaid && instance.InstanceId == mainEquipmentInstanceId && gem.Slot == mainGemSlot)))
                .LongCount();
            return Math.Max(0, inventory) + inlaid;
        }

        private List<OfflineGemMergeMaterialSource> SelectMaterialSources(OfflineGemMergeQuote quote,
            bool mainInlaid, long mainEquipmentInstanceId, int mainGemSlot)
        {
            var result = new List<OfflineGemMergeMaterialSource>();
            long inventory = session.InventoryCount(quote.MaterialItemId)
                - (!mainInlaid && quote.MainItemId == quote.MaterialItemId ? 1 : 0);
            int inventoryCount = (int)Math.Min(quote.MaterialCount, Math.Max(0, inventory));
            if (inventoryCount > 0)
                result.Add(new OfflineGemMergeMaterialSource
                {
                    ItemId = quote.MaterialItemId,
                    Count = inventoryCount
                });
            int remaining = quote.MaterialCount - inventoryCount;
            HashSet<long> equipped = new HashSet<long>(session.Save.Player.Equipment.Equipped
                .Where(value => value != null).Select(value => value.InstanceId));
            foreach (OfflineEquipmentInstance instance in session.Save.Player.Equipment.Instances
                .Where(value => value != null && value.Gems != null && equipped.Contains(value.InstanceId))
                .OrderBy(value => value.InstanceId))
            {
                foreach (OfflineEquipmentGem gem in instance.Gems.Where(value => value != null
                    && value.ItemId == quote.MaterialItemId
                    && !(mainInlaid && instance.InstanceId == mainEquipmentInstanceId
                        && value.Slot == mainGemSlot)).OrderBy(value => value.Slot))
                {
                    if (remaining == 0) return result;
                    result.Add(new OfflineGemMergeMaterialSource
                    {
                        ItemId = gem.ItemId,
                        EquipmentInstanceId = instance.InstanceId,
                        GemSlot = gem.Slot
                    });
                    remaining--;
                }
            }
            return result;
        }

        private static bool ValidMaterialSources(IReadOnlyList<OfflineGemMergeMaterialSource> materials,
            OfflineGemMergeQuote quote, bool mainInlaid, long mainEquipmentInstanceId, int mainGemSlot)
        {
            if (materials == null) return quote.MaterialCount == 0;
            long selectedCount = 0;
            var inlaidSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineGemMergeMaterialSource source in materials)
            {
                if (source == null || source.ItemId != quote.MaterialItemId || source.Count <= 0) return false;
                if (!source.IsInlaid)
                {
                    if (source.EquipmentInstanceId != 0 || source.GemSlot != -1) return false;
                    selectedCount += source.Count;
                    continue;
                }
                if (source.Count != 1 || mainInlaid && source.EquipmentInstanceId == mainEquipmentInstanceId
                    && source.GemSlot == mainGemSlot
                    || !inlaidSlots.Add(source.EquipmentInstanceId + ":" + source.GemSlot)) return false;
                selectedCount++;
            }
            return selectedCount == quote.MaterialCount;
        }

        private static int MissCount(OfflineGemForgeProgress progress, int rank)
        {
            return progress.Ranks.FirstOrDefault(value => value.Rank == rank)?.MissCount ?? 0;
        }

        private static void SetMissCount(OfflineGemForgeProgress progress, int rank, int count)
        {
            OfflineGemForgeRankProgress state = progress.Ranks.FirstOrDefault(value => value.Rank == rank);
            if (state == null) progress.Ranks.Add(new OfflineGemForgeRankProgress { Rank = rank, MissCount = count });
            else state.MissCount = count;
        }

        private static void Normalize(OfflineGemForgeProgress progress)
        {
            if (progress.Ranks == null) progress.Ranks = new List<OfflineGemForgeRankProgress>();
            if (progress.ClaimedReceipts == null) progress.ClaimedReceipts = new List<string>();
            progress.GuaranteedStoryIndex = Math.Max(0, Math.Min(3, progress.GuaranteedStoryIndex));
        }

        private static void Consume(OfflinePlayerProgress player, int itemId, long count)
        {
            if (count == 0) return;
            InventoryOperationResult result = OfflineInventoryOperations.Consume(player, itemId, count,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (!result.IsApplied) throw new InvalidOperationException("Gem forge materials or currency changed before commit.");
        }

        private static void Grant(OfflinePlayerProgress player, int itemId, long count)
        {
            if (count == 0) return;
            InventoryOperationResult result = OfflineInventoryOperations.Grant(player, itemId, count,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (!result.IsApplied) throw new InvalidOperationException("Gem forge result could not be added to inventory.");
        }

        private static int Number(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static double NextRandom(ref long state)
        {
            ulong value = unchecked((ulong)state);
            value ^= value << 13;
            value ^= value >> 7;
            value ^= value << 17;
            state = unchecked((long)value);
            return (value >> 11) * (1.0 / 9007199254740992.0);
        }

        public static OfflineGemForgeRules LoadDefault()
        {
            TextAsset rules = Resources.Load<TextAsset>("Equipment/gem-forge-rules");
            TextAsset manifest = Resources.Load<TextAsset>("Equipment/gem-forge-source-manifest");
            if (rules == null || manifest == null)
                throw new InvalidOperationException("Pinned gem forge rules are not installed in Resources.");
            OfflineGemForgeSourceManifest source = JsonUtility.FromJson<OfflineGemForgeSourceManifest>(manifest.text);
            if (source == null || source.manifest != "854_156383" || source.output == null
                || source.output.path != "Assets/Game/Resources/Equipment/gem-forge-rules.json"
                || source.output.bytes != rules.bytes.Length
                || !string.Equals(Hash(rules.bytes), source.output.sha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Pinned gem forge rules failed source manifest validation.");
            OfflineGemForgeRules parsed = JsonUtility.FromJson<OfflineGemForgeRules>(rules.text);
            if (parsed == null || parsed.manifest != "854_156383" || parsed.gem_merge == null
                || parsed.gem_merge_stories == null || parsed.gem_merge_stories.Length != 3)
                throw new InvalidOperationException("Pinned gem forge rules are invalid.");
            return parsed;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes);
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte value in digest) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
