using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BattleResultUI
{
    // Selected FightMainPanelLeaveBtnUI. The stage host supplies its leave handler.
    public sealed class FightMainPanelLeaveBtnUI : UIBase
    {
        [SerializeField] private Button mBtn;
        private Action leave;
        public void Init(Action handler) => InitWrapper(() => leave = handler ?? throw new ArgumentNullException(nameof(handler)));
        protected override void OnEnableAndInited() { base.OnEnableAndInited(); mBtn.onClick.AddListener(OnBtnClick); }
        protected override void OnDisableAndInited() { mBtn.onClick.RemoveListener(OnBtnClick); base.OnDisableAndInited(); }
        private void OnBtnClick() => leave();
    }
}
