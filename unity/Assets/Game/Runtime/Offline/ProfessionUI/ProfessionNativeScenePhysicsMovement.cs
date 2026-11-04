using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Transform movement is sampled by the animation owner, independently of
    // mesh refreshes and material demand. Native poses use original data units.
    public sealed class ProfessionNativeScenePhysicsMovement
    {
        private bool bound;
        private Vector3 position;
        private float rotation;

        public void Reset() => bound = false;

        public void Reset(Transform host)
        {
            if (!host) throw new ArgumentNullException(nameof(host));
            position = host.position; rotation = host.eulerAngles.z; bound = true;
        }

        public void Apply(Transform host, float dataScale, Vector2 positionFactor, float rotationFactor,
            Action<float, float> translate, Action<float, float, float> rotate)
        {
            if (!host || !(dataScale > 0) || float.IsInfinity(dataScale))
                throw new ArgumentException("Bind the actual native scene transform and original data scale.");
            var nextPosition = host.position;
            float nextRotation = host.eulerAngles.z;
            if (bound)
            {
                var delta = host.InverseTransformVector(nextPosition - position) / dataScale;
                translate(delta.x * positionFactor.x, delta.y * positionFactor.y);
                float degrees = Mathf.DeltaAngle(rotation, nextRotation) * rotationFactor;
                rotate(0, 0, degrees);
            }
            position = nextPosition; rotation = nextRotation; bound = true;
        }
    }
}
