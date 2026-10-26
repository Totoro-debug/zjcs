// Port of client-source/MaintainGlobalScale.cs; decompiler syntax and diagnostic logging adapted.
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    [ExecuteAlways]
    public class WorldMaintainGlobalScale : MonoBehaviour
    {
        [SerializeField] private Vector3 mFixedGlobalScale;
        private Vector3 mLastGlobalScale;
        private Vector3 mOriginFixedGlobalScale;
        private void Awake() => mOriginFixedGlobalScale = mFixedGlobalScale;
        protected virtual void Start() => UpdateLocalScale();
        private void LateUpdate()
        {
            if (mLastGlobalScale != transform.lossyScale) UpdateLocalScale();
        }
        protected virtual Vector3 GetParentLossyScale() => transform.parent == null
            ? Vector3.zero : transform.parent.lossyScale;
        protected void UpdateLocalScale()
        {
            if (mFixedGlobalScale == transform.lossyScale) return;
            Vector3 parent = GetParentLossyScale();
            transform.localScale = HasZeroScale(parent) ? mFixedGlobalScale
                : new Vector3(SafeDivide(mFixedGlobalScale.x, parent.x),
                    SafeDivide(mFixedGlobalScale.y, parent.y), SafeDivide(mFixedGlobalScale.z, parent.z));
            mLastGlobalScale = transform.lossyScale;
        }
        protected float SafeDivide(float a, float b) => Mathf.Abs(b) > Mathf.Epsilon ? a / b : a;
        protected bool HasZeroScale(Vector3 scale) => Mathf.Abs(scale.x) < Mathf.Epsilon || Mathf.Abs(scale.y) < Mathf.Epsilon;
        private void OnValidate()
        {
            foreach (WorldMaintainGlobalScale value in GetComponentsInChildren<WorldMaintainGlobalScale>())
                if (value != this) Debug.LogError("MaintainGlobalScale exists in a child; remove the current component: " + gameObject.name);
        }
        public void RevertFixedScale() => mFixedGlobalScale = mOriginFixedGlobalScale;
        public void SetFixedScale(Vector3 value) => mFixedGlobalScale = value;
    }
}
