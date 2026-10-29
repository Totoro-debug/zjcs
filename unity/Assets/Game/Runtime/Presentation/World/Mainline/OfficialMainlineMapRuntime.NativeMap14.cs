using System;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.World.NativeMap14;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap14NativeWorldView nativeMap14World;
        private float nativeMap14RefreshAt;

        private sealed class Map14NativeState : IMap14NativeWorldState
        {
            private readonly OfficialMainlineMapRuntime host;

            public Map14NativeState(OfficialMainlineMapRuntime host) { this.host = host; }
            public Vector3 PlayerWorldPosition => host.avatar.transform.position;
            public int PlayerLevel => host.progression.Player.Level;
            public int PlayerSubRank => host.progression.Player.SubRank;
            public long PlayerCombatRating => host.progression.EvaluateProps(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;
            public bool IsPublicRegion(int region) => host.regions.IsPublicRegion(region);
            public int GetRegionProgress(int region) => host.regions.regionPoints.TryGetValue(region, out int points)
                ? points : 0;
            public int GetRegionGoddessId(int region) => host.mapEntities.Values
                .SingleOrDefault(entity => entity.Region == region && entity.TypeId == 202)?.PlacementId ?? 0;
        }

        private void InitializeNativeMap14()
        {
            if (mapClassId != 14 || avatar == null || worldCamera == null)
                throw new InvalidOperationException("The native Map14 world requires its initialized player and camera.");
            nativeMap14World = OfficialMap14NativeWorldView.Create(mapRoot, worldCamera, new Map14NativeState(this));
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
        }

        private void RefreshNativeMap14State()
        {
            if (nativeMap14World == null || Time.unscaledTime < nativeMap14RefreshAt) return;
            nativeMap14RefreshAt = Time.unscaledTime + 0.25f;
            nativeMap14World.RefreshState();
        }

        private void DisposeNativeMap14()
        {
            nativeMap14World?.Dispose();
            nativeMap14World = null;
        }
    }
}
