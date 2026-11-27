using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public enum OptionalChestOpenCode { Applied, Unchanged, InvalidRequest, InsufficientItems, Failed }
    public sealed class OptionalChestOpenResult
    {
        public OptionalChestOpenCode Code { get; }
        public string Message { get; }
        public IReadOnlyDictionary<int, long> Rewards { get; }
        public bool IsApplied => Code == OptionalChestOpenCode.Applied;
        internal OptionalChestOpenResult(OptionalChestOpenCode code, string message = null,
            IReadOnlyDictionary<int, long> rewards = null)
        { Code = code; Message = message; Rewards = rewards ?? new Dictionary<int, long>(); }
    }

    // Original explicit options/costs replace the open-chest RPC with one local
    // inventory transaction. Random and other use effects remain separate rules.
    public sealed class OfflineOptionalChestService
    {
        private readonly OfficialChestUsePreviewRules rules;
        private readonly OfflineMainlineProgressSession profile;
        public OfflineOptionalChestService(OfficialChestUsePreviewRules rules, OfflineMainlineProgressSession profile)
        { this.rules = rules ?? throw new ArgumentNullException(nameof(rules)); this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); }

        public OptionalChestOpenResult Open(int itemId, int count, IReadOnlyList<int> selected,
            long nowUnixSeconds, string eventId)
        {
            if (count <= 0 || selected == null || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(eventId) || eventId.Length > 160)
                return new OptionalChestOpenResult(OptionalChestOpenCode.InvalidRequest);
            OptionalChestDefinition chest;
            try { chest = rules.OptionalChest(itemId); }
            catch (NotSupportedException error) { return new OptionalChestOpenResult(OptionalChestOpenCode.InvalidRequest, error.Message); }
            if (selected.Count != chest.OptionCount || selected.Distinct().Count() != selected.Count
                || selected.Any(id => !chest.Options.ContainsKey(id)) || !chest.CanUseInBatches && count != 1)
                return new OptionalChestOpenResult(OptionalChestOpenCode.InvalidRequest);
            Dictionary<int, long> costs, rewards;
            try
            {
                costs = chest.Costs.ToDictionary(pair => pair.Key, pair => checked(pair.Value * count));
                rewards = selected.ToDictionary(id => id, id => checked(chest.Options[id] * count));
            }
            catch (OverflowException error) { return new OptionalChestOpenResult(OptionalChestOpenCode.InvalidRequest, error.Message); }
            profile.Reload();
            if (profile.Save.Player.AppliedInventoryEventIds.Contains(eventId))
                return new OptionalChestOpenResult(OptionalChestOpenCode.Unchanged);
            if (costs.Any(pair => profile.InventoryCount(pair.Key) < pair.Value))
                return new OptionalChestOpenResult(OptionalChestOpenCode.InsufficientItems);
            var result = profile.ApplyInventoryTransaction(eventId, save => {
                foreach (var pair in costs)
                    if (!OfflineInventoryOperations.Consume(save.Player, pair.Key, pair.Value, nowUnixSeconds).IsApplied)
                        throw new InvalidOperationException("The selected chest costs are no longer available.");
                foreach (var pair in rewards)
                    if (!OfflineInventoryOperations.Grant(save.Player, pair.Key, pair.Value, nowUnixSeconds).IsApplied)
                        throw new InvalidOperationException("The selected chest rewards could not be granted.");
            }, itemId, count);
            return result.Status == ProgressionResultStatus.Applied
                ? new OptionalChestOpenResult(OptionalChestOpenCode.Applied, rewards: rewards)
                : new OptionalChestOpenResult(result.Status == ProgressionResultStatus.Unchanged
                    ? OptionalChestOpenCode.Unchanged : OptionalChestOpenCode.Failed, result.Message);
        }
    }
}
