using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemChooseDialog : UIBase
    {
        [SerializeField] private Button mCancelBtn, mConfirmBtn;
        [SerializeField] private WorldMultiLanguageText mTitleTxt, mTip;
        [SerializeField] private Transform mContent;
        [SerializeField] private AwardPanelLayoutElement mAwardPanelLayoutElement;
        [SerializeField] private GameObject mOptionPrefab;
        [SerializeField] private WorldStateSwitcher<bool> mIsRandomStyleStateSwitcher;
        private Action<List<int>> mOnConfirmedCallback;
        private Action<ItemChooseDialogItem> mOnItemChooseDialogItemClickCallback;
        private Action mCloseAction;
        private Dictionary<int, long> mOptions;
        private int mConfirmCount, mUseCount;
        private bool mIsEqualProbability;
        private Dictionary<int, ItemChooseDialogItem> mItems;
        private HashSet<int> mSelectedOptions;
        private IItemChooseDialogBindings bindings;
        private bool mMultipleChoice => mConfirmCount > 1;
        public void Bind(IItemChooseDialogBindings value) => bindings = value ?? throw new ArgumentNullException(nameof(value));
        private void Awake() => mOnItemChooseDialogItemClickCallback = OnItemChooseDialogItemClick;
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            mCancelBtn.onClick.AddListener(OnCancelBtnClick);
            mConfirmBtn.onClick.AddListener(OnConfirmBtnClick);
            Refresh();
        }
        protected override void OnDisableAndInited()
        {
            mCancelBtn.onClick.RemoveListener(OnCancelBtnClick);
            mConfirmBtn.onClick.RemoveListener(OnConfirmBtnClick);
            mCloseAction = null;
            base.OnDisableAndInited();
        }
        public void Init(Dictionary<int, long> options, int confirmCount, int useCount,
            Action<List<int>> onConfirmedCallback, bool isEqualProbability = false)
        {
            mOptions = options; mConfirmCount = confirmCount; mUseCount = useCount;
            mIsEqualProbability = isEqualProbability;
            mSelectedOptions = new HashSet<int>(); mItems = new Dictionary<int, ItemChooseDialogItem>();
            mOnConfirmedCallback = onConfirmedCallback;
            InitWrapper(null);
        }
        private void SetRandomChooseStyle()
        {
            foreach (var item in mItems.Values) item.SetToggleVisiable(false);
            mTitleTxt.SetKey("ui_itemChoose_randomTitle"); mTip.SetKey("ui_itemChoose_randomTip");
        }
        private void SetSelfChooseStyle()
        {
            foreach (var item in mItems.Values) item.SetToggleVisiable(true);
            mTitleTxt.SetKey("ui_choos_dialog_title"); mTip.SetKey("ui_choos_dialog_tip", mConfirmCount);
        }
        public void SetTitleTextKey(string key) => mTitleTxt.SetKey(key);
        private void Refresh()
        {
            mContent.DestroyChildren();
            var ids = new List<int>(mOptions.Keys);
            bindings.SortOptions(ids);
            foreach (int id in ids)
            {
                var item = bindings.InstantiateUi(mOptionPrefab, mContent).GetComponent<ItemChooseDialogItem>();
                item.Init(id, unchecked(mOptions[id] * mUseCount), mOnItemChooseDialogItemClickCallback);
                mItems.Add(id, item);
            }
            mAwardPanelLayoutElement.SetCurrentRowNumByCellNum(mItems.Count);
            RefreshConfirmBtn();
            bool randomStyle = mConfirmCount == 0;
            mIsRandomStyleStateSwitcher.SetState(randomStyle && mIsEqualProbability);
            if (randomStyle) SetRandomChooseStyle(); else SetSelfChooseStyle();
        }
        private void OnItemChooseDialogItemClick(ItemChooseDialogItem item)
        {
            if (mMultipleChoice) HandleMultipleChoice(item); else HandleSingleChoice(item);
            RefreshConfirmBtn();
        }
        private void HandleMultipleChoice(ItemChooseDialogItem item)
        {
            int id = item.ClassId;
            if (mSelectedOptions.Remove(id))
            {
                item.ToggleState = ItemChooseDialogItem.SelectState.Off;
                foreach (var value in mItems.Values)
                    if (value.ToggleState == ItemChooseDialogItem.SelectState.Hide)
                        value.ToggleState = ItemChooseDialogItem.SelectState.Off;
                return;
            }
            if (mSelectedOptions.Count >= mConfirmCount) return;
            item.ToggleState = ItemChooseDialogItem.SelectState.On;
            mSelectedOptions.Add(id);
            if (mSelectedOptions.Count < mConfirmCount) return;
            foreach (var value in mItems.Values)
                if (value.ToggleState != ItemChooseDialogItem.SelectState.On)
                    value.ToggleState = ItemChooseDialogItem.SelectState.Hide;
        }
        private void HandleSingleChoice(ItemChooseDialogItem item)
        {
            int id = item.ClassId;
            if (mSelectedOptions.Contains(id)) return;
            foreach (int selected in mSelectedOptions)
                if (mItems.TryGetValue(selected, out var previous)) previous.ToggleState = ItemChooseDialogItem.SelectState.Off;
            mSelectedOptions.Clear(); item.ToggleState = ItemChooseDialogItem.SelectState.On;
            mSelectedOptions.Add(id);
        }
        private void RefreshConfirmBtn()
        {
            bool can = mSelectedOptions.Count == mConfirmCount;
            mConfirmBtn.interactable = can;
            bindings.SetConfirmGray(mConfirmBtn.gameObject, !can);
        }
        public void SetCloseAction(Action value) => mCloseAction = value;
        private void CloseDialog() { if (mCloseAction != null) mCloseAction(); else bindings.Close(gameObject); }
        private void OnConfirmBtnClick() { mOnConfirmedCallback?.Invoke(mSelectedOptions.ToList()); CloseDialog(); }
        private void OnCancelBtnClick() => CloseDialog();
        public void RefreshItemCounts(long multiplier)
        { foreach (var item in mItems) item.Value.RefreshCount(unchecked(mOptions[item.Key] * multiplier)); }
    }
}
