using System;
using UnityEngine;
using Zjcs.Official.SkillsUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    // The selected challenge reward follows TryInit's !ShowUseDetail return.
    // Preserve the native fields and the clear/reset order before that return.
    public sealed class ItemDetailDialogUseResult : MonoBehaviour
    {
        [SerializeField] private WorldMultiLanguageText mMultiLanguageText;
        [SerializeField] private LoopVerticalScrollWithMaxHeight mLoopVerticalScrollRect;
        [SerializeField] private string mConfirmedTipKey, mOptionalTipKey, mRandomTipKey;
        [SerializeField] private AwardPanelLayoutElement mAwardPanelLayoutElement;
        [SerializeField] private WorldStateSwitcher<bool> mIsRandomStateSwitcher;
        [SerializeField] private float mProbabilityExtraCellHeight = 22;
        public bool IsRandom => false;
        public long UseCount { get; private set; }
        public long PerUseCount => 0;
        public bool TryInit(ItemDetailDefinition definition)
        {
            UseCount = 1;
            mLoopVerticalScrollRect.ClearCells();
            if (definition == null || !definition.ShowUseDetail) return false;
            throw new NotSupportedException("Migrate the original use-result loaders before showing this item's use details.");
        }
    }
}
