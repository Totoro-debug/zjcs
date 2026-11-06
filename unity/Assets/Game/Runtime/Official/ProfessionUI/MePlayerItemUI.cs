using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;

namespace Zjcs.Official.ProfessionUI
{
    // Selected client MePlayerItemUI; factory/player/resource operations cross
    // the existing native UI initialization seam through a local host adapter.
    public sealed class MePlayerItemUI : UIBase, IGameObjectPoolItem, IProfessionCurrencyItem
    {
        [SerializeField] private Image mIcon;
        [SerializeField] private MultiLanguageText mCountText;
        [SerializeField] private Button mBtn;
        [SerializeField] private Button mAddBtn;
        [SerializeField] private Color mNormalColor;
        [SerializeField] private Color mTipColor;
        [SerializeField] private Color mLimitColor;

        private IPlayerItemUIHost host;
        private IPlayerItemUIHandler mHandler;
        private IPlayerItemUICountProvider mCountProvider;
        private PlayerItemUIItemIdProvider mPlayerItemUIItemIdProvider;
        private PlayerItemType mItemType;
        private long? mCurrentCount;
        private bool mIsStopUpdateCount;
        private IDisposable iconHandle;
        public Component Component => this;

        public void Bind(IPlayerItemUIHost value) => host = value ?? throw new ArgumentNullException(nameof(value));
        private IPlayerItemUIHost Host => host ?? throw new InvalidOperationException("Original currency UI host is unbound.");

        private void Awake()
        {
            mBtn.onClick.AddListener(OnButtonClick);
            if (mAddBtn != null) mAddBtn.onClick.AddListener(OnAddBtnClick);
        }

        protected override void OnDestroy()
        {
            if (mBtn != null) mBtn.onClick.RemoveListener(OnButtonClick);
            if (mAddBtn != null) mAddBtn.onClick.RemoveListener(OnAddBtnClick);
            base.OnDestroy();
        }

        public void Init(PlayerItemType type, PlayerItemUIItemIdProvider playerItemUIItemIdProvider)
        {
            InitWrapper(() => {
                mItemType = type;
                mPlayerItemUIItemIdProvider = playerItemUIItemIdProvider;
            });
        }

        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            mHandler = Host.CreateHandler(mItemType, mPlayerItemUIItemIdProvider);
            mCountProvider = Host.CreateCountProvider(mItemType, mPlayerItemUIItemIdProvider);
            mCountProvider.Init();
            mCountProvider.OnItemCountUpdate += OnItemCountUpdate;
            Refresh();
        }

        public void SetStopUpdateCount(bool isStop)
        {
            if (mIsStopUpdateCount == isStop) return;
            mIsStopUpdateCount = isStop;
            if (mCountProvider == null)
                Debug.LogError("MePlayerItemUI CountProvider Is NULL UIBase._Inited " + _Inited
                    + " mClassId " + mPlayerItemUIItemIdProvider.ItemId + " mItemType " + mItemType);
            else if (!mIsStopUpdateCount) UpdateCount(mCountProvider.Count);
        }

        protected override void OnDisableAndInited()
        {
            iconHandle?.Dispose();
            iconHandle = null;
            if (mCountProvider != null)
            {
                mCountProvider.OnItemCountUpdate -= OnItemCountUpdate;
                mCountProvider.Release();
            }
            base.OnDisableAndInited();
        }

        private void Refresh()
        {
            iconHandle = Host.LoadIcon(mPlayerItemUIItemIdProvider.ItemId, sprite => mIcon.sprite = sprite);
            if (mAddBtn != null) mAddBtn.gameObject.SetActive(mHandler.ShowAddBtn);
            UpdateCount(mCountProvider.Count, instance: true);
        }

        private void OnItemCountUpdate(long count, bool forceUpdate) => UpdateCount(count, false, forceUpdate);

        private void UpdateCount(long count, bool instance = false, bool forceUpdate = false)
        {
            if ((instance || !mIsStopUpdateCount) && (mCurrentCount != count || forceUpdate))
            {
                mCurrentCount = count;
                string key = "{0}";
                var args = new List<object> { Host.Compact(count) };
                if (mHandler is IPlayerItemUIWithLimit withLimit && withLimit.TryGetMaxCountLimit(out long maxLimitCount))
                {
                    Color color = count >= maxLimitCount ? mTipColor : mNormalColor;
                    key = Host.ColorFormat("{0}", color) + "/" + Host.ColorFormat("{1}", mLimitColor);
                    args.Add(Host.Compact(maxLimitCount));
                }
                mCountText.SetKey(key, args.ToArray());
            }
        }

        private void OnButtonClick() => mHandler.HandleClick(Host.InputPosition);
        private void OnAddBtnClick() => mHandler.HandleAddBtn();
        public void OnGameObjectPoolReset() { }

        public void OnGameObjectPoolClear()
        {
            mPlayerItemUIItemIdProvider = default;
            mCurrentCount = null;
            mIsStopUpdateCount = false;
            ResetInitState();
            mHandler = null;
            mCountProvider = null;
        }
    }
}
