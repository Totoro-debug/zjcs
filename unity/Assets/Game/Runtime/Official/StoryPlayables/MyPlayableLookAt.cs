using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    public enum PlayableLookAtDirection { Up, Down, Left, Right }

    public class MyPlayableLookAt : MyPlayable
    {
        public float Duration;
        public AnimationCurve Curve;
        public PlayableLookAtDirection InitDirection = PlayableLookAtDirection.Right;
        public float AngleOffset;
        public bool FollowSourceFlipX = true;

        private Transform targetTransform;
        private float angle;

        protected override void PlayImpl()
        {
            base.PlayImpl();
            targetTransform = myApplyComponent as Transform;
            if (targetTransform == null) return;
            Vector2 initial = InitDirection switch
            {
                PlayableLookAtDirection.Up => Vector2.up,
                PlayableLookAtDirection.Down => Vector2.down,
                PlayableLookAtDirection.Left => Vector2.left,
                _ => Vector2.right
            };
            bool flipped = mySource.transform.localScale.x < 0f;
            if (FollowSourceFlipX && flipped) initial.x = -initial.x;
            Vector2 direction = GetTargetPos() - GetMyPos();
            float signedAngle = Vector3.Cross(direction, initial).z;
            angle = (signedAngle > 0f ? -1f : 1f) * Vector2.Angle(initial, direction);
            if (FollowSourceFlipX && flipped) angle = -angle;
        }

        protected override bool ProcessFrameImpl(float deltaTime)
        {
            base.ProcessFrameImpl(deltaTime);
            if (targetTransform == null) return true;
            if (myStatus == PlayableStatus.Play)
            {
                float progress = Time / Duration;
                targetTransform.localEulerAngles = new Vector3(0f, 0f,
                    (angle + AngleOffset) * Curve.Evaluate(progress));
            }
            return false;
        }

        public override float GetDuration() => Duration;
        protected virtual Vector2 GetMyPos() => targetTransform.position;
        protected virtual Vector2 GetTargetPos() => myTarget.transform.position;
    }
}
