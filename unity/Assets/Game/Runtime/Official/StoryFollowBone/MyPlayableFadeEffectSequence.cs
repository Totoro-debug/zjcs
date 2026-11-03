using System.Collections;
using UnityEngine;

namespace Zjcs.Official.StoryFollowBone
{
    public sealed class MyPlayableFadeEffectSequence : MyPlayableTrigger
    {
        [System.Serializable]
        public sealed class MyPlayableFadeEffectCfg
        {
            public float StartTime;
            public float Duration;
            public MyEffectFadeOut Effect;
        }

        [SerializeField]
        private MyPlayableFadeEffectCfg[] myTriggerCfgs;

        private Coroutine fadeRoutine;
        private int currentIndex;

        private void OnDestroy() => StopFadeTask();

        public override void Play(MyPlayable playable)
        {
            base.Play(playable);
            StopFadeTask();
            currentIndex = 0;
            fadeRoutine = StartCoroutine(PlayEffects());
        }

        public void StopFadeTask()
        {
            if (fadeRoutine == null) return;
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        private IEnumerator PlayEffects()
        {
            float time = 0f;
            MyPlayableFadeEffectCfg cfg = GetNextEffectCfg();
            while (cfg != null)
            {
                while (time < cfg.StartTime)
                {
                    yield return null;
                    time += Time.deltaTime;
                }

                if (cfg.Effect) cfg.Effect.FadeOut(cfg.Duration);
                cfg = GetNextEffectCfg();
                if (cfg != null)
                {
                    yield return null;
                    time += Time.deltaTime;
                }
            }

            fadeRoutine = null;
        }

        private MyPlayableFadeEffectCfg GetNextEffectCfg()
        {
            if (myTriggerCfgs == null || currentIndex >= myTriggerCfgs.Length) return null;
            MyPlayableFadeEffectCfg cfg = myTriggerCfgs[currentIndex];
            if (cfg == null) return null;
            currentIndex++;
            return cfg;
        }
    }
}
