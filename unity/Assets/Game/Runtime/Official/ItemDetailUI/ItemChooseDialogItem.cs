using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemChooseDialogItem : MonoBehaviour
    {
        public enum SelectState { Off, On, Hide }
        [SerializeField] private ItemIcon mItemIcon;
        [SerializeField] private WorldStateSwitcher<SelectState> mSelectionSwitcher;
        [SerializeField] private Button mToggleBtn;
        private Action<ItemChooseDialogItem> mOnToggleBtnClickEvent;
        private IItemChooseDialogBindings bindings;
        public int ClassId { get; private set; }
        public SelectState ToggleState { get => mSelectionSwitcher.State; set => mSelectionSwitcher.SetState(value); }
        public void Bind(IItemChooseDialogBindings value) => bindings = value ?? throw new ArgumentNullException(nameof(value));
        private void Awake() => mToggleBtn.onClick.AddListener(OnToggleBtnClick);
        private void OnDestroy() => mToggleBtn.onClick.RemoveListener(OnToggleBtnClick);
        public void SetToggleVisiable(bool show) => mToggleBtn.gameObject.SetActive(show);
        private void OnToggleBtnClick() => mOnToggleBtnClickEvent?.Invoke(this);
        public void Init(int classId, long count, Action<ItemChooseDialogItem> onToggleClickCallback)
        {
            if (bindings.Items.GetItemInfo(classId) == null) return;
            ClassId = classId; mOnToggleBtnClickEvent = onToggleClickCallback;
            mItemIcon.Init(classId).SetCount(count).SetClickPopupInfoDialog().SetBottomName();
            var flags = bindings.ChoiceFlags(classId);
            if (flags.IsTreasure) bindings.ApplyTreasureOwnership(mItemIcon, ClassId, true);
            mItemIcon.SetAttachment(ItemIconAttachmentType.ProfessionMismatchTip, flags.ProfessionMismatch);
            mItemIcon.SetAttachment(ItemIconAttachmentType.GMDisabled, flags.FunctionDisabled);
            mItemIcon.SetAttachment(ItemIconAttachmentType.GotCenterTip, flags.Got);
            ToggleState = SelectState.Off;
        }
        public void RefreshCount(long count) => mItemIcon.SetCount(count);
    }
}
