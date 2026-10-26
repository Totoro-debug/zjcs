using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public class WorldDropdownItem : MonoBehaviour
    {
        public List<WorldMultiLanguageText> Texts;
        public Button Button;
        public WorldStateSwitcher<bool> SelectStatus;
        private int mItemIdx;
        public event Action<int> OnItemClick;
        private event Action<int> mOnCustomItemClick;
        protected void Awake() => Button.onClick.AddListener(OnClickCallBack);
        protected void OnDestroy() => Button.onClick.RemoveListener(OnClickCallBack);
        private void OnClickCallBack() { mOnCustomItemClick?.Invoke(mItemIdx); OnItemClick?.Invoke(mItemIdx); }
        public void Init(int index, string itemsKey, bool isSelectedItem, Action<int> onCustomItemClick)
        {
            mOnCustomItemClick = onCustomItemClick;
            Refresh(itemsKey, index, isSelectedItem);
        }
        public void Refresh(string itemKey, int index, bool isSelectedItem)
        {
            SelectStatus.SetState(isSelectedItem);
            mItemIdx = index;
            foreach (WorldMultiLanguageText text in Texts) text.SetKey(itemKey);
        }
    }
}
