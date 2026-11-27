using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Zjcs.Official.TreasureCultivation;

namespace Zjcs.Offline.Progression
{
    public sealed class TreasureCultivationResult
    {
        public ProgressionResultStatus Status { get; }
        public int? ErrorCode { get; }
        public string Message { get; }
        public IReadOnlyDictionary<int, long> Costs { get; }
        public bool IsApplied => Status == ProgressionResultStatus.Applied;

        internal TreasureCultivationResult(ProgressionResultStatus status, int? errorCode = null,
            string message = null, Dictionary<int, long> costs = null)
        {
            Status = status; ErrorCode = errorCode; Message = message ?? string.Empty;
            Costs = new ReadOnlyDictionary<int, long>(costs == null
                ? new Dictionary<int, long>() : new Dictionary<int, long>(costs));
        }
    }

    public sealed partial class OfflineTreasureService
    {
        public event Action<TreasureItem> OnTreasureActive;
        public event Action<TreasureItem, bool> OnTreasureRefresh;
        public event Action<TreasureItem, int, int> OnTreasureUpgradeStar;

        public TreasureCultivationResult ActivateTreasure(int id, string receipt, long nowUnixSeconds)
            => Cultivate(id, receipt, nowUnixSeconds, Cultivation.Activate, false, false);

        public TreasureCultivationResult MergeTreasure(int id, string receipt, long nowUnixSeconds)
            => Cultivate(id, receipt, nowUnixSeconds, Cultivation.Merge, false, false);

        // Ordinary promotion and awakening both use the original upgrade request.
        public TreasureCultivationResult UpgradeTreasure(int id, bool useCommonItem, bool autoMergeMaterial,
            string receipt, long nowUnixSeconds)
            => Cultivate(id, receipt, nowUnixSeconds, Cultivation.Upgrade, useCommonItem, autoMergeMaterial);

        private enum Cultivation { Activate, Merge, Upgrade }

