using UnityEngine;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.EntityLifecycle
{
    public sealed class MyPlayableFixFlipXTrigger : MyPlayableTrigger
    {
        [SerializeField]
        private bool mIsSourceOrTarget;

        [SerializeField]
        private bool mFixFlipX;

        public override void Play(MyPlayable playable)
        {
            base.Play(playable);
            GameObject applyObject = mIsSourceOrTarget ? playable.mySource : playable.myTarget;
            if (!applyObject) return;
            MyGridTransform transform = applyObject.GetComponent<MyGridTransform>();
            if (transform) transform.SetScaleByFlipX(mFixFlipX);
        }
    }
}
