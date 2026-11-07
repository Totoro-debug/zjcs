using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.State;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private const int MainlineAssistPlayType = (int)NpcAssistPlayType.TeamInvite;
        private OfflineNpcAssistanceProgress mainlineAssistProgress;
        private FightOfficialNpcAssistanceCatalog mainlineAssistCatalog;
        private OfficialBattleAssistSelectionView mainlineAssistView;
        private string mainlineAssistEntryId;
        private Action mainlineAssistStart;

        private bool ShouldOfferMainlineAssist(Map10RuntimeEntity entity, Action completed)
        {
            return completed == null && entity != null && FindMap12Stele(entity) == null;
        }

        private void OpenMainlineBattleAssistPicker(Map10RuntimeEntity entity, Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene, Action completed)
        {
            if (mainlineAssistView != null) return;
            mainlineAssistProgress ??= OfflineNpcAssistanceProgress.Open(
                new ProfileSlotCatalog(persistentDataPath), slotId);
            mainlineAssistCatalog ??= OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            OfficialBattleModelCatalog models = OfficialBattleModelCatalog.LoadDefault();
            OfficialBattleNpcPortraitCatalog portraits = OfficialBattleNpcPortraitCatalog.Load();
            FightOfficialNpcInviteRule invite = mainlineAssistCatalog.InviteRule(MainlineAssistPlayType);
            int maximum = Math.Max(0, Math.Min(3, invite.MaxPlayer));
            mainlineAssistEntryId = MainlineAssistKey(entity.PlacementId, stage.Id);
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            int[] existing = mainlineAssistProgress.Selected(MainlineAssistPlayType,
                mainlineAssistEntryId).ToArray();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IReadOnlyList<OfflineNpcAssistCandidate> candidates = OfflineNpcAssistanceCandidates.Build(
                mainlineAssistCatalog, OfflineNpcAssistCandidateMode.TeamInvite,
                MainlineAssistPlayType, progression.Player.Level, friendships,
                progressionSave.StoryCount, npcId => models.HasNpcBattleAnimations(npcId),
                (npcId, cycle, timestamp) => mainlineAssistProgress.AssistCount(
                    MainlineAssistPlayType, npcId, cycle, timestamp), existing, maximum, now, true);
            var rows = candidates.Select(candidate => new OfficialBattleAssistSelectionView.Candidate
            {
                Id = candidate.NpcClassId,
                Name = MainlineAssistName(candidate.NpcClassId),
                Portrait = portraits.Find(candidate.NpcClassId),
                Available = candidate.Available,
                Selected = candidate.Selected,
                FriendshipLevel = candidate.FriendshipLevel,
                FriendshipBlocked = candidate.FriendshipBlocked,
                Reason = MainlineAssistReason(candidate)
            }).ToList();
            IReadOnlyList<int> retained = OfflineNpcAssistanceCandidates.SelectedIds(candidates);
            if (!existing.SequenceEqual(retained))
                mainlineAssistProgress.SetSelection(MainlineAssistPlayType,
                    mainlineAssistEntryId, retained, maximum);

            mainlineAssistStart = () => BeginBattle(entity, stage, roster, scene, completed, null);
            try
            {
                mainlineAssistView = new OfficialBattleAssistSelectionView(hud.ui.OverlayRoot,
                    rows, maximum, SelectMainlineAssistCandidate,
                    ConfirmMainlineAssistSelection, CloseMainlineAssistPicker);
            }
            catch
            {
                mainlineAssistStart = null;
                mainlineAssistEntryId = null;
                throw;
            }
        }

        private void SelectMainlineAssistCandidate(int npcId, bool selected)
        {
            if (mainlineAssistProgress == null || string.IsNullOrEmpty(mainlineAssistEntryId)) return;
            var rows = BuildMainlineAssistCandidates(out int maximum);
            OfficialBattleAssistSelectionView.Candidate candidate = rows.Single(value => value.Id == npcId);
            List<int> next = mainlineAssistProgress.Selected(MainlineAssistPlayType,
                mainlineAssistEntryId).ToList();
            if (selected && !next.Contains(npcId))
            {
                if (!candidate.Available) return;
                if (next.Count >= maximum)
                {
                    hud.ShowOfficialNotice("最多选择 " + maximum + " 名助战伙伴");
                    return;
                }
                next.Add(npcId);
            }
            else if (!selected) next.Remove(npcId);
            mainlineAssistProgress.SetSelection(MainlineAssistPlayType, mainlineAssistEntryId,
                next, maximum);
            mainlineAssistView?.Refresh(BuildMainlineAssistCandidates(out maximum), maximum);
        }

        private List<OfficialBattleAssistSelectionView.Candidate> BuildMainlineAssistCandidates(
            out int maximum)
        {
            if (mainlineAssistCatalog == null || mainlineAssistProgress == null
                || string.IsNullOrEmpty(mainlineAssistEntryId))
                throw new InvalidOperationException("Mainline NPC assistance is not initialized.");
            OfficialBattleModelCatalog models = OfficialBattleModelCatalog.LoadDefault();
            OfficialBattleNpcPortraitCatalog portraits = OfficialBattleNpcPortraitCatalog.Load();
            FightOfficialNpcInviteRule invite = mainlineAssistCatalog.InviteRule(MainlineAssistPlayType);
            maximum = Math.Max(0, Math.Min(3, invite.MaxPlayer));
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            int[] existing = mainlineAssistProgress.Selected(MainlineAssistPlayType,
                mainlineAssistEntryId).ToArray();
            IReadOnlyList<OfflineNpcAssistCandidate> candidates = OfflineNpcAssistanceCandidates.Build(
                mainlineAssistCatalog, OfflineNpcAssistCandidateMode.TeamInvite,
                MainlineAssistPlayType, progression.Player.Level, friendships,
                progressionSave.StoryCount, npcId => models.HasNpcBattleAnimations(npcId),
                (npcId, cycle, timestamp) => mainlineAssistProgress.AssistCount(
                    MainlineAssistPlayType, npcId, cycle, timestamp), existing, maximum,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(), true);
            return candidates.Select(candidate => new OfficialBattleAssistSelectionView.Candidate
            {
                Id = candidate.NpcClassId,
                Name = MainlineAssistName(candidate.NpcClassId),
                Portrait = portraits.Find(candidate.NpcClassId),
                Available = candidate.Available,
                Selected = candidate.Selected,
                FriendshipLevel = candidate.FriendshipLevel,
                FriendshipBlocked = candidate.FriendshipBlocked,
                Reason = MainlineAssistReason(candidate)
            }).ToList();
        }

        private void ConfirmMainlineAssistSelection()
        {
            Action start = mainlineAssistStart;
            CloseMainlineAssistPicker();
            try
            {
                start?.Invoke();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                CancelMainlineBattleAssist(0);
                if (hud != null) hud.ShowOfficialNotice("伙伴助战无法进入战斗");
            }
        }

        private void CloseMainlineAssistPicker()
        {
            mainlineAssistView?.Dispose();
            mainlineAssistView = null;
            mainlineAssistStart = null;
        }

        private static string MainlineAssistReason(OfflineNpcAssistCandidate candidate)
        {
            switch (candidate.BlockReason)
            {
                case OfflineNpcAssistBlockReason.MissingBattleModel: return "本地战斗动画未安装";
                case OfflineNpcAssistBlockReason.NotEncountered: return "尚未相识";
                case OfflineNpcAssistBlockReason.Condition: return "未满足助战条件";
                case OfflineNpcAssistBlockReason.InviteLimit: return "今日助战次数已用完";
                default: return string.Empty;
            }
        }

        private string MainlineAssistName(int npcClassId)
        {
            string key = mainlineAssistCatalog.NpcNameKey(npcClassId);
            string localized = WorldMultiLanguageText.Localize?.Invoke(key);
            return string.IsNullOrWhiteSpace(localized) || localized == key
                ? "伙伴 " + npcClassId.ToString() : localized;
        }

        private string MainlineAssistKey(int placementId, int stageId) =>
            "map" + mapClassId.ToString() + ":" + placementId.ToString()
                + ":stage:" + stageId.ToString();

        private int[] ResolveMainlineAssistSelection(Map10RuntimeEntity entity,
            Map10RuntimeStage stage, FightBattleCheckpoint resume, bool allowAssist)
        {
            if (!allowAssist || entity == null || FindMap12Stele(entity) != null)
                return Array.Empty<int>();
            mainlineAssistProgress ??= OfflineNpcAssistanceProgress.Open(
                new ProfileSlotCatalog(persistentDataPath), slotId);
            mainlineAssistCatalog ??= OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            string entryId = MainlineAssistKey(entity.PlacementId, stage.Id);
            if (resume != null)
            {
                int[] restored = resume.Units.Where(value => value != null && value.NpcClassId > 0)
                    .Select(value => value.NpcClassId).ToArray();
                OfflineNpcAssistBattle active = mainlineAssistProgress.ActiveBattle;
                if (active != null && (active.playType != MainlineAssistPlayType
                    || active.entryId != entryId || active.stageId != stage.Id
                    || !(active.npcClassIds ?? new List<int>()).SequenceEqual(restored)))
                    throw new InvalidOperationException("主线战斗助战快照与 checkpoint 不一致。");
                if (active == null && restored.Length > 0)
                    throw new InvalidOperationException("主线战斗 checkpoint 缺少已提交的助战快照。");
                mainlineAssistEntryId = entryId;
                return restored;
            }

            int[] selected = mainlineAssistProgress.Selected(MainlineAssistPlayType, entryId).ToArray();
            ValidateMainlineAssistSelection(selected);
            mainlineAssistEntryId = entryId;
            return selected;
        }

        private void ValidateMainlineAssistSelection(IReadOnlyCollection<int> selected)
        {
            if (selected == null || selected.Count == 0) return;
            FightOfficialNpcInviteRule invite = mainlineAssistCatalog.InviteRule(MainlineAssistPlayType);
            int maximum = Math.Max(0, Math.Min(3, invite.MaxPlayer));
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            OfficialBattleModelCatalog models = OfficialBattleModelCatalog.LoadDefault();
            IReadOnlyList<OfflineNpcAssistCandidate> candidates = OfflineNpcAssistanceCandidates.Build(
                mainlineAssistCatalog, OfflineNpcAssistCandidateMode.TeamInvite,
                MainlineAssistPlayType, progression.Player.Level, friendships,
                progressionSave.StoryCount, npcId => models.HasNpcBattleAnimations(npcId),
                null, selected, maximum, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), false);
            var available = new HashSet<int>(candidates.Where(value => value.Available)
                .Select(value => value.NpcClassId));
            if (selected.Count > maximum || selected.Any(value => !available.Contains(value)))
                throw new InvalidOperationException("所选主线助战伙伴当前不满足官方资格或队伍上限。");
        }

        private void CommitMainlineAssistSelection(Map10RuntimeEntity entity,
            Map10RuntimeStage stage, IReadOnlyCollection<int> selected)
        {
            if (selected == null || selected.Count == 0) return;
            FightOfficialNpcInviteRule invite = mainlineAssistCatalog.InviteRule(MainlineAssistPlayType);
            OfflineNpcAssistBattle committed = mainlineAssistProgress.CommitBattle(
                MainlineAssistPlayType, MainlineAssistKey(entity.PlacementId, stage.Id), stage.Id,
                Math.Max(0, Math.Min(3, invite.MaxPlayer)), invite.OneNpcMaxInviteCount,
                invite.MaxInviteCount, (NpcAssistRefreshCycle)invite.RefreshCycle,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (committed == null) throw new InvalidOperationException("主线助战事务未提交。");
        }

        private void FinishMainlineBattleAssist(int stageId)
        {
            OfflineNpcAssistBattle active = mainlineAssistProgress?.ActiveBattle;
            if (active == null || active.playType != MainlineAssistPlayType
                || stageId > 0 && active.stageId != stageId)
                return;
            mainlineAssistProgress.FinishBattle(active.playType, active.entryId, stageId);
        }

        private void CancelMainlineBattleAssist(int stageId)
        {
            OfflineNpcAssistBattle active = mainlineAssistProgress?.ActiveBattle;
            if (active == null || active.playType != MainlineAssistPlayType
                || stageId > 0 && active.stageId != stageId)
                return;
            mainlineAssistProgress.CancelBattle(active.playType, active.entryId, stageId);
        }

        private void AddMainlineAssistNpcUnits(FightBattleCheckpoint checkpoint,
            Map10RuntimeBattleScene scene, Map10RuntimeFactionInfo playerFaction, int moveMask,
            IReadOnlyList<int> selected)
        {
            if (selected == null || selected.Count == 0) return;
            if (playerFaction?.EntryPosList == null || playerFaction.EntryPosList.Length == 0)
                throw new InvalidDataException("主线战斗场景没有玩家入口位置。");
            Map10RuntimePoint entry = playerFaction.EntryPosList[0];
            var occupied = new HashSet<FightGridPoint>(checkpoint.Units.Select(value =>
                new FightGridPoint(value.X, value.Y)));
            var positions = (scene.FightGridGroundComponent.PosInfos ?? Array.Empty<Map10RuntimeBattleCell>())
                .Where(value => value != null && value.Height < 999)
                .Select(value => new FightGridPoint(value.Pos.X, value.Pos.Y)).Distinct()
                .OrderBy(value => Math.Abs(value.X - entry.X) + Math.Abs(value.Y - entry.Y))
                .ThenBy(value => value.Y).ThenBy(value => value.X)
                .Where(value => checkpoint.Grid.CanStand(value, moveMask, occupied))
                .Take(selected.Count).ToArray();
            if (positions.Length != selected.Count)
                throw new InvalidDataException("主线战斗场景没有足够的伙伴入口位置。");
            for (int index = 0; index < selected.Count; index++)
            {
                FightGridPoint position = positions[index];
                FightBattleUnitState unit = mainlineAssistCatalog.CreateTeamInviteUnit(
                    checkpoint.Units.Count + 1, 1, Math.Max(1, progression.Player.Level),
                    Math.Max(0, progression.Player.Rank), Math.Max(0, progression.Player.SubRank),
                    position.X, position.Y, moveMask, selected[index]);
                checkpoint.Units.Add(unit);
                occupied.Add(position);
            }
        }

        private void DisposeMainlineBattleAssistPicker()
        {
            CloseMainlineAssistPicker();
            mainlineAssistEntryId = null;
        }
    }
}
