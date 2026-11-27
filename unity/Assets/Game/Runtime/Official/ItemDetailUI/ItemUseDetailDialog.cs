using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemUseDetailDialog : UIBase, IItemDetailDialog
    {
        public ItemDetailDialog ItemDetailDialog;
        public ItemDetailDialogUseContent ItemUseContent;
        public WorldMultiLanguageText ItemUseCountText;
        public event Action<long> OnUseCountChangedEvent;
        public void Init(int itemId, long requireCount = 0)
        { ItemDetailDialog.Init(itemId, requireCount); InitContent(); }
        public void Init(IProfessionItemData data, long requireCount = 0)
        { ItemDetailDialog.Init(data, requireCount); InitContent(); }
        private void InitContent()
        {
            ItemUseContent.SetValueChangedCallBack(OnValueChanged);
            ItemUseContent.SetRefreshSliderCallBack(RefreshSlider);
            ItemUseContent.Init(ItemDetailDialog.ItemInfo.ClassId);
        }
        private void RefreshSlider(long current, long maximum)
        { ItemUseCountText.Format(current, maximum); OnUseCountChangedEvent?.Invoke(current); }
        private void OnValueChanged(float value)
        {
            ItemUseCountText.Format(ItemUseContent.CurrentUseCount, ItemUseContent.CanUseItemCount);
            OnUseCountChangedEvent?.Invoke(ItemUseContent.CurrentUseCount);
        }
    }
}
