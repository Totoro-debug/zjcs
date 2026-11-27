using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static bool ObserveAcquiredAirbornePhase(int id)
        {
            foreach (var status in FindObjectsOfType<MyFightStatus>())
            {
                var entity = status.GetComponent<MyEntity>();
                if (!entity || entity.Entity == null || entity.Entity.ClassId != id || !status.Context?.Owner) continue;
                var owner = status.Context.Owner;
                var body = owner.GetComponent<MyEntity>();
                if (!body || body.Id.EntityId != 2) continue;
                Require(CurrentStatusViewLookup.Get(status) != null,
                    "The airborne status lost its declared current view.");
                var renderer = owner.GetComponentInChildren<BattleNativeMonsterRenderer>();
                var mesh = renderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                var visible = renderer ? renderer.GetComponent<MeshRenderer>() : null;
                if (!renderer || !mesh || mesh.vertexCount == 0 || !visible || !visible.isVisible
                    || !visible.enabled || visible.forceRenderingOff) continue;
                if (id == 24269 && renderer.Animation != "floating_01") continue;
                return true;
            }
            return false;
        }

    }
}
