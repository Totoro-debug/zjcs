// Original CameraFollower. The local scene supplies its camera-follow target.
using UnityEngine;

namespace Zjcs.Official.HomeSceneUiControls
{
    public sealed class HomeCameraFollower : MonoBehaviour
    {
        public float myCameraCenterX;
        public float myThreshold = 1f;
        public Transform Target;
        private Vector3 mCenterPos;

        private void Start()
        {
            mCenterPos = transform.localPosition;
        }

        private void LateUpdate()
        {
            float num = Target.position.x - myCameraCenterX;
            transform.localPosition = mCenterPos + Vector3.right * (num * myThreshold);
        }
    }
}
