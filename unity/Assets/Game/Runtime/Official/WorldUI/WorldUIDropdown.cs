using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>Original UIDropdown. The offline input host routes global pointer-down positions
    /// to OnGlobalPointerDown; translated text/font refresh uses WorldMultiLanguageText services.</summary>
    public class WorldUIDropdown : MonoBehaviour, LoopScrollDataSource
    {
        public LoopVerticalScrollRect MyScrollRect;
        public Button DropdownButton;
        public Animator Animator;
        public RectTransform Viewport;
        public WorldMultiLanguageText HeadLabelText;
        public string AnimationWorkCondition;
        public string HeadKey;
        public List<string> ItemsKey = new List<string>();
        private HashSet<GameObject> mDropdownContainer;
        private bool mIsOn;
        private Action<int> mOnClickItem;
        private int mAnimationWorkConditionHash;
        public int SelectedItemIndex { get; private set; }
        public event Action<int> OnDropdownUpdate;

        protected void Awake()
        {
            MyScrollRect.gameObject.SetActive(true);
            MyScrollRect.dataSource = this;
            DropdownButton.onClick.AddListener(OnClickDrondownButton);
            mDropdownContainer = new HashSet<GameObject> { gameObject };
            GetChildren(gameObject, mDropdownContainer);
            mAnimationWorkConditionHash = UnityEngine.Animator.StringToHash(AnimationWorkCondition);
        }
        protected void Start() => Refresh();
        protected void OnDestroy() => DropdownButton.onClick.RemoveListener(OnClickDrondownButton);
        protected void OnEnable() { if (!string.IsNullOrEmpty(HeadKey)) SetSelectedItemKey(HeadKey); }
        public void Refresh()
        {
            if (mOnClickItem == null) mOnClickItem = OnClickItem;
            SetViewportSize();
            MyScrollRect.totalCount = ItemsKey.Count;
            int startItem = Mathf.Max(SelectedItemIndex - 1, 0);
            if (MyScrollRect.gameObject.activeSelf && MyScrollRect.totalCount > 0)
            {
                RectTransform content = MyScrollRect.content;
                content.anchorMin = new Vector2(content.anchorMin.x, 1f);
                content.anchorMax = new Vector2(content.anchorMax.x, 1f);
                MyScrollRect.RefillCells(startItem);
            }
        }
        public void OnClickItem(int itemIndex)
        {
            if (string.IsNullOrEmpty(HeadKey)) HeadLabelText.SetKey(ItemsKey[itemIndex]);
            SelectedItemIndex = itemIndex;
            OnDropdownUpdate?.Invoke(itemIndex);
            Hide();
        }
        public void SetSelectedItemKey(string headKey)
        {
            if (string.IsNullOrEmpty(HeadKey)) HeadLabelText.SetKey(headKey);
            SelectedItemIndex = GetItemIndex(headKey);
            if (SelectedItemIndex >= 0) { Refresh(); OnDropdownUpdate?.Invoke(SelectedItemIndex); }
        }
        public void SetSelectedItemIndex(int index) { if (index >= 0 && index < ItemsKey.Count) SetSelectedItemKey(ItemsKey[index]); }
        public int GetItemIndex(string key)
        {
            for (int i = 0; i < ItemsKey.Count; i++) if (ItemsKey[i] == key) return i;
            return -1;
        }
        public void OnGlobalPointerDown(Vector2 screenPosition)
        {
            if (EventSystem.current != null)
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPosition }, hits);
                foreach (RaycastResult item in hits) if (mDropdownContainer.Contains(item.gameObject)) return;
            }
            Hide();
        }
        public void OnClickDrondownButton() { if (mIsOn) Hide(); else Show(); }
        protected void OnDisable() => Hide();
        public void Show()
        {
            if (mIsOn) return;
            mIsOn = true;
            Refresh();
            Animator.SetBool(mAnimationWorkConditionHash, mIsOn);
        }
        public void Hide()
        {
            if (!mIsOn) return;
            mIsOn = false;
            Animator.SetBool(mAnimationWorkConditionHash, mIsOn);
        }
        public void ProvideData(Transform transform, int idx)
        {
            if (idx < ItemsKey.Count) transform.GetComponent<WorldDropdownItem>().Init(idx, ItemsKey[idx], idx == SelectedItemIndex, mOnClickItem);
        }
        private void GetChildren(GameObject owner, HashSet<GameObject> result)
        {
            for (int i = 0; i < owner.transform.childCount; i++)
            {
                GameObject child = owner.transform.GetChild(i).gameObject;
                GetChildren(child, result);
                result.Add(child);
            }
        }
        private void SetViewportSize()
        {
            if (Viewport == null) return;
            Vector2 size = Viewport.sizeDelta;
            size.y = Screen.height;
            Viewport.sizeDelta = size;
        }
    }
}
