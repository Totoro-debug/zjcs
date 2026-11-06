using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original battle content. The local adapter supplies committed
    // participation/reward data and the approved independent checkpoint HP.
    public sealed class ActivityDomainBossPanelBattleContent : WorldInitializedUI
    {
        public enum LayerStatus { Lock, Unlock, Killed, LockTime }
        public enum AwardStatus { Lock, CanReceive, Received }
        [SerializeField] private WorldShowRemainTimeInfo mUnlockRemainTimeInfo;
        [SerializeField] private MonsterUI mMonsterUI;
        [SerializeField] private Button mMonsterInfoBtn, mReceiveKillAwardBtn;
        [SerializeField] private Transform mChallengeAwardSlot, mKillAwardSlot;
        [SerializeField] private Slider mHpSlider;
        [SerializeField] private WorldMultiLanguageText mHpText;
        [SerializeField] private float mHpSliderOffset;
        [SerializeField] private WorldStateSwitcher<LayerStatus> mLayerStateSwitcher;
        [SerializeField] private TipDotNodeLinker mLinker;
        [SerializeField] private ActivityDomainBossTeamButton mTeamBtn;
        [SerializeField] private Button mChallengeBtn, mGoToMapBtn;
        [SerializeField] private string mGoToMapNavigateKey;
        [SerializeField] private GameObject mClaimableEffectGo;
        [SerializeField] private WorldMultiLanguageText mChallengeCountText;
        [SerializeField] private WorldStateSwitcher<bool> mChallengeStateSwitcher;
        private IActivityDomainBossPageBindings bindings;
        private ActivityDomainBossPageState state;
        private int mActivityId, mDifficulty;
        private Coroutine unlock;

        public void Configure(IActivityDomainBossPageBindings value)
            => bindings = value ?? throw new ArgumentNullException(nameof(value));
        public void Init(int activityId, int difficulty) => InitWrapper(() =>
        { mActivityId = activityId; mDifficulty = difficulty; state = bindings.Read(activityId, difficulty); });
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            mMonsterInfoBtn.onClick.AddListener(OnClickMonsterInfoBtn);
            mReceiveKillAwardBtn.onClick.AddListener(OnClickReceiveKillAwardBtn);
            mChallengeBtn.onClick.AddListener(OnChallengeBtn);
            mGoToMapBtn.onClick.AddListener(OnGoToMapBtn);
            mMonsterUI.Init(state.MonsterId);
            mTeamBtn.Init(bindings.Team(mActivityId));
            RefreshContent();
        }
        protected override void OnDisableAndInited()
        {
            mMonsterInfoBtn.onClick.RemoveListener(OnClickMonsterInfoBtn);
            mReceiveKillAwardBtn.onClick.RemoveListener(OnClickReceiveKillAwardBtn);
            mChallengeBtn.onClick.RemoveListener(OnChallengeBtn);
            mGoToMapBtn.onClick.RemoveListener(OnGoToMapBtn);
            ClearUnlockWait();
            base.OnDisableAndInited();
        }
        private void RefreshContent()
        {
            LayerStatus status = GetStatus();
            mLayerStateSwitcher.SetState(status);
            if (status == LayerStatus.LockTime)
            {
                mUnlockRemainTimeInfo.Init(state.NextLogicDayResetTime);
                ClearUnlockWait();
                unlock = StartCoroutine(WaitUnlockTime(state.NextLogicDayResetTime));
            }
            float ratio = mDifficulty < state.Difficulty ? 0f
                : mDifficulty != state.Difficulty ? 1f : state.HpRatio;
            ratio = Mathf.Clamp01(ratio);
            mHpText.Format(WorldBattleReportNumberFormat.Percent(ratio));
            mHpSlider.value = ratio;
            mChallengeCountText.Format(Math.Max(state.MaxDailyChallengeCount - state.DailyChallengeCount, 0),
                state.MaxDailyChallengeCount);
            mChallengeStateSwitcher.SetState(state.DailyChallengeCount >= state.MaxDailyChallengeCount);
            ShowAward();
        }
        private LayerStatus GetStatus()
        {
            int difficulty = state.Difficulty, maximum = state.Difficulties.Max();
            if (mDifficulty < difficulty - 1 && mDifficulty < maximum) return LayerStatus.Killed;
            if (mDifficulty == difficulty - 1)
                return (state.DifficultyLimited & state.CanChallenge) || mDifficulty == maximum
                    ? LayerStatus.Unlock : LayerStatus.Killed;
            if (mDifficulty == difficulty) return !state.DifficultyLimited ? LayerStatus.Unlock : LayerStatus.LockTime;
            return LayerStatus.Lock;
        }
        private void ClearUnlockWait()
        { if (unlock != null) StopCoroutine(unlock); unlock = null; }
        private IEnumerator WaitUnlockTime(DateTime end)
        {
            yield return new WaitForSeconds(Math.Max(0f, (float)(end - DateTime.Now).TotalSeconds));
            unlock = null;
            state = bindings.Read(mActivityId, mDifficulty);
            RefreshContent();
        }
        private void ShowAward()
        {
            bindings.RecycleRewardIcons(this);
            bool dailyReceived = state.DailyAwardCount >= state.MaxDailyAwardCount;
            foreach (var reward in state.DailyAwards)
            {
                ItemIcon icon = bindings.GetRewardIcon(reward.Key, mChallengeAwardSlot, this);
                icon.SetCount(reward.Value); icon.SetClickPopupInfoDialog();
                icon.SetAttachment(ItemIconAttachmentType.GotTip, dailyReceived);
            }
            bool received = state.AwardedDifficulties.Contains(mDifficulty);
            bool claimable = state.Difficulty > mDifficulty && !received;
            foreach (var reward in state.KillAwards)
            {
                ItemIcon icon = bindings.GetRewardIcon(reward.Key, mKillAwardSlot, this);
                icon.SetCount(reward.Value); icon.SetClickPopupInfoDialog();
                icon.SetAttachment(ItemIconAttachmentType.GotTip, received);
                icon.SetAttachment(ItemIconAttachmentType.RedDotTip, claimable);
                if (claimable) icon.AddBgContent(mClaimableEffectGo);
            }
            mReceiveKillAwardBtn.gameObject.SetActive(claimable);
        }
        // Empty in the official application; retain that behavior.
        private void OnClickMonsterInfoBtn() { }
        private void OnClickReceiveKillAwardBtn() => bindings.ReceiveKilledAward(mActivityId, mDifficulty);
        private void OnChallengeBtn() => bindings.Challenge(mActivityId);
        private void OnGoToMapBtn() => bindings.Navigate(mGoToMapNavigateKey);
        private void OnDestroy() => bindings?.RecycleRewardIcons(this);
    }
}
