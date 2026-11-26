using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Official.BattleSkillPlayables
{
    public struct PlayPlayableWithSourceAndTargetData
    {
        public EcEntityIdentifier SourceId, TargetId;
    }

    [Serializable]
    public sealed class PlayPlayableWithSourceAndTargetHandler : FightViewHandlerBase<PlayPlayableWithSourceAndTargetData>
    {
        [SerializeField] private GameObject mPlayable;
        private IMyActionEntityLookup mEntities;
        private IMyPlayableManager mMyPlayableManager;

        protected override void OnInit(IFightViewHandlerHost host)
        {
            mEntities = host.ServiceProvider?.GetService(typeof(IMyActionEntityLookup)) as IMyActionEntityLookup;
            mMyPlayableManager = host.ServiceProvider?.GetService(typeof(IMyPlayableManager)) as IMyPlayableManager;
        }

        protected override void OnRelease() { mEntities = null; mMyPlayableManager = null; }

        protected override void HandleData(PlayPlayableWithSourceAndTargetData data)
        {
            if (mEntities == null) return;
            MyEntity source = mEntities.GetEntity(data.SourceId), target = mEntities.GetEntity(data.TargetId);
            if (!source || !target) Debug.LogError("[PlayPlayableWithSourceAndTargetHandler] SourceEntity or TargetEntity is invalid");
            else if (!mPlayable) Debug.LogError("[PlayPlayableWithSourceAndTargetHandler] Playable is invalid");
            else mMyPlayableManager?.Play(mPlayable, source.gameObject, target.gameObject);
        }

        public override void TryGetPlayables(ref HashSet<GameObject> playables)
        {
            if (playables != null && mPlayable) playables.Add(mPlayable);
        }
    }
}
