using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BattleResultUI
{
    // Selected GameEndPanelDemonKing. The host replaces AwardManager and LeaveGame;
    // original fields, callbacks and reward-dependent transforms remain on the prefab.
    public sealed class GameEndPanelDemonKing : UIBase
    {
        [Serializable]
        public sealed class DemonKingBossRectTransformInfo
        {
            public RectTransform RectTransform;
            public Vector2 Position;
            public float Scale;
        }
        public GameEndPanelBase GameEndPanelBase;
        public Button LeaveGameBtn;
        public GameEndPanelBoss GameEndPanelBoss;
        [SerializeField] private List<DemonKingBossRectTransformInfo> mBossTransform;
        private IBattleBossResultHost host;

        public void Init(IBattleBossResultHost value) => InitWrapper(() =>
        {
            host = value ?? throw new ArgumentNullException(nameof(value));
            GameEndPanelBoss.Bind(host);
        });

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            LeaveGameBtn.onClick.AddListener(OnLeaveGameBtnClicked);
            GameEndPanelBase.InitCallBack(RefreshUI);
            GameEndPanelBase.InitThumbUpReportSwitchCallback(OnThumbUpReportSwitch);
        }
        protected override void OnDisableAndInited()
        {
            LeaveGameBtn.onClick.RemoveListener(OnLeaveGameBtnClicked);
            base.OnDisableAndInited();
        }
        private void OnLeaveGameBtnClicked() => host.LeaveGame();
        private void RefreshUI(BattleResultData result)
        {
            if (result == null) return;
            GameEndPanelBoss.Init(result);
            GameEndPanelBoss.RefreshDamage(result.DamageValue);
        }
        private void OnThumbUpReportSwitch(bool showing) => GameEndPanelBoss?.OnThumbUpReportSwitch(showing);
        public void RefreshAward(IReadOnlyList<OfficialBattleAwardEntry> awards)
        {
            GameEndPanelBoss.RefreshAward(awards);
            if (awards == null || awards.Count == 0 || mBossTransform == null) return;
            foreach (DemonKingBossRectTransformInfo item in mBossTransform)
                if (item.RectTransform != null)
                {
                    item.RectTransform.anchoredPosition = item.Position;
                    item.RectTransform.localScale = Vector3.one * item.Scale;
                }
        }
    }
}
