// Migrated official AutoStopOnSourceDie; see burning-grid playable source receipt.
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Object = UnityEngine.Object;

namespace Zjcs.Official.BattleGridItemPlayables
{
    public class AutoStopOnSourceDie : ApplyOnInit, IBattleSourceDeathPlayable
    {
        public float DelayStop;
        private IDisposable mSourceSubscription;
        private IBattleSourceDeathEvents sourceDeathEvents;
        private CancellationTokenSource mCancellationTokenSource = new CancellationTokenSource();

        public void BindSourceDeathView(IBattleSourceDeathView view)
        {
            sourceDeathEvents = view as IBattleSourceDeathEvents;
        }

        protected override void InitImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
        {
            // Original MyFightRoleCharacter lookup/subscription is supplied by the owning scene.
            mSourceSubscription = sourceDeathEvents?.SubscribeSourceDeath(playable.mySource,
                () => Component_OnDie(false, 0L)); // Original leaf ignores both event arguments.
        }

        protected override void OnDisable()
        {
            mSourceSubscription?.Dispose();
            mSourceSubscription = null;
            mCancellationTokenSource.Cancel();
            base.OnDisable();
        }

        private void Component_OnDie(bool arg1, long arg2)
        {
            if (DelayStop > 0f)
            {
                UniTaskVoid val = StopPlayable();
                val.Forget();
            }
            else
            {
                StopImpl();
            }
        }

        private void StopImpl()
        {
            MyPlayable component = ((Component)this).GetComponent<MyPlayable>();
            if (!((Object)(object)component == (Object)null))
            {
                component.Stop();
            }
        }

        private async UniTaskVoid StopPlayable()
        {
            // Original state-machine body, including integer milliseconds, scaled time,
            // LastPostLateUpdate (8), cancellation and immediate stop after the await.
            await UniTask.Delay((int)(DelayStop * 1000f), false, (PlayerLoopTiming)8,
                mCancellationTokenSource.Token, false);
            StopImpl();
        }

        protected override void PlayImpl(MyPlayable playable, MyPlayableApplyAttribute applyAttr)
        {
        }
    }
}