        private TreasureCultivationResult Cultivate(int id, string receipt, long nowUnixSeconds,
            Cultivation operation, bool useCommonItem, bool autoMergeMaterial)
        {
            if (string.IsNullOrWhiteSpace(receipt) || receipt.Length > 160
                || nowUnixSeconds < 0 || nowUnixSeconds > 253402300799L)
                return new TreasureCultivationResult(ProgressionResultStatus.InvalidState,
                    message: "A transaction receipt and valid Unix time are required.");

            var notifications = new PendingTreasureNotifications(this);
            Dictionary<int, long> payment = null;
            int errorCode = 0;
            ProgressionResult committed;
            try
            {
                session.Reload();
                if (session.Save.Player.AppliedInventoryEventIds.Contains(receipt))
                    return new TreasureCultivationResult(ProgressionResultStatus.Unchanged);
                committed = session.ApplyInventoryTransaction(receipt, next =>
                {
                    var player = new TransactionPlayer(this, next.Player, nowUnixSeconds);
                    Dictionary<int, long> requested;
                    if (operation == Cultivation.Activate)
                    {
                        errorCode = Checks.CanActivateTreasure(id, player);
                        requested = errorCode == 0
                            ? GetUpgrade(player.GetItemById(id).Info.Quality, player.GetItemById(id).State.Star).Materials : null;
                    }
                    else if (operation == Cultivation.Merge)
                    {
                        errorCode = Checks.CanMergeTreasure(id, player);
                        requested = errorCode == 0 ? GetMergeMaterials(id) : null;
                    }
                    else
                    {
                        errorCode = Checks.GetTreasureNextLevelUpgradeMaterial(id, player, true, useCommonItem, out requested);
                        if (errorCode == 0)
                            errorCode = Checks.CanTreasureUpgrade(id, requested, player, autoMergeMaterial);
                    }
                    if (errorCode != 0) throw new InvalidOperationException("Original treasure rule rejected the request.");

                    payment = Checks.GetTreasureUpgradeMaterial(new Dictionary<int, long>(requested),
                        operation == Cultivation.Upgrade && autoMergeMaterial, player);
                    foreach (var cost in payment)
                        if (!OfflineInventoryOperations.Consume(next.Player, cost.Key, cost.Value, nowUnixSeconds).IsApplied)
                            throw new InvalidOperationException("Treasure material debit failed.");

                    if (operation == Cultivation.Merge)
                    {
                        bool existingItem = player.GetItemById(id) != null;
                        if (!OfflineInventoryOperations.Grant(next.Player, id, 1, nowUnixSeconds).IsApplied)
                            throw new InvalidOperationException("Treasure item grant failed.");
                        var parameters = next.Player.CharacterProperties.Treasures;
                        // Parameters belong to an actual item. A new item cannot inherit an orphaned record.
                        if (!existingItem) parameters.RemoveAll(row => row.ClassId == id);
                        if (!parameters.Any(row => row.ClassId == id))
                            parameters.Add(new OfflineCharacterTreasure { ClassId = id,
                                Star = 0, HitCount = catalog.GetTreasure(id).InitialHitCount });
                    }
                    var item = player.GetItemById(id);
                    var transition = new OfficialTreasureStateTransitions(notifications);
                    if (operation == Cultivation.Upgrade) transition.UpgradeTreasureStar(item);
                    else transition.TreasureActivate(item);
                    var saved = next.Player.CharacterProperties.Treasures.First(row => row.ClassId == id);
                    saved.Active = item.State.Active;
                    saved.Star = item.State.Star;
                    saved.HitCount = item.State.HitCount;
                });
            }
            catch (IOException exception)
            {
                return new TreasureCultivationResult(ProgressionResultStatus.InvalidState, message: exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return new TreasureCultivationResult(ProgressionResultStatus.InvalidState, message: exception.Message);
            }
            if (errorCode != 0)
                return new TreasureCultivationResult(ProgressionResultStatus.NotReady, errorCode);
            if (!committed.IsApplied)
                return new TreasureCultivationResult(committed.Status, message: committed.Message);
            // Observers see only durable state. Rejections and replays produce no success events.
            notifications.Deliver();
            return new TreasureCultivationResult(committed.Status, 0, costs: payment);
        }

        private sealed class TransactionPlayer : ITreasureCultivationPlayer
        {
            private readonly OfflineTreasureService owner;
            private readonly OfflinePlayerProgress player;
            private readonly DateTime now;
            internal TransactionPlayer(OfflineTreasureService owner, OfflinePlayerProgress player, long unixSeconds)
            { this.owner = owner; this.player = player; now = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime; }
            public TreasureItem GetItemById(int id) => owner.SnapshotItem(player, id);
            public long GetItemAmount(int id)
                => OfflineItemAutoIncrease.Count(player.Inventory.FirstOrDefault(row => row.ItemId == id), now);
            public bool HasItem(int id, long count) => GetItemAmount(id) >= count;
            public bool HasItems(Dictionary<int, long> requested)
                => requested.All(row => HasItem(row.Key, row.Value));
        }

        private sealed class PendingTreasureNotifications : ITreasureTransitionNotifications
        {
            private readonly OfflineTreasureService owner;
            private readonly List<Action> pending = new List<Action>();
            internal PendingTreasureNotifications(OfflineTreasureService owner) { this.owner = owner; }
            public void Active(TreasureItem item) => pending.Add(() => owner.OnTreasureActive?.Invoke(item));
            public void Refresh(TreasureItem item, bool isNew) => pending.Add(() => owner.OnTreasureRefresh?.Invoke(item, isNew));
            public void UpgradeStar(TreasureItem item, int previous, int current)
                => pending.Add(() => owner.OnTreasureUpgradeStar?.Invoke(item, previous, current));
            internal void Deliver() { foreach (var notification in pending) notification(); }
        }
    }
}
