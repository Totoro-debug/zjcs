using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.BattleResultUI
{
    // Selected GameEndPanelBase. UnityEvent subscriptions replace R3 disposal;
    // original settings, result data and window providers cross explicit ports.
    public class GameEndPanelBase : MonoBehaviour
    {
        [SerializeField] private Button mSkillItemReportBtn;
        [SerializeField] protected Button mGamePlaybackShareBtn;
        [SerializeField] protected Button mGameSaveRecordBtn;
        [SerializeField] protected Button mThumbUpReportSwitchBtn;
        [SerializeField] protected Image mThumbUpImg;
        [SerializeField] protected Image mReportImg;
        private bool mAnimFinished;
        protected BattleResultData mGameResultMsg;
        private Action<BattleResultData> mCallBack;
        private Action mAnimAction;
        private Action<bool> mOnThumbUpReportSwitchBtnClickedAction;
        private bool mIsFastVictory;
        private bool mIsShowingThumbUp = true;
        protected CancellationTokenSource mCancellationTokenSource;
        private IBattleResultBaseHost host;

        public BattleResultData ResultMsg => mGameResultMsg;
        public Button ReportButton => mSkillItemReportBtn;
        public void Bind(IBattleResultBaseHost value) => host = value ?? throw new ArgumentNullException(nameof(value));
        private IBattleResultBaseHost Host => host ?? throw new InvalidOperationException("Original result host is unbound.");

        public void InitMsg(BattleResultData gameResultMsg)
        {
            mGameResultMsg = gameResultMsg;
            mIsFastVictory = mGameResultMsg != null && mGameResultMsg.FastVictory;
            RefreshSkillItemReportBtn();
            RefreshShareBtn();
            RefreshSaveRecordBtn();
            RefreshThumbUpReportSwitchBtn();
            Host.SetThumbsUpGameId(mGameResultMsg != null ? mGameResultMsg.GameId : 0);
            mCallBack?.Invoke(gameResultMsg);
        }

        public void InitCallBack(Action<BattleResultData> callBack)
        {
            mCallBack = callBack;
            if (mGameResultMsg != null) callBack?.Invoke(mGameResultMsg);
        }

        public void InitThumbUpReportSwitchCallback(Action<bool> callback)
        {
            mOnThumbUpReportSwitchBtnClickedAction = callback;
            mOnThumbUpReportSwitchBtnClickedAction?.Invoke(mIsShowingThumbUp);
        }

        protected virtual void OnEnable()
        {
            mCancellationTokenSource = new CancellationTokenSource();
            if (mSkillItemReportBtn != null) mSkillItemReportBtn.onClick.AddListener(OnSkillItemReportBtnClicked);
            if (mGamePlaybackShareBtn != null) mGamePlaybackShareBtn.onClick.AddListener(OnGamePlaybackShareBtnClicked);
            if (mGameSaveRecordBtn != null) mGameSaveRecordBtn.onClick.AddListener(OnGameRecordSaveBtnClicked);
            if (mThumbUpReportSwitchBtn != null) mThumbUpReportSwitchBtn.onClick.AddListener(OnThumbUpReportSwitchBtnClicked);
        }

        private void OnDisable()
        {
            if (mSkillItemReportBtn != null) mSkillItemReportBtn.onClick.RemoveListener(OnSkillItemReportBtnClicked);
            if (mGamePlaybackShareBtn != null) mGamePlaybackShareBtn.onClick.RemoveListener(OnGamePlaybackShareBtnClicked);
            if (mGameSaveRecordBtn != null) mGameSaveRecordBtn.onClick.RemoveListener(OnGameRecordSaveBtnClicked);
            if (mThumbUpReportSwitchBtn != null) mThumbUpReportSwitchBtn.onClick.RemoveListener(OnThumbUpReportSwitchBtnClicked);
            if (mCancellationTokenSource != null)
            {
                mCancellationTokenSource.Cancel();
                mCancellationTokenSource.Dispose();
                mCancellationTokenSource = null;
            }
        }

        private void RefreshSkillItemReportBtn()
        {
            if (mSkillItemReportBtn != null && mIsFastVictory) mSkillItemReportBtn.gameObject.SetActive(false);
        }

        protected virtual void RefreshShareBtn()
        {
            if (mGamePlaybackShareBtn == null) return;
            if (mGameResultMsg == null || mIsFastVictory)
            {
                mGamePlaybackShareBtn.gameObject.SetActive(false);
                return;
            }
            BattleResultStage stageInfo = Host.GetStage(mGameResultMsg.StageId);
            if (stageInfo == null)
            {
                mGamePlaybackShareBtn.gameObject.SetActive(false);
                return;
            }
            mGamePlaybackShareBtn.gameObject.SetActive(Host.ShareStageTypes.Contains(stageInfo.Type));
        }

        protected virtual void RefreshSaveRecordBtn()
        {
            if (mGameSaveRecordBtn == null) return;
            if (mGameResultMsg == null)
            {
                mGameSaveRecordBtn.gameObject.SetActive(false);
                return;
            }
            BattleResultStage stageInfo = Host.GetStage(mGameResultMsg.StageId);
            if (stageInfo == null || Host.IsPlayback)
            {
                mGameSaveRecordBtn.gameObject.SetActive(false);
                return;
            }
            bool active = Host.SaveRecordStageTypes.Contains(stageInfo.Type)
                && !Host.IsAutoSaveRecordStage(stageInfo.Id) && Host.HasLocalCapturedRecord(mGameResultMsg.GameId);
            mGameSaveRecordBtn.gameObject.SetActive(active);
        }

        protected virtual void RefreshThumbUpReportSwitchBtn()
        {
            if (mThumbUpReportSwitchBtn == null) return;
            if (mGameResultMsg == null || mIsFastVictory)
            {
                mThumbUpReportSwitchBtn.gameObject.SetActive(false);
                return;
            }
            BattleResultStage stageInfo = Host.GetStage(mGameResultMsg.StageId);
            if (stageInfo == null || !Host.ThumbsUpStageTypes.Contains(stageInfo.Type))
            {
                mThumbUpReportSwitchBtn.gameObject.SetActive(false);
                return;
            }
            mThumbUpReportSwitchBtn.gameObject.SetActive(HasNonTeamMatchedReportTarget());
        }

        private bool HasNonTeamMatchedReportTarget()
        {
            var players = mGameResultMsg.Players;
            if (players == null || players.Count == 0) return false;
            var team = new HashSet<long>();
            foreach (long target in Host.TeamMemberTargetIds) team.Add(target);
            foreach (var player in players)
            {
                if (player?.TargetData != null && !player.TargetData.IsNpc
                    && !team.Contains(player.TargetData.TargetId)) return true;
            }
            return false;
        }

        private void OnSkillItemReportBtnClicked() => Host.OpenReport(mGameResultMsg);
        protected virtual void OnGamePlaybackShareBtnClicked() => Host.OpenShare(mGameResultMsg.GameId);

        protected virtual void OnGameRecordSaveBtnClicked()
        {
            BattleResultStage stageInfo = Host.GetStage(mGameResultMsg.StageId);
            if (stageInfo != null && Host.ActiveSaveRecord(mGameResultMsg.GameId, stageInfo.Type))
                Host.ShowSaveRecordSuccess();
        }

        protected virtual void OnThumbUpReportSwitchBtnClicked()
        {
            mIsShowingThumbUp = !mIsShowingThumbUp;
            mThumbUpImg.gameObject.SetActive(!mIsShowingThumbUp);
            mReportImg.gameObject.SetActive(mIsShowingThumbUp);
            mOnThumbUpReportSwitchBtnClickedAction?.Invoke(mIsShowingThumbUp);
        }

        public virtual void SetSkillItemReportBtn(bool active)
        {
            if (ResultMsg == null || mIsFastVictory) mSkillItemReportBtn.gameObject.SetActive(false);
            else if (mSkillItemReportBtn != null) mSkillItemReportBtn.gameObject.SetActive(active);
        }

        public void InitAnimAction(Action animAction)
        {
            mAnimAction = animAction;
            if (mAnimFinished) mAnimAction?.Invoke();
        }

        public void ResetData()
        {
            mAnimFinished = false;
            mGameResultMsg = null;
            mCallBack = null;
            mAnimAction = null;
            mOnThumbUpReportSwitchBtnClickedAction = null;
            mIsShowingThumbUp = true;
        }

        public void OnAnimFinished()
        {
            mAnimFinished = true;
            mAnimAction?.Invoke();
        }
    }
}
