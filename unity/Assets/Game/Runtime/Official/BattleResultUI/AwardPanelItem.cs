using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.BattleResultUI
{
    public interface IBattleAwardIconHost
    {
        ItemAwardIcon InstantiateAward(Transform parent, OfficialBattleAwardEntry data);
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
    }

    // Selected AwardPanelItem, including pooled visibility and original show animation.
    public sealed class AwardPanelItem : MonoBehaviour, IOfficialBattleAwardListItem, IGameObjectPoolItem
    {
        [SerializeField] private Transform mAnchor;
        [SerializeField] private Animation mShowAnimation;
        private ItemAwardIcon mAwardIcon;
        private bool mIsActive;
        public Component Component => this;
        public void Init(IBattleAwardIconHost host, OfficialBattleAwardEntry data)
        {
            if (mAwardIcon == null) mAwardIcon = host.InstantiateAward(mAnchor, data);
            else mAwardIcon.Init(host, data);
        }
        private void OnDisable() { if (mAwardIcon != null) mAwardIcon.Clear(); }
        public void Show()
        {
            if (mIsActive) return;
            mIsActive = true;
            mAnchor.gameObject.SetActive(true);
            mShowAnimation.Play();
        }
        public void Hide() { mIsActive = false; mAnchor.gameObject.SetActive(false); }
        public void OnGameObjectPoolReset() => mIsActive = false;
        public void OnGameObjectPoolClear() { }
    }
}
