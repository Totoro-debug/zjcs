using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Official.ItemDetailUI
{
    // ItemDetailDialog's original ordinary-item path. Usable/expiring inventory
    // objects require their original use/expiry adapters before entering here.
    public sealed class ItemDetailDialog : UIBase
    {
        private enum ShowItemCountType { ItemCountNormal, ItemCountLimit, ItemCountMoreThanLimit }
        public Image ItemImage;
        public WorldMultiLanguageText FakeItemIcon, NameText;
        public Image QualityBg;
        public WorldMultiLanguageText QualityText, TypeText;
        public GameObject DetailGo;
        public WorldMultiLanguageText FuncDescText, DescText;
        public GameObject ItemCountGo;
        public WorldMultiLanguageText ItemCountText;
        [SerializeField] private WorldMultiLanguageText mItemMaxCountText;
        public GameObject ItemSourceObj;
        public ItemSourceGo ItemSourceGo;
        [SerializeField] private Component mItemUseDetailDialog;
        [SerializeField] private ItemDetailDialogUseResult mItemDetailDialogUseResult;
        [SerializeField] private bool mIsSmall;
        [SerializeField] private ItemDetailDialogAddition mAdditionDetail;
        [SerializeField] private WorldStateSwitcher<bool> mRemainTimeStateSwitcher;
        [SerializeField] private Component mRemainTimeInfo;
        [SerializeField] private WorldStateSwitcher<ShowItemCountType> mShowCountStateSwitcher;
        private IItemDetailBindings bindings;
        private ItemDetailDefinition definition;
        public IProfessionItemInfo ItemInfo => definition?.Info;
        public long RequireCount { get; private set; }
        public void Bind(IItemDetailBindings value)
        {
            bindings = value ?? throw new ArgumentNullException(nameof(value));
            if (ItemSourceGo) ItemSourceGo.Bind(value);
        }
        public void Init(int itemId, long requireCount = 0)
        {
            if (bindings == null) throw new InvalidOperationException("Bind original item details before Init.");
            definition = bindings.Detail(itemId) ?? throw new InvalidOperationException("Original item definition is absent.");
            if (definition.Info == null) throw new InvalidOperationException("Original item info is absent.");
            if (mItemUseDetailDialog) throw new NotSupportedException("Bind the original item-use dialog before showing usable items.");
            if (mItemDetailDialogUseResult)
                mItemDetailDialogUseResult.gameObject.SetActive(mItemDetailDialogUseResult.TryInit(definition));
            ItemItem();
            if (requireCount > 10000 && definition.IsCookingMaterial
                && bindings.Items.Player.GetItemAmount(itemId) < requireCount)
                Debug.LogError("食材的需求量大于10000");
            RequireCount = requireCount;
            InitSourceData();
            if (mAdditionDetail) mAdditionDetail.Init(definition);
            mRemainTimeStateSwitcher.SetState(false);
        }
        protected override void OnEnable()
        {
            if (bindings == null) throw new InvalidOperationException("Bind original item details before activation.");
            bindings.Items.Player.OnItemCountUpdate += OnItemCountUpdate;
        }
        protected override void OnDisable()
        {
            if (bindings != null) bindings.Items.Player.OnItemCountUpdate -= OnItemCountUpdate;
        }
        private void OnItemCountUpdate(IProfessionItemData item, long previous, object source)
        {
            if (ItemInfo != null && item.Info.ClassId == ItemInfo.ClassId && ItemCountGo && ItemCountGo.activeSelf)
                RefreshCountContent(item.Count);
        }
        private void ItemItem()
        {
            if (ItemImage)
                ItemImage.sprite = string.IsNullOrEmpty(ItemInfo.Icon) ? null : bindings.LoadSprite(ItemInfo.Icon);
            if (FakeItemIcon)
            {
                bool fake = !ItemImage.sprite;
                FakeItemIcon.gameObject.SetActive(fake); ItemImage.gameObject.SetActive(!fake);
                if (fake) FakeItemIcon.SetKey("{0}", ItemInfo.GetFakeIcon());
            }
            if (NameText) NameText.SetKey(definition.NameKey);
            bool function = false, description = false;
            if (FuncDescText)
            {
                FuncDescText.SetKey(definition.FunctionDescriptionKey, definition.FunctionDescriptionArguments);
                function = bindings.ContainsText(definition.FunctionDescriptionKey);
                FuncDescText.gameObject.SetActive(function);
            }
            if (DescText)
            {
                DescText.SetKey(definition.DescriptionKey, definition.DescriptionArguments);
                description = bindings.ContainsText(definition.DescriptionKey);
                DescText.gameObject.SetActive(description);
            }
            if (DetailGo) DetailGo.SetActive(function | description);
            if (TypeText) TypeText.SetKey(definition.TypeKey);
            SetQuality(ItemInfo.Quality);
            if (ItemCountGo)
            {
                bool show = ItemInfo.CanOverlap && definition.ShowPlayerItemCount;
                ItemCountGo.SetActive(show);
                if (show) RefreshCountContent(bindings.Items.Player.GetItemAmount(ItemInfo.ClassId));
            }
        }
        private void RefreshCountContent(long count)
        {
            ItemCountText.Format(CultureNumberArg.Compact(count));
            if (definition.IsCountLimit) mItemMaxCountText.Format(CultureNumberArg.Compact(definition.MaxCount));
            mShowCountStateSwitcher.SetState(!definition.IsCountLimit ? ShowItemCountType.ItemCountNormal
                : count >= definition.MaxCount ? ShowItemCountType.ItemCountMoreThanLimit : ShowItemCountType.ItemCountLimit);
        }
        public void SetQuality(Quality quality)
        {
            if (QualityText)
            {
                QualityText.SetKey("Quality." + quality);
                QualityText.Color = bindings.QualityTextColor(quality);
            }
            if (QualityBg) QualityBg.sprite = bindings.QualityBackground(quality, mIsSmall);
        }
        private void InitSourceData()
        {
            if (!ItemSourceGo) return;
            if (GetComponent<WorldPopupWindow>()) ItemSourceGo.Init(ItemInfo.ClassId, gameObject, RequireCount);
            bool active = false;
            foreach (var source in bindings.Sources(ItemInfo.ClassId))
                if (!source.Hidden) { active = true; break; }
            ItemSourceObj.SetActive(active);
        }
        public void HideSource() { if (ItemSourceObj) ItemSourceObj.SetActive(false); }
        public RectTransform GetFirstMaterialGetButtonRect() => ItemSourceGo.MapResourceJumpButtonRect;
    }

}
