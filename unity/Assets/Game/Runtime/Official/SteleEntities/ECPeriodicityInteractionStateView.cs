// Original serialized EC fields delegate to the migrated original state logic.
using System;
using UnityEngine;
using Zjcs.Official.StelePlayables;

namespace Zjcs.Official.SteleEntities
{
    [Serializable]
    public sealed class ECPeriodicityInteractionStateView : ECViewBase
    {
        [SerializeField] private GameObject mCanInteractionPlayable;
        [SerializeField] private GameObject mEndInteractionPlayable;
        [NonSerialized] private StelePlayables.ECPeriodicityInteractionStateView view;
        protected override void Load()
        {
            base.Load();
            view = new StelePlayables.ECPeriodicityInteractionStateView(new OfficialStelePlayableCatalog.Entry
            {
                entityClassId = mClassId, mCanInteractionPlayable = mCanInteractionPlayable,
                mEndInteractionPlayable = mEndInteractionPlayable
            }, mOwner, mEntity.Periodicity, mEntity.Play);
            view.Load();
        }
        protected override void Unload() { view?.Unload(); view = null; base.Unload(); }
    }
}
