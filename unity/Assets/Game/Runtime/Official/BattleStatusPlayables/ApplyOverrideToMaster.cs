using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Official.BattleStatusPlayables
{
    public class ApplyOverrideToMaster : ApplyOverrideSourceOrTarget, IBattleMasterEntityOverridePlayable
    {
        private IBattleMasterEntityResolver masterResolver;

        public void BindMasterEntityResolver(IBattleMasterEntityResolver resolver) => masterResolver = resolver;

        protected override bool TryGetOverrideGameObject(MyPlayable playable, out GameObject overrideObject)
        {
            GameObject role = UseSourceDataToOverride ? playable.mySource : playable.myTarget;
            if (role != null && masterResolver != null
                && masterResolver.TryGetMasterEntity(role, out overrideObject) && overrideObject != null)
                return true;
            overrideObject = null;
            return false;
        }
    }
}
