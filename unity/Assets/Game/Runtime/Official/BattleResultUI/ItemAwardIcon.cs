using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.BattleResultUI
{
    // Selected ItemAwardIcon's ordinary, integral, class-ID award branch.
    // Rule 43000 has no indexed item, conversion, overflow or output-addition data.
    public sealed class ItemAwardIcon : UIBase, IGameObjectPoolItem
    {
        private enum IconState { None, Pieces, Token, PiecesAndToken }
        public Transform Anchor;
        [SerializeField] private WorldStateSwitcher<IconState> mIconStateSwitcher;
        public ImageProxy AgioIcon;
        public WorldMultiLanguageText AgioCountText;
        public ImageProxy RecycleItemIcon;
        public WorldMultiLanguageText RecycleText;
        private IBattleAwardIconHost host;
        private OfficialBattleAwardEntry award;
        public ItemIcon ItemIcon { get; private set; }
        public event Action OnInitCompleted;

        public void Init(IBattleAwardIconHost value, OfficialBattleAwardEntry data) => InitWrapper(() =>
        {
            host = value ?? throw new ArgumentNullException(nameof(value));
            award = data ?? throw new ArgumentNullException(nameof(data));
            if (data.ItemId != 41021 || data.Count != 300 || data.OriginalData != null)
                throw new InvalidOperationException("Only the verified fixed demon challenge award is bound here.");
        });
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            RecycleIcon();
            ItemIcon = host.GetIcon(award.ItemId, Anchor);
            SetItemIconStyle();
            if (!ItemIcon.myInfo.CanOverlap) ItemIcon.HideText();
            else ItemIcon.SetCount(award.Count, "ui_ItemIcon_count_key");
            ItemIcon.SetClickPopupInfoDialog();
            mIconStateSwitcher.SetState(IconState.None);
            ItemIcon.SetAttachment(ItemIconAttachmentType.ExtraGetTip, false);
            OnInitCompleted?.Invoke();
        }
        private void SetItemIconStyle() => ItemIcon.SetBottomName().SetBottomTextColor(ThemeColorType.Dark).SetBottomTextEffect(true);
        protected override void OnDisableAndInited()
        {
            RecycleIcon();
            mIconStateSwitcher.Clear();
            AgioIcon.Clear();
            RecycleItemIcon.Clear();
            base.OnDisableAndInited();
        }
        private void RecycleIcon()
        {
            if (ItemIcon == null) return;
            ItemIcon.ResetCountTextColor();
            host.RecycleIcon(ItemIcon);
            ItemIcon = null;
        }
        public void Clear() { RecycleIcon(); ResetInitState(); }
        public void OnGameObjectPoolReset() { }
        public void OnGameObjectPoolClear() { }
    }
}
