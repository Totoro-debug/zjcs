using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleResultUI
{
    public interface IArenaResultHost
    {
        PlayerInfoData Opponent { get; }
        ArenaFightResult Settlement { get; }
        void LeaveGame();
        void OpenOpponent();
    }

    // Selected original own-attacker/guard branch. A settled local snapshot
    // replaces ArenaDataManager's asynchronous guard and fight-record requests.
    public sealed class GameEndPanelArena : MonoBehaviour
    {
        [Serializable] public sealed class ArenaIconGroup
        {
            public PlayerIconMe PlayerIconMe;
            public ArenaOpponentIcon OpponentIcon;
            public void InitMe() => PlayerIconMe.Init();
            public void InitOpponent(PlayerInfoData opponent, Action clicked) => OpponentIcon.Init(opponent, clicked);
        }
        public GameEndPanelBase GameEndPanelBase;
        public GameObject WinEffect, LoseEffect;
        public WorldMultiLanguageText WinTitleText, LoseTitleText, LoseTipText;
        [SerializeField] private ArenaIconGroup mWinnerIcon, mLoserIcon;
        public Button LeaveBtn;
        [SerializeField] private ArenaShowFightResult mShowFightResult;
        private IArenaResultHost host;

        public void Bind(IArenaResultHost value) => host = value ?? throw new ArgumentNullException(nameof(value));
        private void OnEnable()
        {
            GameEndPanelBase.InitCallBack(ShowGameResult);
            LeaveBtn.onClick.AddListener(OnLeaveBtnClicked);
        }
        private void OnDisable() => LeaveBtn.onClick.RemoveListener(OnLeaveBtnClicked);
        private void OnLeaveBtnClicked() => host.LeaveGame();
        private void ShowGameResult(BattleResultData result)
        {
            if (result == null) return;
            if (host == null) throw new InvalidOperationException("Original arena result host is unbound.");
            mShowFightResult.gameObject.SetActive(true);
            mShowFightResult.Init(host.Settlement);
            WinEffect.SetActive(result.Win);
            LoseEffect.SetActive(!result.Win);
            if (result.Win)
            {
                WinTitleText.SetKey("ui_arena_win_text");
                mWinnerIcon.InitMe();
                mLoserIcon.InitOpponent(host.Opponent, host.OpenOpponent);
            }
            else
            {
                LoseTitleText.SetKey("ui_arena_lose_text");
                LoseTipText.Format(host.Opponent.Name);
            }
        }
    }
}
