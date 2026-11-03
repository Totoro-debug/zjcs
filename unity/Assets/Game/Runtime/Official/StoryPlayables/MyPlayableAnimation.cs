using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    [MyPlayableApply(typeof(Animation))]
    public sealed class MyPlayableAnimation : MyPlayable
    {
        [SerializeField]
        private string mAnimationName;

        [SerializeField]
        private float mDuration;

        protected override void PlayImpl()
        {
            base.PlayImpl();
            if (myApplyComponent is Animation animation)
                animation.Play(mAnimationName);
        }

        public override float GetDuration() => mDuration;
    }
}
