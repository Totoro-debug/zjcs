using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private readonly Dictionary<int, OfficialBattleShadowView> attachedShadows = new Dictionary<int, OfficialBattleShadowView>();

        private void BindAttachedShadow(int unitId, int entityId, Transform body, Transform source)
        {
            // The borrowed avatar already has the independent native skeleton.
            // Sampled mesh models do not supply this bone interface.
            var renderer = source.GetComponentInChildren<ProfessionNativeSceneRenderer>(true);
            if (renderer == null) return;
            OfficialBattleShadowView shadow = OfficialBattleShadowView.Create(entityId, body, renderer);
            if (shadow != null) attachedShadows.Add(unitId, shadow);
        }

        private void ReleaseAttachedShadow(int unitId)
        {
            if (!attachedShadows.TryGetValue(unitId, out var shadow)) return;
            shadow.Dispose();
            attachedShadows.Remove(unitId);
        }

        private void ReleaseAttachedShadows()
        {
            foreach (var shadow in attachedShadows.Values) shadow.Dispose();
            attachedShadows.Clear();
        }
    }
}
