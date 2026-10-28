// Original client ECPeriodicityInteractionStateView. Local services replace
// the EC bridge, MeManager, parser and injected playable manager only.
using System;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.StelePlayables
{
    public interface IPeriodicityInteractionStateSource
    {
        int GetCurrentPeriodicityGroupId();
        int GetWorldPeriodicityInteractionCount(int groupId);
        int? GetPeriodicityMaxCount(int groupId);
        event Action<int, int> OnPeriodicityInteractionChanged;
    }

    public sealed class ECPeriodicityInteractionStateView
    {
        private readonly GameObject mCanInteractionPlayable;
        private readonly GameObject mEndInteractionPlayable;
        private readonly IPeriodicityInteractionStateSource source;
        private readonly Func<GameObject, GameObject, GameObject, MyPlayable> play;
        private readonly GameObject mOwner;
        private readonly int mClassId;
        private MyPlayable mPlayable;
        private bool mIsCanInteraction;

        public ECPeriodicityInteractionStateView(OfficialStelePlayableCatalog.Entry entry,
            GameObject owner, IPeriodicityInteractionStateSource stateSource,
            Func<GameObject, GameObject, GameObject, MyPlayable> playOriginal)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            mCanInteractionPlayable = entry.mCanInteractionPlayable;
            mEndInteractionPlayable = entry.mEndInteractionPlayable;
            mClassId = entry.entityClassId;
            mOwner = owner;
            source = stateSource ?? throw new ArgumentNullException(nameof(stateSource));
            play = playOriginal ?? throw new ArgumentNullException(nameof(playOriginal));
        }

        public void Load()
        {
            RefreshPlayable();
            source.OnPeriodicityInteractionChanged += MainPlayer_OnPeriodicityInteractionChanged;
        }

        private void MainPlayer_OnPeriodicityInteractionChanged(int periodicityGroupId, int count)
        {
            if (source.GetCurrentPeriodicityGroupId() == periodicityGroupId) RefreshPlayable();
        }

        private void RefreshPlayable(int periodicityGroupId = -1)
        {
            if (periodicityGroupId == -1) periodicityGroupId = source.GetCurrentPeriodicityGroupId();
            if (mPlayable != null)
            {
                mPlayable.Stop();
                mPlayable = null;
            }
            int count = source.GetWorldPeriodicityInteractionCount(periodicityGroupId);
            int? maxCount = source.GetPeriodicityMaxCount(periodicityGroupId);
            if (!maxCount.HasValue)
                Debug.LogError("物件 " + mClassId + " 周期交互信息获取失败periodicityGroupId:" + periodicityGroupId);
            else
                ChangeCanInteraction(count < maxCount.Value);
        }

        private void ChangeCanInteraction(bool isCan)
        {
            if (!isCan && mIsCanInteraction)
                mPlayable = play(mEndInteractionPlayable, mOwner, mOwner);
            else if (isCan)
                mPlayable = play(mCanInteractionPlayable, mOwner, mOwner);
            mIsCanInteraction = isCan;
        }

        public void Unload()
        {
            source.OnPeriodicityInteractionChanged -= MainPlayer_OnPeriodicityInteractionChanged;
        }
    }
}
