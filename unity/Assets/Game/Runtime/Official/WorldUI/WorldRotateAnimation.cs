using UnityEngine;
using UnityEngine.Serialization;

namespace Zjcs.Official.WorldUI
{
    // launch-source/RotateAnimation.cs; retain local-space, delta-time rotation.
    [AddComponentMenu("M88/Effect/Rotate Animation")]
    public sealed class WorldRotateAnimation : MonoBehaviour
    {
        [FormerlySerializedAs("mySpeeds")]
        [SerializeField] private Vector3 mSpeeds = new Vector3(0f, 100f, 0f);

        private void Start() { }

        private void Update()
        {
            if (mSpeeds.x != 0f || mSpeeds.y != 0f || mSpeeds.z != 0f)
                transform.Rotate(mSpeeds * Time.deltaTime, Space.Self);
        }

        public void SetSpeeds(Vector3 speeds) { mSpeeds = speeds; }
    }
}
