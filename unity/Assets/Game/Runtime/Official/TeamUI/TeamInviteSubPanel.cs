using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Cysharp.Threading.Tasks;

namespace Zjcs.Official.TeamUI
{
    // Original list lifecycle and async-state-machine branches.
    public class TeamInviteSubPanel : TeamInviteSubPanelBase, LoopScrollDataSource
    {
        [SerializeField] protected GameObject mEmptyGo;
        [SerializeField] protected WorldMultiLanguageText mEmptyDescText;
        [SerializeField] protected LoopVerticalScrollRect mLoopRect;
        [SerializeField] protected WorldUIDropdown mUISortSelector;
        protected ITeamInvitePlayer mCurrentPlayer;
        protected Func<TeamTargetData> mGetTeamTarget;
        private readonly Dictionary<long, TeamInvitePlayerInfoData> mPlayerDict = new Dictionary<long, TeamInvitePlayerInfoData>();
        private IReadOnlyList<TeamInviteSortType> mSortTypes;
        protected ITeamInviteUiServices services;
        public TeamInviteSortType? SelectSort { get; protected set; }
        public GAction OnRefreshPanelFinish { get; } = new GAction();
        public void Configure(ITeamInviteUiServices value) { services = value; }
        protected long GetCompanionId() => services.CompanionId;
        protected virtual void Awake() { mUISortSelector.OnDropdownUpdate += MUISortSelector_OnDropdownUpdate; }
        public override TeamInviteSortType GetCurSortType() => SelectSort ?? PriorSort ?? DefaultSort;
        private void MUISortSelector_OnDropdownUpdate(int index)
        { SelectSort = mSortTypes[index]; Refresh(); }
        protected override void OnEnable()
        {
            base.OnEnable();
            if (services.RelationshipAvailable) services.RelationRemoved += OnFriendRemoveRelation;
            services.TeamPlayerAdded += MainTeam_OnPlayerAdd;
        }
        protected override void OnDisable()
        {
            PriorSort = DefaultSort;
            mEmptyGo.SetActive(false);
            if (services.ManagerAvailable) services.TeamPlayerAdded -= MainTeam_OnPlayerAdd;
            if (services.RelationshipAvailable) services.RelationRemoved -= OnFriendRemoveRelation;
            base.OnDisable();
        }
        public void RefreshPanel(ITeamInvitePlayer provider, TeamInviteSortType defaultSortType,
            CancellationToken cancellationToken = default)
        {
            SelectSort = null; PriorSort = null;
            SetDefaultSortType(defaultSortType);
            mEmptyDescText.SetKey(provider.GetEmptyDescKey());
            mUISortSelector.Hide();
            mCurrentPlayer = provider;
            Forget(RefreshPlayerAsync(cancellationToken));
        }
        private static async void Forget(Task task)
        {
            try { await task; }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private async Task RefreshPlayerAsync(CancellationToken token)
        {
            RefreshSortItems();
            ResetLoopRect();
            List<TeamInvitePlayerInfoData> result = await mCurrentPlayer.LoadPlayers(TeamTarget, token);
            if (!token.IsCancellationRequested)
            {
                if ((result?.Count ?? 0) > 0)
                    foreach (var info in result)
                        if (!PlayerFilter(info))
                        {
                            PlayerList.Add(info.PlayerId);
                            mPlayerDict.Add(info.PlayerId, info);
                        }
                Refresh();
                OnRefreshPanelFinish.SafeInvoke();
            }
        }
        protected virtual bool PlayerFilter(TeamInvitePlayerInfoData info)
        {
            if (!services.MainPlayerAvailable) return true;
            if (info.PlayerId == services.MainPlayerId) return true;
            if (mExcludePlayers.Contains(info.PlayerId)) return true;
            return false;
        }
        private void ResetLoopRect()
        {
            PlayerList.Clear(); mPlayerDict.Clear();
            mLoopRect.totalCount = PlayerList.Count;
            mLoopRect.RefillCells();
        }
        private void Refresh()
        {
            if (mGetTeamTarget == null) mGetTeamTarget = GetTeamTarget;
            SortPlayers();
            mLoopRect.dataSource = this;
            mLoopRect.totalCount = PlayerList.Count;
            mLoopRect.RefillCells();
            mEmptyGo.SetActive(PlayerList.Count <= 0);
        }
        public override TeamInvitePlayerInfoData GetPlayerInfo(long id)
        { mPlayerDict.TryGetValue(id, out var value); return value; }
        protected virtual void OnFriendRemoveRelation(long id)
        { PlayerList.Remove(id); mPlayerDict.Remove(id); Refresh(); }
        protected virtual void MainTeam_OnPlayerAdd(TeamPanelPlayer info) { }
        private void SortPlayers()
        {
            int index = GetSortIndex();
            if (mSortTypes != null && index >= 0 && index < mSortTypes.Count)
                SortPlayer(PlayerList, mSortTypes[index]);
        }
        protected virtual void SortPlayer(List<long> players, TeamInviteSortType sort)
        {
            bool joint = mGetTeamTarget().TargetCatalog == TeamTargetCatalog.JointStrike;
            long companion = GetCompanionId();
            Func<long, long, int> compare;
            switch (sort)
            {
                case TeamInviteSortType.FriendShip:
                    compare = new TeamInviteComparerBase(GetPlayerInfo, joint, services).SetCompanionId(companion).BaseFriendComparer;
                    break;
                case TeamInviteSortType.CombatRating:
                    compare = new TeamInviteComparerBase(GetPlayerInfo, joint, services).SetCompanionId(companion).BaseCombatRatingComparer;
                    break;
                case TeamInviteSortType.ActivityBuff:
                    int? activity = services.FestivalActivityId;
                    compare = activity == null
                        ? new TeamInviteComparerBase(GetPlayerInfo, joint, services).SetCompanionId(companion).BaseNoDataActivityBuffComparer
                        : new TeamInviteComparerBase(GetPlayerInfo, joint, services).SetCompanionId(companion).SetActivityId(activity.Value).BaseActivityBuffComparer;
                    break;
                default:
                    compare = new TeamInviteComparerBase(GetPlayerInfo, joint, services, sort.ToPropType()).SetCompanionId(companion).BaseElementPropTypeComparer;
                    break;
            }
            players.Sort(compare.Invoke);
        }
        protected virtual void RefreshSortItems()
        {
            mUISortSelector.ItemsKey.Clear();
            mSortTypes = mCurrentPlayer.GetSortTypes();
            mUISortSelector.ItemsKey.AddRange(mSortTypes.Select(it => services.Localize("TeamInviteSortType." + it)));
            mUISortSelector.SetSelectedItemIndex(GetSortIndex());
        }
        public int GetSortIndex()
        {
            if (mSortTypes == null) return 0;
            TeamInviteSortType sort = GetCurSortType();
            for (int i = 0; i < mSortTypes.Count; i++) if (mSortTypes[i] == sort) return i;
            services.Error("TeamInviteSubPanel 在 SortOption 中没有找到 Sort: " + sort);
            return -1;
        }
        public virtual void ProvideData(Transform transform, int index)
        {
            if (index >= 0 && index < PlayerList.Count)
                transform.GetComponent<TeamInvitePlayerItem>().Init(GetPlayerInfo(PlayerList[index]), GetCurSortType(), mGetTeamTarget);
        }
        private TeamTargetData GetTeamTarget() => TeamTarget;
        public async UniTask<Toggle> GetValidRobotAssistanceTg(CancellationToken token)
        {
            int totalCount = PlayerList.Count;
            if (totalCount == 0) return null;
            int originStart = mLoopRect.ItemTypeStart;
            GameObject item = GetItemByIndex(originStart);
            if (item == null || item.GetComponent<TeamInviteRobotItem>() == null) return null;
            for (int index = 0; index < totalCount; index++)
            {
                if (index < mLoopRect.ItemTypeStart || index >= mLoopRect.ItemTypeEnd)
                    await RefillCells(index, token);
                Toggle toggle = GetItemByIndex(index).GetComponent<TeamInviteRobotItem>().GetInvitedToggle();
                if (toggle != null) return toggle;
            }
            await RefillCells(originStart, token);
            return null;
        }
        private UniTask RefillCells(int startItem, CancellationToken token)
        {
            var completion = new UniTaskCompletionSource();
            mLoopRect.RefillCells(startItem, 0f, () => { completion.TrySetResult(); });
            return completion.Task.AttachExternalCancellation(token).Timeout(
                TimeSpan.FromSeconds(3.0), (DelayType)0, (PlayerLoopTiming)8, null);
        }
        // Original LoopScrollRectUtil.GetItemByIndex, including its strict end comparison.
        private GameObject GetItemByIndex(int index)
        {
            if (mLoopRect.content == null) return null;
            if (index < mLoopRect.ItemTypeStart || index > mLoopRect.ItemTypeEnd) return null;
            int position = index - mLoopRect.ItemTypeStart;
            if (position < 0 || position >= mLoopRect.content.childCount) return null;
            Transform child = mLoopRect.content.GetChild(position);
            return child == null ? null : child.gameObject;
        }
        public Button GetNPCAssistanceButton(int npcClassId)
        {
            if (mLoopRect.content.childCount == 0) return null;
            var rows = mLoopRect.content.GetComponentsInChildren<TeamInvitePlayerItem>();
            if (rows == null || rows.Length == 0) return null;
            foreach (var row in rows)
                if (row.PlayerInfo.TargetData is IProfessionInfoNpcTarget npc && npc.NpcClassId == npcClassId)
                    return row.GetIniviteButton();
            return null;
        }
    }
}
