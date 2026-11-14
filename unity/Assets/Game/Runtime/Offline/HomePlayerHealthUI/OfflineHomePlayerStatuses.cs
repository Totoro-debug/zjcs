using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.HomePlayerStatusRules;
using SourceType = Zjcs.Official.HomePlayerStatusRules.EStatusSourceType;
using PanelSourceType = Zjcs.Official.HomePlayerPanelUI.EStatusSourceType;
using PanelIconType = Zjcs.Official.HomePlayerPanelUI.StatusEffectIconType;

namespace Zjcs.Offline.HomePlayerHealthUI
{
    // A committed-profile owner, shared by status producers and the original
    // HUD. Fresh profiles contain no records; there is no fixture fallback.
    public sealed class OfflineHomePlayerStatuses : IDisposable
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly OfflineHomeStatusConfiguration configuration;
        private readonly Func<DateTime> clock;
        private readonly Dictionary<int, IHomePanelStatusEffect> statuses = new Dictionary<int, IHomePanelStatusEffect>();
        private Dictionary<int, string> observed = new Dictionary<int, string>();
        private bool disposed;
        public IReadOnlyDictionary<int, IHomePanelStatusEffect> Statuses { get { Check(); return statuses; } }
        public event Action<IHomePanelStatusEffect> StatusUpdated;

        public OfflineHomePlayerStatuses(OfflineMainlineProgressSession profile,
            OfflineHomeStatusConfiguration configuration, Func<DateTime> clock = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.clock = clock ?? (() => DateTime.UtcNow);
            SynchronizeCommittedPlayer();
        }

        public ProgressionResult GainStatusEffect(int classId, SourceType source, string eventId)
        {
            Check();
            if (string.IsNullOrWhiteSpace(eventId) || !Enum.IsDefined(typeof(SourceType), source))
                return new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = "A status source and durable event id are required." };
            configuration.Get(classId);
            profile.Reload();
            var result = profile.ApplyCharacterMutation(player =>
            {
                long playerId = player.CharacterProperties.LocalActorIdentities.PlayerId;
                if (playerId <= 0) throw new InvalidOperationException("Initialize the durable player identity before granting statuses.");
                var records = player.StatusEffects.ToDictionary(row => row.ClassId,
                    row => new PlayerStatusEffectDataWrap(row, configuration.Get, clock));
                var manager = new PlayerStatusEffectManager(playerId, records, configuration.Get, clock);
                manager.GainStatusEffect(classId, source);
                player.StatusEffects = records.Values.Select(row => row.Data).ToList();
            }, eventId);
            if (result.IsApplied) SynchronizeCommittedPlayer();
            return result;
        }

        // The caller owns the action that changes the record (e.g. an actual
        // completed fight). This layer supplies atomic persistence, never a
        // guessed activation or server-expiry schedule.
        public ProgressionResult UpdateStatusEffect(int classId, string eventId, Action<PlayerStatusEffectDataWrap> update)
        {
            Check();
            if (string.IsNullOrWhiteSpace(eventId) || update == null)
                return new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = "A status transition and durable event id are required." };
            configuration.Get(classId);
            profile.Reload();
            var result = profile.ApplyCharacterMutationIfChanged(player =>
            {
                var row = player.StatusEffects.FirstOrDefault(value => value.ClassId == classId);
                if (row == null) return false;
                string previous = JsonUtility.ToJson(row);
                long owner = row.PlayerId;
                update(new PlayerStatusEffectDataWrap(row, configuration.Get, clock));
                if (row.ClassId != classId || row.PlayerId != owner)
                    throw new InvalidOperationException("A status transition cannot change its identity.");
                string current = JsonUtility.ToJson(row);
                // A producer may retain its callback argument. Do not let that
                // reference mutate the installed Save after the transaction.
                player.StatusEffects[player.StatusEffects.IndexOf(row)] =
                    JsonUtility.FromJson<PlayerStatusEffectData>(current);
                return previous != current;
            }, eventId);
            if (result.IsApplied) SynchronizeCommittedPlayer();
            return result;
        }

        // Call after a successful external profile transaction/reload. Publish
        // only after installing the complete new dictionary so original UI
        // subscribers always observe the committed set, including removals.
        public void SynchronizeCommittedPlayer()
        {
            Check();
            var next = new Dictionary<int, IHomePanelStatusEffect>();
            var snapshots = new Dictionary<int, string>();
            foreach (var saved in profile.Save.Player.StatusEffects)
            {
                configuration.Get(saved.ClassId);
                var detached = JsonUtility.FromJson<PlayerStatusEffectData>(JsonUtility.ToJson(saved));
                next.Add(saved.ClassId, new StatusView(new PlayerStatusEffectDataWrap(detached, configuration.Get, clock)));
                snapshots.Add(saved.ClassId, JsonUtility.ToJson(saved));
            }
            int[] changed = observed.Keys.Union(snapshots.Keys).Where(id =>
                !observed.TryGetValue(id, out var previous) || !snapshots.TryGetValue(id, out var current) || previous != current).ToArray();
            statuses.Clear();
            foreach (var row in next) statuses.Add(row.Key, row.Value);
            observed = snapshots;
            foreach (int id in changed)
            {
                statuses.TryGetValue(id, out var status);
                StatusUpdated?.Invoke(status);
            }
        }

        private sealed class StatusView : IHomePanelStatusEffect, IHomePanelStatusInfo
        {
            private readonly PlayerStatusEffectDataWrap record;
            public StatusView(PlayerStatusEffectDataWrap record) { this.record = record; }
            public bool IsActive => record.IsActive;
            public PanelSourceType StatusSourceType => (PanelSourceType)(int)record.StatusSourceType;
            public IHomePanelStatusInfo Info => this;
            public DateTime LastGainedTime => record.LastGainedTime;
            public int GetDisplayRemainTimes() => record.GetDisplayRemainTimes();
            public PanelIconType IconType => (PanelIconType)(int)record.Info.IconType;
            public int EntryId => record.Info.EntryId;
            public int ClassId => record.ClassId;
            public int Duration => record.Info.Duration;
            public bool IsReduceOncePerPlay => record.Info.IsReduceOncePerPlay;
        }

        private void Check() { if (disposed) throw new ObjectDisposedException(nameof(OfflineHomePlayerStatuses)); }
        public void Dispose()
        {
            if (disposed) return;
            StatusUpdated = null; statuses.Clear(); observed.Clear(); disposed = true;
        }
    }
}
