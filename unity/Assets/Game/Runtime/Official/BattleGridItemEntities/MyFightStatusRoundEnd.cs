// Original selected client lifecycle/playable behavior; local component and service bindings.
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Official.BattleGridItemEntities
{
    public class MyFightStatusRoundEnd : MyFightStatusBase<object>
    {
        [Tooltip("状态应用者作为目标的Playable, 这个状态结束会把所有释放的Playable停止")]
        [SerializeField] private GameObject mPlayablePrefab;
        private readonly List<MyPlayable> mPlayables = new List<MyPlayable>();
        private IFightStatusPlayableState mStatus;
        public IMyPlayableManager mMyPlayableManager { private get; set; }

        public override void OnStart(bool newEntity)
        {
            base.OnStart(newEntity);
            mStatus = myStatus.Component;
            mStatus.OnInvoked += OnRoundEnd;
        }

        public override void OnEnd(object userData)
        {
            if (mStatus != null) mStatus.OnInvoked -= OnRoundEnd;
            mStatus = null;
            foreach (var playable in mPlayables) if (playable) playable.Stop();
            mPlayables.Clear();
            base.OnEnd(userData);
        }

        private void OnRoundEnd(IFightStatusPlayableState status)
        {
            if (mPlayablePrefab)
            {
                GameObject owner = myStatus.ApplyEntity;
                mPlayables.Add(mMyPlayableManager.Play(mPlayablePrefab, owner, owner));
            }
        }
    }
}
