using UnityEngine;

namespace Zjcs.Official.StoryPlayables
{
    [MyPlayableApply(typeof(Light))]
    public sealed class MyPlayableLight : MyPlayable
    {
        public AnimationCurve IntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float UnitIntensity = 10f;
        public int LoopIntensityCurveCount = 1;
        public float SingleDuration;

        private Light lightComponent;

        protected override void PlayImpl()
        {
            base.PlayImpl();
            lightComponent = myApplyComponent as Light
                ?? myApplyComponent?.GetComponent<Light>();
        }

        protected override bool ProcessFrameImpl(float deltaTime)
        {
            base.ProcessFrameImpl(deltaTime);
            if (lightComponent == null) return false;
            float time = Time;
            if (LoopIntensityCurveCount > 1) time %= SingleDuration;
            lightComponent.intensity = IntensityCurve.Evaluate(time / SingleDuration) * UnitIntensity;
            return false;
        }

        protected override void StopImpl()
        {
            lightComponent = null;
            base.StopImpl();
        }

        public override float GetDuration()
            => LoopIntensityCurveCount < 0 ? -1f : SingleDuration * LoopIntensityCurveCount;
    }
}
