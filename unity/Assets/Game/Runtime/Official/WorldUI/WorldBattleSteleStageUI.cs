using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // BattleSteleWorldStageDialog, with ECS/team/player-play-state services
    // supplied by the local map host. Serialized references retain prefab paths.
    public sealed class WorldBattleSteleStageUI : WorldInitializedUI, IStageUI
    {
        public Transform mRewardSlot;
        public Button mTeamBtn, mBattleBtn;
        public WorldMultiLanguageText mTitleText, mRecommendCombatRatingText, mCurrentCombatRatingText,
            mRankSuppressionText, mTeamInviteText;
        public WorldStateSwitcher<bool> mTeamStateSwitcher, mRankSuppressionSwitcher;
        private WorldStageUiContext stage;
        private WorldBattleSteleContext info;

        private void Awake()
        {
            mTeamBtn.onClick.AddListener(OnClickTeamBtn);
            mBattleBtn.onClick.AddListener(OnClickBattleBtn);
        }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            info.SubscribeTeamChanges?.Invoke(RefreshTeamMemberCount);
        }

        protected override void OnDisableAndInited()
        {
            info.UnsubscribeTeamChanges?.Invoke(RefreshTeamMemberCount);
            base.OnDisableAndInited();
        }

        public void Init(WorldStageUiContext context) => InitWrapper(() =>
        {
            stage = context;
            info = context.BattleStele ?? throw new ArgumentException("Original stele UI context is missing.");
            info.PrepareRankSuppression?.Invoke();
            mRankSuppressionSwitcher.SetState(info.RankSuppressed);
            mTeamStateSwitcher.SetState(info.SupportTeam);
            info.PrepareTeamTarget?.Invoke();
            RefreshTeamMemberCount();
            mTitleText.SetKey("stage_" + info.StageId + "_name");
            mRecommendCombatRatingText.Format(info.Compact(info.RecommendCombatRating));
            mRankSuppressionText.Format(info.Compact(info.RecommendCombatRating));
            info.PrepareCombatRating?.Invoke();
            mCurrentCombatRatingText.SetKey("ui_battle_stele_current_combat_rating",
                ColorUtility.ToHtmlStringRGBA(info.CombatRatingColor), info.Compact(info.CombatRating));
            stage.ShowRewardPreview(mRewardSlot);
        });

        // The offline team service invokes this after the same add/remove/update
        // events handled by the original dialog; it queries the current roster.
        public void RefreshTeamMemberCount()
        {
            var count = info.TeamMemberCount();
            mTeamInviteText.Format(count.members, count.maximum);
        }

        private void OnClickTeamBtn() => info.OpenTeam();

        private void OnClickBattleBtn()
        {
            if (info.SupportTeam)
            {
                var count = info.TeamMemberCount();
                if (count.members < count.maximum)
                {
                    info.Confirm("ui_battle_stele_team_member_count_tip", StartBattle);
                    return;
                }
            }
            else if (info.HasTeam())
            {
                info.Notify("ui_battle_stele_not_support_team_tip");
                return;
            }
            StartBattle();
        }

        private void StartBattle() => info.PlayFightAction(info.StageId, () =>
        {
            stage.StartStage(0);
            stage.Close();
        });
    }

    public sealed class WorldBattleSteleContext
    {
        public int StageId;
        public bool SupportTeam, RankSuppressed;
        public long RecommendCombatRating, CombatRating;
        public Color CombatRatingColor;
        public Func<long, string> Compact;
        public Func<(int members, int maximum)> TeamMemberCount;
        public Func<bool> HasTeam;
        public Action OpenTeam;
        public Action<string, Action> Confirm;
        public Action<string> Notify;
        public Action<int, Action> PlayFightAction;
        public Action<Action> SubscribeTeamChanges, UnsubscribeTeamChanges;
        public Action PrepareRankSuppression, PrepareTeamTarget, PrepareCombatRating;
    }
}
