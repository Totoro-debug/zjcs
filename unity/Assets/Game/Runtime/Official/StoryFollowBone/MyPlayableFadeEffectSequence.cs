using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

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

        private CancellationTokenSource fadeCancellation;
        private int currentIndex;

        private void OnDestroy() => StopFadeTask();

        public override void Play(MyPlayable playable)
        {
            base.Play(playable);
            StopFadeTask();
            currentIndex = 0;
            fadeCancellation = new CancellationTokenSource();
            PlayEffects(fadeCancellation.Token).Forget();
        }

        public void StopFadeTask()
        {
            if (fadeCancellation == null) return;
            fadeCancellation.Cancel();
            fadeCancellation.Dispose();
            fadeCancellation = null;
        }

        private async UniTaskVoid PlayEffects(CancellationToken cancellationToken)
        {
            float time = 0f;
            MyPlayableFadeEffectCfg cfg = GetNextEffectCfg();
            while (cfg != null)
            {
                time += Time.deltaTime;
                if (time >= cfg.StartTime)
                {
                    if (cfg.Effect) cfg.Effect.FadeOut(cfg.Duration);
                    cfg = GetNextEffectCfg();
                    if (cfg == null)
                    {
                        StopFadeTask();
                        return;
                    }
                }
                await UniTask.NextFrame(cancellationToken, false);
            }
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
