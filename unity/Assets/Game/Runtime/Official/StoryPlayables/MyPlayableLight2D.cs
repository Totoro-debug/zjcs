using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Zjcs.Official.StoryPlayables
{
    [MyPlayableApply(typeof(Light2D))]
    public sealed class MyPlayableLight2D : MyPlayable
    {
        public AnimationCurve IntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public float UnitIntensity = 10f;
        public int LoopIntensityCurveCount = 1;
        public float SingleDuration;

        private Light2D lightComponent;

        protected override void PlayImpl()
        {
            base.PlayImpl();
            lightComponent = myApplyComponent as Light2D
                ?? myApplyComponent?.GetComponent<Light2D>();
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
