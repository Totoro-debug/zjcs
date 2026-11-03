using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    [MyPlayableApply(typeof(Transform))]
    public sealed class MyPlayableDefault : MyPlayable
    {
        [SerializeField]
        private float mDurationTime;

        public override float GetDuration() => mDurationTime;
    }
}
