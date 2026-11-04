using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    [ExecuteInEditMode]
    public class TransformEulerZLocker : MonoBehaviour
    {
        public float WorldEulerZ;
        public bool IsUI;
        private float mLastEulerAngleZ;
        private void Awake()
        {
            if (IsUI) ApplyUIEulerAngles();
            else ApplyMapEulerAngles();
        }
        private void Update()
        {
            if (IsUI) { ApplyUIEulerAngles(); return; }
            float z = transform.eulerAngles.z;
            if (Mathf.Abs(z - mLastEulerAngleZ) > .01f)
            {
                ApplyMapEulerAngles();
                mLastEulerAngleZ = z;
            }
        }
        private void ApplyMapEulerAngles() => transform.eulerAngles = new Vector3(0, 0, WorldEulerZ);
        private void ApplyUIEulerAngles()
        {
            Transform parent = transform.parent;
            if (parent != null) transform.localEulerAngles = new Vector3(0, 0, WorldEulerZ - parent.eulerAngles.z);
        }
    }
}
