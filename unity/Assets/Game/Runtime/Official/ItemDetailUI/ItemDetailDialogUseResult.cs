using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    // Original presentation and refresh order; the rule source supplies the
    // selected loader's rows and probability/option branches.
    public sealed class ItemDetailDialogUseResult : MonoBehaviour, LoopScrollDataSource
    {
        [SerializeField] private WorldMultiLanguageText mMultiLanguageText;
        [SerializeField] private LoopVerticalScrollWithMaxHeight mLoopVerticalScrollRect;
        [SerializeField] private string mConfirmedTipKey, mOptionalTipKey, mRandomTipKey;
        [SerializeField] private AwardPanelLayoutElement mAwardPanelLayoutElement;
        [SerializeField] private WorldStateSwitcher<bool> mIsRandomStateSwitcher;
        [SerializeField] private float mProbabilityExtraCellHeight = 22;
        private readonly List<ItemUseResultEntry> mResultItemInfos = new List<ItemUseResultEntry>();
        private bool mShowProbability, mOriginCellSizeCached;
        private Vector2 mOriginCellSize;
        private CancellationTokenSource mCancellationTokenSource;
        private long mUseCount;
        private IItemUseResultBindings bindings;
        public void Bind(IItemUseResultBindings value) => bindings = value ?? throw new ArgumentNullException(nameof(value));
        public bool IsRandom { get; private set; }
        public long UseCount
        {
            get => mUseCount;
            set
            {
                if (mUseCount == value || value <= 0) return;
                mUseCount = value;
                if (IsRandom) mMultiLanguageText.SetKey(mRandomTipKey, UseCount);
                mLoopVerticalScrollRect.RefreshCells();
            }
        }
        public long PerUseCount => mResultItemInfos.Count == 0 ? 0 : mResultItemInfos[0].Count;
        public bool TryInit(ItemDetailDefinition definition)
        {
            mUseCount = 1;
            mResultItemInfos.Clear();
            mLoopVerticalScrollRect.ClearCells();
            Clear();
            if (definition == null || !definition.ShowUseDetail) return false;
            if (bindings == null) throw new InvalidOperationException("Original use-result loaders are unbound.");
            var result = bindings.LoadUseResult(definition.Info.ClassId);
            if (result == null) return false;
            mResultItemInfos.AddRange(result.Entries);
            IsRandom = result.IsRandom;
            mShowProbability = result.ShowProbability;
            mIsRandomStateSwitcher.SetState(result.IsEqualProbability && !mShowProbability);
            ApplyProbabilityCellHeight(mShowProbability);
            if (result.OptionCount > 0) mMultiLanguageText.SetKey(mOptionalTipKey, result.OptionCount);
            else if (IsRandom) mMultiLanguageText.SetKey(mRandomTipKey, UseCount);
            else mMultiLanguageText.SetKey(mConfirmedTipKey);
            if (mResultItemInfos.Count <= 0) return false;
            mCancellationTokenSource = new CancellationTokenSource();
            Refresh(mCancellationTokenSource.Token).Forget();
            return true;
        }
        private async UniTaskVoid Refresh(CancellationToken cancellationToken)
        {
            mAwardPanelLayoutElement.SetCurrentRowNumByCellNum(mResultItemInfos.Count);
            mLoopVerticalScrollRect.dataSource = this;
            mLoopVerticalScrollRect.totalCount = mResultItemInfos.Count;
            await UniTask.WaitUntil(() => mLoopVerticalScrollRect.isActiveAndEnabled
                && mAwardPanelLayoutElement.isActiveAndEnabled, (PlayerLoopTiming)8, cancellationToken);
            mLoopVerticalScrollRect.RefreshCells();
            Clear();
        }
        private void OnDisable() => Clear();
        private void OnDestroy() => Clear();
        private void Clear()
        {
            if (mCancellationTokenSource == null) return;
            mCancellationTokenSource.Cancel(); mCancellationTokenSource.Dispose(); mCancellationTokenSource = null;
        }
        void LoopScrollDataSource.ProvideData(Transform transform, int index)
        {
            var row = mResultItemInfos[index];
            var cell = transform.GetComponent<UseResultIcon>();
            long count = IsRandom ? row.Count : unchecked(row.Count * UseCount);
            cell.SetProbability(mShowProbability ? row.Probability : (double?)null);
            cell.SetItem(row.ItemId, count, row.FromId, true);
        }
        private void ApplyProbabilityCellHeight(bool showProbability)
        {
            var grid = mAwardPanelLayoutElement ? mAwardPanelLayoutElement.ContentLayoutGroup : null;
            if (!grid) return;
            if (!mOriginCellSizeCached) { mOriginCellSize = grid.cellSize; mOriginCellSizeCached = true; }
            var size = mOriginCellSize;
            if (showProbability) size.y += mProbabilityExtraCellHeight;
            grid.cellSize = size;
            mAwardPanelLayoutElement.MarkHeightDirty();
        }
    }
}
