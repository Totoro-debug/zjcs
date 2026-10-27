using System;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.World.NativeMap12;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap12NativeWorldView nativeMap12World;
        private OfflineMainlineSave nativeMap12ObservedSave;
        private sealed class Map12NativeState : IMap12NativeWorldState
        {
            private readonly OfficialMainlineMapRuntime host;
            public Map12NativeState(OfficialMainlineMapRuntime host) { this.host = host; }
            public Vector3 PlayerWorldPosition => host.avatar.transform.position;
            public int PlayerLevel => host.progression.Player.Level;
            public int PlayerSubRank => host.progression.Player.SubRank;
            public long PlayerCombatRating => host.progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;
            public bool IsPublicRegion(int region) => host.regions.IsPublicRegion(region);
            public int GetRegionProgress(int region) => host.regions.regionPoints.TryGetValue(region, out int points) ? points : 0;
            public int GetRegionGoddessId(int region) => host.mapEntities.Values.SingleOrDefault(e => e.Region == region && e.TypeId == 202)?.PlacementId ?? 0;
        }
        private void InitializeNativeMap12()
        {
            // CreateWorldCamera runs before the saved/default entry position
            // is applied. Bind the initial native partition to that final pose.
            worldCamera.transform.position = avatar.transform.position + new Vector3(0f, 0f, -20f);
            // MapSpineAttachmentMeshBackerRenderer assigns "Object" to all
            // original world character attachment renderers. Preserve the
            // migrated attachment draw order within that original layer.
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
                renderer.sortingLayerName = "Object";
            nativeMap12World = OfficialMap12NativeWorldView.Create(mapRoot, worldCamera, new Map12NativeState(this));
            nativeMap12ObservedSave = progressionSave.Save;
        }
        private void RefreshNativeMap12State()
        {
            if (!nativeMap12World || ReferenceEquals(nativeMap12ObservedSave, progressionSave.Save)) return;
            nativeMap12World.RefreshState();
            nativeMap12ObservedSave = progressionSave.Save;
        }
    }
}
