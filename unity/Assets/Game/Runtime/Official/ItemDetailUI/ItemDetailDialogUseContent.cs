using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeCountControlsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemDetailDialogUseContent : MonoBehaviour, IWindowOperator
    {
        public SetCountSlider SetCountSlider;
        public GameObject SliderObj;
        public Button UseItemBtn, CancelItemBtn;
        public WorldStateSwitcher<bool> CanUseStateSwitcher;
        [SerializeField] private WorldMultiLanguageText mUseBtnText;
        private bool mIsShowUseBtn;
        private long mSingleMaxUseCount = -1, mSingleMinUseCount = -1;
        private int itemId;
        private Action<long> mUseCallBack;
        private Action<long, long> mRefreshSliderCallBack;
        private Action<float> mValueChangedCallBack;
        private Action mRefreshViewCallBack;
        private Func<bool> mUseItemConditionCallBack;
        private Func<long> mGetCurrentUseCountCallBack;
        private IItemUseContentServices services;
        private IDisposable awardHandler;
        public long CanUseItemCount { get; private set; }
        public long CurrentUseCount { get; private set; }
        public GameObject Operator => gameObject;
        public void Bind(IItemUseContentServices value) => services = value ?? throw new ArgumentNullException(nameof(value));
        private void Awake()
        {
            UseItemBtn.onClick.AddListener(OnUseItemBtnClick);
            CancelItemBtn.onClick.AddListener(OnCancelItemBtnClick);
            SetCountSlider.OnCurrentCountUpdate += SetCountSlider_OnCurrentCountUpdate;
        }
        private void OnEnable()
        {
            if (services == null) throw new InvalidOperationException("Bind original item-use services before activation.");
            services.Player.OnItemCountUpdate += MainPlayer_OnItemCountUpdate;
            awardHandler = services.RegisterAwardHandler(HandleAward);
        }
        private void OnDisable()
        {
            if (services != null) services.Player.OnItemCountUpdate -= MainPlayer_OnItemCountUpdate;
            awardHandler?.Dispose(); awardHandler = null;
        }
        private void OnDestroy() => SetCountSlider.OnCurrentCountUpdate -= SetCountSlider_OnCurrentCountUpdate;
        public void Init(int id)
        {
            itemId = id;
            if (!services.IsCanUse(id, 1)) return;
            string key = services.UseButtonText(id);
            if (!string.IsNullOrEmpty(key)) mUseBtnText.SetKey(key);
            CanUseItemCount = services.Player.GetItemAmount(id);
            mIsShowUseBtn = CanUseItemCount > 0;
            RefreshView();
        }
        private long GetSingleMaxUseCount()
        { if (mSingleMaxUseCount == -1) mSingleMaxUseCount = services.ItemBatchUseMaxCount; return mSingleMaxUseCount; }
        private long GetSingleMinUseCount()
        { if (mSingleMinUseCount == -1) mSingleMinUseCount = services.ItemBatchUseMinCount; return mSingleMinUseCount; }
        private void MainPlayer_OnItemCountUpdate(IProfessionItemData data, long previous, object source)
        {
            if (data.Info.ClassId != itemId) return;
            RefreshView();
            if (CanUseItemCount < 1) services.CloseParent(gameObject);
        }
        private void RefreshView()
        {
            CanUseStateSwitcher.SetState(mIsShowUseBtn);
            CanUseItemCount = services.Player.GetItemAmount(itemId);
            if (mGetCurrentUseCountCallBack != null) CurrentUseCount = mGetCurrentUseCountCallBack();
            else if (CanUseItemCount > 0)
            {
                CurrentUseCount = services.CanUseInBatches(itemId) ? (long)Mathf.Min((float)CanUseItemCount, (float)GetSingleMaxUseCount()) : 1;
                if (itemId == 129) CurrentUseCount = (long)Mathf.Min((float)CanUseItemCount, 1f);
            }
            else CurrentUseCount = 0;
            SetSliderCondition();
            mRefreshViewCallBack?.Invoke();
        }
        private void SetSliderCondition()
        {
            bool show = CanUseItemCount > 1 && services.CanUseInBatches(itemId) && mIsShowUseBtn;
            SliderObj.SetActive(show);
            if (show) RefreshSlider(CurrentUseCount, CanUseItemCount);
        }
        private void RefreshSlider(long current, long maximum)
        {
            if (SetCountSlider is SetUseCountSlider use)
                use.Init(GetSingleMinUseCount(), maximum, current, GetSingleMaxUseCount(), "ui_fast_hang_up_max_count_tip");
            else SetCountSlider.Init(GetSingleMinUseCount(), maximum, current, 1);
            mRefreshSliderCallBack?.Invoke(current, maximum);
        }
        private void SetCountSlider_OnCurrentCountUpdate(long value)
        { CurrentUseCount = value; mValueChangedCallBack?.Invoke(value); }
        public void ShowUseBtn(bool show)
        {
            CanUseItemCount = services.Player.GetItemAmount(itemId);
            mIsShowUseBtn = (CanUseItemCount > 0) & show;
            RefreshView();
        }
        public void TryShowSlider()
        {
            bool show = CanUseItemCount > 1 && mIsShowUseBtn;
            SliderObj.SetActive(show);
            if (show) RefreshSlider(CurrentUseCount, CanUseItemCount);
        }
        private bool HandleAward(IItemUseAward award)
        {
            if (!award.IsOpenChest) return false;
            services.MergeAndShowAward(award, this);
            return true;
        }
        private void OnUseItemBtnClick()
        {
            if (mUseItemConditionCallBack != null && !mUseItemConditionCallBack()) return;
            if (CurrentUseCount > CanUseItemCount) { services.ItemNotEnough(itemId); return; }
            services.UseItem(itemId, CurrentUseCount, this, count => {
                mUseCallBack?.Invoke(count);
                if (!services.CanOverlap(itemId)) services.CloseParent(gameObject);
            });
        }
        private void OnCancelItemBtnClick() => services.CloseParent(gameObject);
        public void SetSliderValue(float value) => SetCountSlider.Slider.value = value;
        public void SetUseItemConditionCallBack(Func<bool> value) => mUseItemConditionCallBack = value;
        public void SetValueChangedCallBack(Action<float> value) => mValueChangedCallBack = value;
        public void SetRefreshSliderCallBack(Action<long, long> value) => mRefreshSliderCallBack = value;
        public void SetUseCallBack(Action<long> value) => mUseCallBack = value;
        public void SetRefreshViewCallBack(Action value) => mRefreshViewCallBack = value;
        public void SetCurrentUseCountCallBack(Func<long> value) => mGetCurrentUseCountCallBack = value;
    }
}
