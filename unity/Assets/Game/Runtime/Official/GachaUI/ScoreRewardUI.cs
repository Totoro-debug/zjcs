using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.GachaUI
{
    // Original ScoreRewardUI execution order; global services and RPCs are local ports.
    public class ScoreRewardUI : UIBase
    {
        private Func<bool> mAdditionInfoTipShowFunction;
        public WorldMultiLanguageText ScoreText;
        public Button GetScoreRewardBtn, ScoreRewardInfoBtn;
        public Transform RewardSlot;
        public Image ProgressImage;
        public WorldStateSwitcher<bool> StateSwitcher;
        [SerializeField] private Color mFullScoreTextColor;
        [SerializeField] private AdditionInfoTip mAdditionInfoTip;
        private GachaTextKeyArgWrap mAdditionInfoTipTextWrap, mScoreRuleTextWrap;
        private ScoreRewardKey mAchievementDataKey;
        private ScoreRewardAchievementInfo mAchievementInfo = new ScoreRewardAchievementInfo { IsInvalid = true };
        private readonly List<ItemIcon> mCacheIcons = new List<ItemIcon>();
        private string mDetailPrefabPath, mFullScoreTextColorHtml;
        private bool mRewardCompleted;
        public ScoreRewardAchievementInfo AchievementInfo => mAchievementInfo;
        public IScoreRewardServices Services { private get; set; }

        private void Awake()
        {
            mFullScoreTextColorHtml = ColorUtility.ToHtmlStringRGB(mFullScoreTextColor);
            GetScoreRewardBtn.onClick.AddListener(OnClickGetScoreRewardBtn);
            ScoreRewardInfoBtn.onClick.AddListener(OnClickScoreRewardInfoBtn);
        }
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            Services.OnRecordAlreadyGotAchievementReward += MainPlayer_OnRecordAlreadyGotAchievementReward;
            Services.OnUpdateAchievementData += MainPlayer_OnUpdateAchievementData;
            Services.OnOpenChest += MainPlayer_OnOpenChest;
            Services.OnUpdateBlackboardData += MainPlayer_OnUpdateBlackboardData;
            RefreshInfo();
        }
        protected override void OnDisableAndInited()
        {
            if (Services.HasPlayer)
            {
                Services.OnRecordAlreadyGotAchievementReward -= MainPlayer_OnRecordAlreadyGotAchievementReward;
                Services.OnUpdateAchievementData -= MainPlayer_OnUpdateAchievementData;
                Services.OnOpenChest -= MainPlayer_OnOpenChest;
                Services.OnUpdateBlackboardData -= MainPlayer_OnUpdateBlackboardData;
            }
            RecycleItemIcons();
            base.OnDisableAndInited();
        }
        private void MainPlayer_OnUpdateBlackboardData(ScoreBlackboardKey dataKey)
        {
            if (mAchievementInfo.BlackboardKey is ScoreBlackboardKey key
                && key.BlackboardDataType == dataKey.BlackboardDataType && !(key.ParamsKey != dataKey.ParamsKey)) RefreshInfo();
        }
        private void MainPlayer_OnUpdateAchievementData(ScoreRewardKey key)
        { if (key.AchievementType == mAchievementDataKey.AchievementType) RefreshInfo(); }
        private void MainPlayer_OnRecordAlreadyGotAchievementReward(int value) => RefreshInfo();
        private void MainPlayer_OnOpenChest(int chestId, long openCount) => RefreshReward();
        public void Init(ScoreRewardKey key, GachaTextKeyArgWrap rule) => Init(key, rule, string.Empty);
        public void Init(ScoreRewardKey key, GachaTextKeyArgWrap rule, string path)
        { InitWrapper(() => { mAchievementDataKey = key; mScoreRuleTextWrap = rule; mDetailPrefabPath = path; }); }
        public void SetAdditionInfoData(Func<bool> predicate, GachaTextKeyArgWrap text)
        { mAdditionInfoTipShowFunction = predicate; mAdditionInfoTipTextWrap = text; RefreshInfo(); }
        public void ClearAdditionInfoData()
        {
            mAdditionInfoTipShowFunction = null;
            mAdditionInfoTipTextWrap = GachaTextKeyArgWrap.sEmpty;
            mAdditionInfoTip.HideInfo();
        }
        public void RefreshInfo()
        {
            var infos = Services.GetAchievementInfoListByType(mAchievementDataKey);
            if (infos == null || infos.Count == 0) return;
            if (Services.TryGetGotRewardMaxIndex(mAchievementDataKey, out int index))
            { int next = index + 1; mAchievementInfo = infos[next < infos.Count ? next : infos.Count - 1]; }
            else mAchievementInfo = infos[0];
            RefreshReward();
            CheckShowAdditionInfoTip();
        }
        private void CheckShowAdditionInfoTip()
        {
            if (mAdditionInfoTipShowFunction == null || !mAdditionInfoTipTextWrap.IsValid) mAdditionInfoTip.HideInfo();
            else if (mAdditionInfoTipShowFunction()) mAdditionInfoTip.ShowInfo(mAdditionInfoTipTextWrap);
            else mAdditionInfoTip.HideInfo();
        }
        private void RefreshReward()
        {
            if (mAchievementInfo.IsInvalid) return;
            RecycleItemIcons();
            foreach (var reward in mAchievementInfo.Reward)
                mCacheIcons.Add(Services.GetIcon(reward.Key, RewardSlot).SetClickPopupInfoDialog().SetCount(reward.Value));
            if (!mAchievementInfo.HasProgressTask) return;
            var progress = Services.GetProgress(mAchievementInfo);
            mRewardCompleted = progress.currentValue >= progress.maxValue && Services.IsAlreadyGotAchievementReward(mAchievementInfo);
            bool canGet = Services.IsCanGetAchievementReward(mAchievementInfo) && !Services.IsAlreadyGotAchievementReward(mAchievementInfo);
            StateSwitcher.SetState(canGet);
            SetProgress(progress.maxValue, progress.currentValue);
            bool got = !canGet && progress.maxValue <= progress.currentValue;
            if (!got) return;
            foreach (var icon in mCacheIcons) icon.SetAttachment(ItemIconAttachmentType.GotTip, got);
            ProgressImage.fillAmount = 1f;
        }
        private void RecycleItemIcons()
        {
            if (Services == null) return;
            foreach (var icon in mCacheIcons) Services.RecycleIcon(icon);
            mCacheIcons.Clear();
        }
        private void SetProgress(long maxCount, long currentCount)
        {
            long previous = 0;
            int index = mAchievementInfo.Index - 1;
            if (index >= 0 && Services.GetAchievementInfoByGroupAndIndex(mAchievementInfo.Group, index).ScoreRewardCount is long count) previous = count;
            if (mRewardCompleted) { ScoreText.SetKey("{0}", currentCount); ProgressImage.fillAmount = 0f; return; }
            ProgressImage.fillAmount = (float)(currentCount - previous) / (float)(maxCount - previous);
            object max = Services.Compact(maxCount - previous), current = Services.Compact(currentCount - previous);
            if (ProgressImage.fillAmount >= 1f) ScoreText.SetKey("score_reward_ui_full_text", mFullScoreTextColorHtml, current, max);
            else ScoreText.SetKey("score_reward_ui_text", current, max);
        }
        private void OnClickGetScoreRewardBtn()
        {
            if (mAchievementInfo.IsInvalid) return;
            if (!Services.IsCanGetAchievementReward(mAchievementInfo)) { Services.ShowSystemMessage("AchievementPanel.CanNotGetReward"); return; }
            if (Services.IsAlreadyGotAchievementReward(mAchievementInfo)) { Services.ShowSystemMessage("AchievementPanel.AlreadyGotReward"); return; }
            if (Services.CanBatchGetScoreReward(mAchievementDataKey) && !Services.IsScoreRewardBatchGetLimited)
            { Services.ClaimAll(new List<int> { mAchievementInfo.Group }, RefreshInfo); return; }
            Services.Claim(mAchievementInfo.Group, 1, false, RefreshInfo);
        }
        private void OnClickScoreRewardInfoBtn()
            => Services.PopupScoreRewardDialog(mDetailPrefabPath, mRewardCompleted, mAchievementDataKey, mScoreRuleTextWrap);
    }
}
