// Port of client-source/MaintainGlobalScaleUI.cs; preserve root-canvas scale compensation.
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    [ExecuteAlways]
    public sealed class WorldMaintainGlobalScaleUI : WorldMaintainGlobalScale
    {
        private RectTransform mRootCanvasRectTransform;
        private Transform mRootCanvasRectTransformParent;
        private bool mIsTransformParentDirty;
        protected override void Start()
        {
            RefreshRootCanvas();
            base.Start();
        }
        private void OnTransformParentChanged()
        {
            if (!gameObject.activeInHierarchy) mIsTransformParentDirty = true;
            else ParentChanged();
        }
        private void OnEnable()
        {
            if (mIsTransformParentDirty) ParentChanged();
        }
        private void ParentChanged()
        {
            RefreshRootCanvas();
            UpdateLocalScale();
            mIsTransformParentDirty = false;
        }
        private void RefreshRootCanvas()
        {
            if (!IsParentDirty()) return;
            Transform current = transform;
            Canvas root = null;
            while (current != null)
            {
                if (current.TryGetComponent(out Canvas canvas)) root = canvas;
                current = current.parent;
            }
            if (root != null)
            {
                mRootCanvasRectTransform = root.GetComponent<RectTransform>();
                mRootCanvasRectTransformParent = mRootCanvasRectTransform.parent;
            }
        }
        private bool IsParentDirty() => mRootCanvasRectTransform == null
            || mRootCanvasRectTransformParent != mRootCanvasRectTransform.parent
            || !transform.IsChildOf(mRootCanvasRectTransform);
        protected override Vector3 GetParentLossyScale()
        {
            Vector3 parent = base.GetParentLossyScale();
            Vector3 canvas = mRootCanvasRectTransform != null ? mRootCanvasRectTransform.localScale : Vector3.one;
            return new Vector3(SafeDivide(parent.x, canvas.x), SafeDivide(parent.y, canvas.y), SafeDivide(parent.z, canvas.z));
        }
        public void ForceUpdateScale()
        {
            mIsTransformParentDirty = true;
            mRootCanvasRectTransformParent = null;
            mRootCanvasRectTransform = null;
            RefreshRootCanvas();
            UpdateLocalScale();
        }
    }
}
