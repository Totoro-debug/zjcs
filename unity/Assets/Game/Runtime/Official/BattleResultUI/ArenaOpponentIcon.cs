using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BattleResultUI
{
    public sealed class ArenaOpponentIcon : UIBase
    {
        [SerializeField] private PlayerInfoUI mInfoUI;
        // The selected result prefab has no combat-rating subcontroller.
        [SerializeField] private Component mCombatRatingInfo;
        private PlayerInfoData? info;
        private Action clicked;
        private Action<PlayerInfoData> updated;
        public void Init(PlayerInfoData value, Action onClicked) => InitWrapper(() =>
        { info = value; clicked = onClicked; });
        public void SetInfoUpdateCallback(Action<PlayerInfoData> callback) => updated = callback;
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            var value = info.Value;
            mInfoUI.Init(value, () => clicked?.Invoke());
            mInfoUI.Refresh();
            if (mCombatRatingInfo) throw new NotSupportedException("Inspect the original arena combat-rating controller.");
            updated?.Invoke(value);
        }
        public bool TryGetOpponentSubRank(out int subRank)
        { subRank = info?.SubRank ?? 0; return info.HasValue; }
    }
}
