using System;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private MainlineNativeGround configuredNativeGround;
        private float configuredNativeGroundRefreshAt;
        private void RefreshConfiguredNativeGround()
        {
            if (configuredNativeGround == null || Time.unscaledTime < configuredNativeGroundRefreshAt) return;
            configuredNativeGroundRefreshAt = Time.unscaledTime + 0.25f;
            configuredNativeGround.Refresh();
        }
        private void InitializeConfiguredNativeGround()
        {
            var source = Resources.Load<TextAsset>(MapProfile.NativeGroundLabelResource);
            var data = source ? JsonUtility.FromJson<Zjcs.Official.World.NativeMap14Labels.Map14LabelData>(source.text) : null;
            if (data == null || data.mapClassId != mapClassId || data.regions.Length != MapProfile.RegionCount)
                throw new InvalidOperationException("Original native ground region data is not bound.");
            var shared = Zjcs.Presentation.World.NativeMap12.OfficialMap12NativeWorldAssets.Load();
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true)) renderer.sortingLayerName = "Object";
            worldCamera.transform.position = new Vector3(avatar.transform.position.x, avatar.transform.position.y, map11WorldViewSource.cameraZ);
            var prefab = Resources.Load<GameObject>(MapProfile.NativeGroundPrefabResource);
            if (!prefab) throw new InvalidOperationException("Original configured native ground prefab is not bound.");
            configuredNativeGround = new MainlineNativeGround(mapRoot, prefab, worldCamera,
                new MainlineNativeGroundState(this), data, shared.EnvironmentData, shared.Pipeline, shared.Font,
                region => progressionSave.Save.Player.UnlockedMapClassIds.Contains(mapClassId));
        }

        private sealed class MainlineNativeGroundState : IMainlineWorldGroundState
        {
            private readonly OfficialMainlineMapRuntime host;

            public MainlineNativeGroundState(OfficialMainlineMapRuntime host) { this.host = host; }
            public Vector3 PlayerWorldPosition => host.avatar.transform.position;
            public int PlayerLevel => host.progression.Player.Level;
            public int PlayerSubRank => host.progression.Player.SubRank;
            public long PlayerCombatRating => host.progression.EvaluateProps(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating;
            public bool IsPublicRegion(int region) => host.regions.IsPublicRegion(region);
            public int GetRegionProgress(int region) => host.regions.regionPoints.TryGetValue(region, out int points)
                ? points : 0;
            public void UnlockAllRegionPositions(int region)
            {
                host.mapGridVisibility?.UnlockRegion(region);
                host.SaveLocalProgress();
            }
            public int GetRegionGoddessId(int region) => host.mapEntities.Values
                .SingleOrDefault(entity => entity.Region == region && entity.TypeId == 202)?.PlacementId ?? 0;
        }

    }
}
