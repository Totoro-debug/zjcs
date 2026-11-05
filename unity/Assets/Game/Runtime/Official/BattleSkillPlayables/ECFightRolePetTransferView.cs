using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.BattleSkillPlayables
{
    // ECFightRolePetTransferView: explicit bindings replace ECView lookup;
    // the battle clock replaces its WaitForSeconds coroutine.
    public sealed class ECFightRolePetTransferView : IDisposable
    {
        private readonly List<GameObject> mGraphGroups;
        private readonly float mTransferTime;
        private readonly GameObject mHidePlayable, mShowPlayable, source;
        private readonly IMyPlayableManager MyPlayableManager;
        private readonly Action forcePlayDiePlayable;
        private MyPlayable mPlayable;
        private float remaining = -1f;
        private bool isTransferToEntity, disposed;

        public ECFightRolePetTransferView(List<GameObject> graphGroups, float transferTime,
            GameObject hidePlayable, GameObject showPlayable, GameObject source,
            IMyPlayableManager manager, Action forcePlayDiePlayable, bool initialTransfer)
        {
            mGraphGroups = graphGroups ?? throw new ArgumentNullException(nameof(graphGroups));
            mTransferTime = transferTime;
            mHidePlayable = hidePlayable; mShowPlayable = showPlayable;
            this.source = source ? source : throw new ArgumentNullException(nameof(source));
            MyPlayableManager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.forcePlayDiePlayable = forcePlayDiePlayable;
            isTransferToEntity = initialTransfer;
            SetGraphStatus();
        }

        public void OnPetEntityStatusChange(bool isTransfer)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ECFightRolePetTransferView));
            isTransferToEntity = isTransfer;
            ResetCurrentPlayable();
            GameObject playable = isTransfer ? mHidePlayable : mShowPlayable;
            if (playable && source)
            {
                mPlayable = MyPlayableManager.Play(playable, source, source);
                mPlayable.OnStop += OnPlayableStop;
            }
            StopWaitCoroutine();
            remaining = mTransferTime;
        }

        private void OnPlayableStop(MyPlayable playable)
        {
            playable.OnStop -= OnPlayableStop;
            SetGraphStatus();
        }

        private void SetGraphStatus()
        {
            foreach (GameObject group in mGraphGroups)
                if (group) group.SetActive(!isTransferToEntity);
        }

        private void ResetCurrentPlayable()
        {
            if (mPlayable)
            {
                mPlayable.Stop();
                mPlayable.OnStop -= OnPlayableStop;
                mPlayable = null;
            }
        }

        private void StopWaitCoroutine() => remaining = -1f;

        public void Tick(float deltaTime)
        {
            if (disposed || remaining < 0f || deltaTime <= 0f) return;
            remaining -= deltaTime;
            if (remaining > 0f) return;
            remaining = -1f;
            SetGraphStatus();
            forcePlayDiePlayable?.Invoke();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ResetCurrentPlayable();
            StopWaitCoroutine();
        }
    }
}
