using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed class HomeSkyCameraFollower : MonoBehaviour
    {
        private Transform target;
        private Vector3 centerPosition;
        private float centerX;
        private float threshold;
        private bool initialized;

        public void Initialize(Transform target, float centerX, float threshold)
        {
            this.target = target;
            this.centerX = centerX;
            this.threshold = threshold;
            centerPosition = transform.localPosition;
            initialized = target != null;
        }

        private void LateUpdate()
        {
            if (!initialized || target == null) return;
            float offset = (target.position.x - centerX) * threshold;
            transform.localPosition = centerPosition + Vector3.right * offset;
        }
    }
}
