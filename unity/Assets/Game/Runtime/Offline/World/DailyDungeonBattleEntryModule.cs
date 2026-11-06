using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Offline.World
{
    /// <summary>
    /// Owns the consistency boundary shared by daily dungeon, private dungeon and
    /// daily challenge battle entries. Presentation supplies only the mount and
    /// persistence adapters; this module keeps the official commit/mount/save
    /// order and the rollback rule in one place.
    /// </summary>
    public sealed class DailyDungeonBattleEntryModule
    {
        public sealed class PreparedEntry
        {
            internal PreparedEntry(int stageId, int assistPlayType, string entryId, bool restore,
                bool allowFinishedRestore, FightBattleCheckpoint checkpoint, int[] selectedNpcIds,
                OfflineNpcAssistBattle activeAssist)
            {
                StageId = stageId;
                AssistPlayType = assistPlayType;
                EntryId = entryId;
                Restore = restore;
                AllowFinishedRestore = allowFinishedRestore;
                Checkpoint = checkpoint;
                SelectedNpcIds = selectedNpcIds;
                ActiveAssist = activeAssist;
            }

            public int StageId { get; }
            public int AssistPlayType { get; }
            public string EntryId { get; }
            public bool Restore { get; }
            public bool AllowFinishedRestore { get; }
            public FightBattleCheckpoint Checkpoint { get; }
            public IReadOnlyList<int> SelectedNpcIds { get; }
            public OfflineNpcAssistBattle ActiveAssist { get; }
        }

        public PreparedEntry Prepare(int stageId, int defaultStageType, int assistPlayType,
            string entryId, bool restore, FightBattleCheckpoint checkpoint,
            IReadOnlyList<int> selectedNpcIds, OfflineNpcAssistBattle activeAssist,
            bool allowFinishedRestore)
        {
            if (stageId <= 0) throw new ArgumentOutOfRangeException(nameof(stageId));
            if (defaultStageType <= 0) throw new ArgumentOutOfRangeException(nameof(defaultStageType));
            if (assistPlayType <= 0) throw new ArgumentOutOfRangeException(nameof(assistPlayType));
            if (string.IsNullOrWhiteSpace(entryId)) throw new ArgumentException(
                "A battle assistance entry identity is required.", nameof(entryId));
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.StageId != stageId)
                throw new InvalidOperationException("Saved battle checkpoint does not match the active stage.");
            if (restore && checkpoint.Finished && !allowFinishedRestore)
                throw new InvalidOperationException("A finished checkpoint cannot be restored for this battle entry.");
            if (!restore && checkpoint.Finished)
                throw new InvalidOperationException("A new battle entry cannot use a finished checkpoint.");
            if (checkpoint.StageType == 0 && !checkpoint.Finished)
                checkpoint.StageType = defaultStageType;

            if (activeAssist != null && (activeAssist.playType != assistPlayType
                || activeAssist.entryId != entryId || activeAssist.stageId != stageId))
                throw new InvalidOperationException("另一场 NPC 助战战斗尚未结束。");

            int[] checkpointNpcIds = checkpoint.Units == null ? Array.Empty<int>()
                : checkpoint.Units.Where(unit => unit != null && unit.NpcClassId > 0)
                    .Select(unit => unit.NpcClassId).ToArray();
            int[] selected = selectedNpcIds == null ? Array.Empty<int>() : selectedNpcIds.ToArray();
            if (restore)
            {
                selected = checkpointNpcIds;
                if (activeAssist != null && !SameIds(activeAssist.npcClassIds, selected))
                    throw new InvalidOperationException("助战快照与战斗 checkpoint 不一致。");
                if (activeAssist == null && selected.Length > 0 && !checkpoint.Finished)
                    throw new InvalidOperationException("战斗 checkpoint 缺少已提交的助战快照。");
            }
            else if (activeAssist != null && !SameIds(activeAssist.npcClassIds, selected))
                throw new InvalidOperationException("已提交的助战快照与本次战斗选择不一致。");

            return new PreparedEntry(stageId, assistPlayType, entryId, restore,
                allowFinishedRestore, checkpoint, selected, activeAssist);
        }

        /// <summary>
        /// Performs the invariant order: submit selected assists, mount the
        /// original battle, then persist its first checkpoint. A newly submitted
        /// assist transaction is cancelled when either later operation fails.
        /// </summary>
        public void Enter(PreparedEntry prepared, FightOfficialNpcInviteRule inviteRule,
            OfflineNpcAssistanceProgress assistProgress, int maxPlayer, bool commitEmptySelection,
            long nowUnixSeconds, Func<FightBattleCheckpoint, FightBattleCheckpoint> mount,
            Func<string, bool> saveCheckpoint, Action disposeBattle)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            if (inviteRule == null) throw new ArgumentNullException(nameof(inviteRule));
            if (assistProgress == null) throw new ArgumentNullException(nameof(assistProgress));
            if (maxPlayer < 0) throw new ArgumentOutOfRangeException(nameof(maxPlayer));
            if (nowUnixSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds));
            if (mount == null) throw new ArgumentNullException(nameof(mount));
            if (saveCheckpoint == null) throw new ArgumentNullException(nameof(saveCheckpoint));
            if (disposeBattle == null) throw new ArgumentNullException(nameof(disposeBattle));

            bool committed = false;
            try
            {
                bool shouldCommit = !prepared.Restore && prepared.ActiveAssist == null
                    && (commitEmptySelection || prepared.SelectedNpcIds.Count > 0);
                if (shouldCommit)
                {
                    assistProgress.CommitBattle(prepared.AssistPlayType, prepared.EntryId,
                        prepared.StageId, maxPlayer, inviteRule.OneNpcMaxInviteCount,
                        inviteRule.MaxInviteCount, (NpcAssistRefreshCycle)inviteRule.RefreshCycle,
                        nowUnixSeconds);
                    committed = true;
                }

                FightBattleCheckpoint mountedCheckpoint = mount(prepared.Checkpoint);
                if (mountedCheckpoint == null)
                    throw new InvalidOperationException("Battle mounting did not return a checkpoint.");
                string serialized = FightBattleCheckpointSerializer.Save(mountedCheckpoint);
                if (!saveCheckpoint(serialized))
                    throw new InvalidOperationException("战斗 checkpoint 保存失败。");
            }
            catch
            {
                if (committed)
                    assistProgress.CancelBattle(prepared.AssistPlayType, prepared.EntryId, prepared.StageId);
                disposeBattle();
                throw;
            }
        }

        private static bool SameIds(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            IReadOnlyList<int> a = left ?? Array.Empty<int>();
            IReadOnlyList<int> b = right ?? Array.Empty<int>();
            return a.SequenceEqual(b);
        }
    }
}
