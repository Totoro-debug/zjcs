using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.MapGridVisibility;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private static IEnumerator VerifyLiveVisibility(MainlineNativeMiniMapRoot root,
            OfflineMapGridVisibility visibility, MainlineMiniMapVisibilityAgent agent,
            OfflineMapGridVisibility.Configuration config, OfflineMapGridVisibility.BodyConfiguration bodies,
            Map13DescriptionFixture description, Pos2d center)
        {
            var mask = root.GetComponentInChildren<MiniMapMaskManager>(true);
            Require(mask && mask.isActiveAndEnabled, "Original live minimap mask is active");
            var target = description.GetMapRegionPosList(13)[38].First(position =>
                !visibility.IsVisible(position) && mask.MaskTilemap.HasTile(root.GetTilePosByGrid(13, position)));
            int notifications = 0;
            Action<Pos2d, bool, bool> observe = (position, previous, value) => notifications++;
            agent.OnPosVisibilityUpdate += observe;
            visibility.MovePlayer(target);
            yield return null;
            yield return null;
            Require(notifications > 0 && visibility.IsVisible(target)
                && !mask.MaskTilemap.HasTile(root.GetTilePosByGrid(13, target)),
                "World movement removes the original minimap tile through the live event bridge");
            visibility.MovePlayer(center);
            visibility.UnlockRegion(38);
            yield return null;
            yield return null;
            Require(visibility.RegionPositions(38).All(visibility.IsVisible), "Original region reveal updates world state");
            Require(description.GetMapRegionPosList(13)[38].Where(visibility.IsVisible).All(position =>
                !mask.MaskTilemap.HasTile(root.GetTilePosByGrid(13, position))), "Original minimap follows region reveal");

            string path = Path.Combine(Workspace, "live-visibility-save.bin");
            File.WriteAllBytes(path, visibility.Snapshot());
            using (var restored = new OfflineMapGridVisibility(config, bodies, center,
                File.ReadAllBytes(path), new[] { 38 }, description.QueryRegion, id => false))
            {
                Require(restored.IsVisible(target) && restored.Snapshot().SequenceEqual(visibility.Snapshot()),
                    "World visibility cold read preserves the exact original RLE snapshot");
                var otherObject = new GameObject("RestoredWorldVisibility");
                var otherAgent = otherObject.AddComponent<MainlineMiniMapVisibilityAgent>();
                otherAgent.Bind(restored.Component);
                Require(ReferenceEquals(otherAgent.Component, restored.Component)
                    && !ReferenceEquals(otherAgent.Component, agent.Component), "Independent world instances keep their own live state");
                otherAgent.Dispose();
                // Disposing the borrowed minimap input must not end world visibility.
                restored.MovePlayer(target);
                Require(restored.IsVisible(target), "World visibility survives minimap input disposal");
                UnityEngine.Object.Destroy(otherObject);
            }
            agent.OnPosVisibilityUpdate -= observe;
            Debug.Log("MAINLINE_MINIMAP_LIVE_VISIBILITY_PASS");
        }
    }
}
