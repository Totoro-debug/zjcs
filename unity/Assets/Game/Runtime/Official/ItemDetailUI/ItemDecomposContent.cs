using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemDecomposContent : UIBase
    {
        [SerializeField] private WorldStateSwitcher<bool> mShowDecomposeStateSwitcher;
        [SerializeField] private Button mDecomposeBtn, mCancleBtn;
        private Action decompose, cancel;
        public void Bind(Action split, Action close) { decompose = split; cancel = close; }
        private void Awake()
        {
            if (mDecomposeBtn) mDecomposeBtn.onClick.AddListener(OnDecomposeClick);
            if (mCancleBtn) mCancleBtn.onClick.AddListener(OnCancelItemBtnClick);
        }
        protected override void OnDestroy()
        {
            if (mDecomposeBtn) mDecomposeBtn.onClick.RemoveListener(OnDecomposeClick);
            if (mCancleBtn) mCancleBtn.onClick.RemoveListener(OnCancelItemBtnClick);
            base.OnDestroy();
        }
        public void SetDecomposShow(bool value) => mShowDecomposeStateSwitcher.SetState(value);
        private void OnCancelItemBtnClick() => (cancel ?? throw new InvalidOperationException("Original item window close is unbound."))();
        private void OnDecomposeClick() => (decompose ?? throw new NotSupportedException("Bind the original recycle confirmation for recyclable items."))();
    }
}
