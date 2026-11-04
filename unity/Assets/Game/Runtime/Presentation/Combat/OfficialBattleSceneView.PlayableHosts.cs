using UnityEngine;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView : IBattleMasterEntityResolver
    {
        bool IBattleMasterEntityResolver.TryGetMasterEntity(GameObject character, out GameObject master)
        {
            master = null;
            if (character == null || latestCheckpoint?.Units == null) return false;
            foreach (var unit in latestCheckpoint.Units)
            {
                if (unit == null || Body(unit.Id)?.gameObject != character) continue;
                // ApplyOverrideToMaster reads this character's direct MasterId.
                // Only an entity with a live scene body can supply the override;
                // death alone does not remove an entity from the original container.
                if (FindUnit(latestCheckpoint, unit.MasterId) == null) return false;
                Transform body = Body(unit.MasterId);
                if (body == null) return false;
                master = body.gameObject;
                return true;
            }
            return false;
        }
    }
}
