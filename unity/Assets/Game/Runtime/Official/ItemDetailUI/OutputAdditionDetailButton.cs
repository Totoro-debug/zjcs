using System;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class OutputAdditionDetailButton : UIBase
    {
        [SerializeField] private Button mBtn;
        [SerializeField] private int mOutputGroupType;
        [SerializeField] private GameObject mContext;
        [SerializeField] private WorldMultiLanguageText mTxt;
        private bool noOutputGroup;
        public void BindNoOutputGroup(ItemDetailDefinition definition)
        {
            if (definition == null || definition.HasOutputAdditionGroup)
                throw new InvalidOperationException("This item requires original output-addition bindings.");
            noOutputGroup = true;
        }
        private void Awake()
        {
            if (!noOutputGroup) throw new InvalidOperationException("Bind the original item's output-addition branch before Awake.");
            mBtn.onClick.AddListener(OnBtnClick);
            InitWrapper(() => { });
        }
        protected override void OnEnableAndInited() => mContext.SetActive(false);
        protected override void OnDestroy()
        { mBtn.onClick.RemoveListener(OnBtnClick); base.OnDestroy(); }
        private void OnBtnClick() => throw new NotSupportedException("This ordinary item has no output-addition group.");
    }
}
