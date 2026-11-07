using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemDetailDialogAddition : UIBase
    {
        [SerializeField] private OutputAdditionDetailButton mAdditionDetailButton;
        private ItemDetailDefinition definition;
        public void Init(ItemDetailDefinition value) => InitWrapper(() => definition = value);
        protected override void OnEnableAndInited()
        {
            if (!mAdditionDetailButton) return;
            mAdditionDetailButton.gameObject.SetActive(false);
            if (definition.HasOutputAdditionGroup)
                throw new NotSupportedException("Bind the original output-addition group before showing this item.");
        }
    }
}
