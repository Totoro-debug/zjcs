using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Operations consumed by the original generic team-confirm button. The
    // activity-specific ChallengeBtn remains a separate original controller.
    public interface IActivityTeamBattleStart
    {
        bool HasTeam { get; }
        bool HasStageHandler { get; }
        int MaximumPlayerCount { get; }
        int PlayerCount { get; }
        bool MeetsTargetPlayerCount { get; }
        bool IsAllowStartBattle { get; }
        bool AllAssist { get; }
        void ConfirmStart(Action<int, IReadOnlyList<long>> completed);
        void OpenConfirm(Action start);
        void StartStage();
        void CloseWindow();
        void ReportError(int error);
        void ReportLockedMembers(IReadOnlyList<long> players);
    }

    // Selected original SecretAreaBattleButtons. Transport/popup/stage calls
    // are adapted; original count checks and confirmation branches are kept.
    [Serializable]
    public sealed class SecretAreaBattleButtons : MonoBehaviour
    {
        [SerializeField] private Button mBattleBtn;
        private IActivityTeamBattleStart mHandler;
        public void Inject(IActivityTeamBattleStart handler) => mHandler = handler;
        public void SetHandler(IActivityTeamBattleStart handler) => mHandler = handler;
        public void Init() => mBattleBtn.onClick.AddListener(HandleBattleBtnClicked);
        private void HandleBattleBtnClicked()
        {
            if (mHandler.HasTeam && mHandler.MeetsTargetPlayerCount
                && mHandler.IsAllowStartBattle) StartBattle();
        }
        private void StartBattle()
        {
            if (!mHandler.HasStageHandler) return;
            if (mHandler.HasTeam && mHandler.MeetsTargetPlayerCount)
            {
                if (mHandler.PlayerCount > mHandler.MaximumPlayerCount)
                { mHandler.ReportError(6163); return; }
                if (!mHandler.AllAssist)
                {
                    mHandler.ConfirmStart((error, players) =>
                    {
                        if (error != 0) Debug.LogError("OperPlayerTeamSecretAreaConfirmStart Error:" + error, this);
                        if (players == null)
                        {
                            mHandler.OpenConfirm(mHandler.StartStage);
                            mHandler.CloseWindow();
                        }
                        else if (players.Count != 0) mHandler.ReportLockedMembers(players);
                    });
                    return;
                }
                mHandler.StartStage();
            }
            mHandler.CloseWindow();
        }
        private void OnDestroy() { if (mBattleBtn) mBattleBtn.onClick.RemoveListener(HandleBattleBtnClicked); }
    }
}
