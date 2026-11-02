using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.World;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Editor.MainlineNativeGround
{
    public static partial class NativeGroundAcceptance
    {
        private static IEnumerator VerifyMiniMapRegions(string output, RenderPipelineAsset pipeline)
        {
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = null;
            foreach (int mapId in new[] { 13, 16 })
            {
                var assets = Resources.Load<MainlineMiniMapRegionAssets>($"Map{mapId}/Native/mini-map-regions");
                Require(assets && assets.MapClassId == mapId, "Original regional minimap catalog.");
                var state = new MiniMapState(mapId, assets.Regions.Length);
                var parent = new GameObject("Original regional minimap fixture");
                var cameraObject = new GameObject("Regional minimap render fixture");
                var camera = cameraObject.AddComponent<Camera>();
                // The original MiniMap camera selects renderer 1 rather than
                // the default world renderer. Preserve that rendering path.
                cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(1);
                camera.orthographic = true;
                camera.cullingMask = 1 << 16;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.07f, .09f, .12f, 1);
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                var target = new RenderTexture(432, 768, 24, RenderTextureFormat.ARGB32);
                target.Create(); camera.targetTexture = target;
                using (var view = new MainlineMiniMapRegions(parent.transform, assets, state))
                {
                    for (int i = 0; i < 6; i++) yield return null;
                    var tiles = parent.GetComponentsInChildren<Tilemap>();
                    Require(tiles.Length == assets.Regions.Length && tiles.Sum(tile => tile.GetUsedTilesCount()) > 0,
                        "The original manager loads every configured regional Tilemap.");
                    Require(tiles.All(tile => tile.transform.parent.localPosition.z == -1), "Original private-region Z.");
                    var bounds = tiles.Select(tile => tile.GetComponent<TilemapRenderer>().bounds).ToArray();
                    var frame = bounds[0]; foreach (var bound in bounds.Skip(1)) frame.Encapsulate(bound);
                    var sample = tiles[0];
                    var renderer = sample.GetComponent<TilemapRenderer>();
                    Debug.Log($"Original minimap {mapId} frame {frame}, tile bounds {sample.localBounds}, sprite {sample.GetUsedSpritesCount()}, layer {sample.gameObject.layer}, material {renderer.sharedMaterial}, shader supported {renderer.sharedMaterial.shader.isSupported}, renderer {renderer.enabled}.");
                    camera.transform.position = new Vector3(frame.center.x, frame.center.y, -50);
                    camera.orthographicSize = Mathf.Max(frame.extents.y, frame.extents.x / (432f / 768f)) * 1.05f;
                    Capture(camera, target, Path.Combine(output, $"map{mapId}-minimap-regions-private.png"), 2);
                    state.Statuses[0] = MapRegionStatus.Public;
                    view.Refresh();
                    var first = tiles.Single(tile => tile.gameObject.name == "RegionGround_0(Clone)");
                    Require(first.transform.parent.localPosition.z == 0
                        && tiles.Where(tile => tile != first).All(tile => tile.transform.parent.localPosition.z == -1),
                        "Original status event switches only the selected region Z.");
                    var ids = tiles.Select(tile => tile.GetInstanceID()).OrderBy(value => value).ToArray();
                    state.Range.SetVisible(false);
                    Require(parent.GetComponentsInChildren<Tilemap>().Length == 0, "Original manager hides out-of-range regions.");
                    yield return new WaitForSecondsRealtime(.1f);
                    state.Range.SetVisible(true);
                    Require(ids.SequenceEqual(parent.GetComponentsInChildren<Tilemap>().Select(tile => tile.GetInstanceID()).OrderBy(value => value)),
                        "Original inactive cache is reused before its deadline.");
                    state.Range.SetVisible(false);
                    var previousRange = state.Range;
                    state.Range = new FixtureRange();
                    state.Range.SetVisible(false);
                    view.Refresh();
                    float retireAt = Time.realtimeSinceStartup + 10.25f;
                    while (Time.realtimeSinceStartup < retireAt)
                    {
                        previousRange.SetVisible(true);
                        previousRange.SetVisible(false);
                        view.Refresh();
                        Require(parent.GetComponentsInChildren<Tilemap>().Length == 0,
                            "A replaced range handler must not reactivate the current hidden regions.");
                        yield return null;
                    }
                    Require(parent.GetComponentsInChildren<Tilemap>(true).Length == 0, "Original ten-second inactive retirement.");
                    state.Range.SetVisible(true);
                    yield return null;
                    Require(parent.GetComponentsInChildren<Tilemap>().Length == assets.Regions.Length,
                        "Original regions reload after retirement.");
                }
                yield return null;
                Require(parent.transform.childCount == 0, "Regional minimap releases its original instance.");
                using (var view = new MainlineMiniMapRegions(parent.transform, assets, state))
                {
                    for (int i = 0; i < 6; i++) yield return null;
                    var first = parent.GetComponentsInChildren<Tilemap>().Single(tile => tile.gameObject.name == "RegionGround_0(Clone)");
                    Require(first.transform.parent.localPosition.z == 0, "Remount restores public-region state.");
                    Capture(camera, target, Path.Combine(output, $"map{mapId}-minimap-regions-remount.png"), 2);
                }
                yield return null;
                camera.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(parent); UnityEngine.Object.Destroy(cameraObject);
                state.Range.Dispose();
                yield return null;
                Require(errors == 0, "Original regional minimap lifecycle logged errors.");
            }
            GraphicsSettings.defaultRenderPipeline = previousPipeline;
            QualitySettings.renderPipeline = previousQuality;
            Debug.Log("Shared original regional minimaps passed for Map13 and Map16: all native Tilemaps, public Z events, range/cache reuse, ten-second retirement, reload, disposal and remount. This fixture does not cover masks, markers, UI/input, formal map saves or a Windows Player.");
        }

        private sealed class MiniMapState : IMainlineMiniMapRegionState
        {
            public readonly MapRegionStatus[] Statuses;
            public FixtureRange Range = new FixtureRange();
            public MiniMapState(int mapId, int regions) { CurrentMapClassId = mapId; Statuses = Enumerable.Repeat(MapRegionStatus.Private, regions).ToArray(); }
            public int CurrentMapClassId { get; }
            public bool HasMainPlayer => true;
            public IMiniMapCharacterController Character { get; } = new FixtureCharacter();
            public IMapViewRangeHandler ViewRange => Range;
            public IReadOnlyList<MapRegionStatus> RegionStatuses => Statuses;
        }
        private sealed class FixtureCharacter : IMiniMapCharacterController
        {
            public MyGridTransform MyGridTransform => throw new InvalidOperationException("Regional geometry must not consume an entity movement surface.");
        }
        private sealed class FixtureRange : IMapViewRangeHandler
        {
            public bool Visible { get; private set; } = true;
            public void SetVisible(bool visible) { Visible = visible; OnVisibleRangeUpdate.Invoke(this); }
            public GAction<IMapViewRangeHandler> OnVisibleRangeUpdate { get; set; } = new GAction<IMapViewRangeHandler>();
            public IReadOnlyList<Pos2d> VisiblePosList => null;
            public bool IsEntityInViewRange(IMiniMapEntityBody body) => Visible;
            public bool IsPosInViewRange(Pos2d pos) => Visible;
            public bool IsRangeInViewRange(Pos2d min, Pos2d max) => Visible;
            public void Dispose() { }
        }
    }
}
