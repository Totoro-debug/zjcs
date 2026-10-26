// Port of client-source/LayoutElementClamper.cs; decompiler casts and logging are adapted.
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WorldLayoutElementClamper : MonoBehaviour, ILayoutElement
    {
        private RectTransform mRectTransform;
        [SerializeField] private int mMaxPreferredWidth;
        [SerializeField] private int mMaxPreferredHeight;
        public RectTransform RectTransform => mRectTransform != null ? mRectTransform
            : (mRectTransform = GetComponent<RectTransform>());
        public int MaxPreferredWidth => mMaxPreferredWidth;
        public int MaxPreferredHeight => mMaxPreferredHeight;
        public float minWidth => -1f;
        public float preferredWidth
        {
            get
            {
                float value = LayoutUtility.GetLayoutProperty(RectTransform,
                    layout => ReferenceEquals(layout, this) ? -1f : layout.preferredWidth, 0f);
                return mMaxPreferredWidth < 0 ? value : Mathf.Clamp(value, 0f, mMaxPreferredWidth);
            }
        }
        public float flexibleWidth => 0f;
        public float minHeight => -1f;
        public float preferredHeight
        {
            get
            {
                float value = LayoutUtility.GetLayoutProperty(RectTransform,
                    layout => ReferenceEquals(layout, this) ? -1f : layout.preferredHeight, 0f);
                return mMaxPreferredHeight < 0 ? value : Mathf.Clamp(value, 0f, mMaxPreferredHeight);
            }
        }
        public float flexibleHeight => 0f;
        public int layoutPriority => 1;
        private void Awake()
        {
            var components = GetComponents<WorldLayoutElementClamper>();
            if (components.Length <= 1) return;
            Debug.LogError("GameObject name:" + gameObject.name
                + "\nMultiple LayoutElementClamper components found on GameObject. Removing additional components to avoid stack overflow.");
            for (int i = 1; i < components.Length; i++) DestroyImmediate(components[i]);
        }
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
    }
}
