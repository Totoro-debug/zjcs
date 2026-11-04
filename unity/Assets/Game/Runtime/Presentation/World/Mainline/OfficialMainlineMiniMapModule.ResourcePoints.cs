using System;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Offline.World.MainlineMiniMap;
using Zjcs.Offline.World.NativeLabelResources;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativeMap13MiniMapResources;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineMiniMapModule
        {
            private MainlineMiniMapMarkerCatalog resourceMarkerCatalog;
            private MainlineMapLabelContext resourcePointLabels;
            private LocalMiniMapResourcePointContext resourcePointContext;
            private MiniMapResourcePointManager nativeResourcePoints;

            private void InitializeNativeResourcePoints(Transform parent)
            {
                resourceMarkerCatalog = new MainlineMiniMapMarkerCatalog(
                    Resources.Load<TextAsset>("MainlineMiniMap/current-marker-data"),
                    Resources.Load<NativeMap13AssetCatalog>("MainlineMiniMap/current-marker-assets"));
                EnsureMiniMapFilterState();
                var definition = host.session.Definition;
                var world = new MainlineMiniMapWorldData(new[] { definition },
                    Resources.Load<TextAsset>("MainlineMiniMap/current-region-displays"));
                if (!world.TryGetMapLiteData(host.mapClassId, out var lite))
                    throw new InvalidOperationException("Original minimap world data is not bound.");
                var data = new Map13LabelData
                {
                    manifest = definition.SourceManifest,
                    mapClassId = host.mapClassId,
                    width = lite.MapGroundSize.X,
                    height = lite.MapGroundSize.Y,
                    origin = new[] { lite.MapGroundOriginPos.x, lite.MapGroundOriginPos.y, lite.MapGroundOriginPos.z },
                    suppressUnlockRank = (SubRank)miniMapUiData.suppressUnlockRank,
                    regions = world.GetMapRegionDict(host.mapClassId).Values.Select(row => row.LabelInfo).ToArray(),
                    maxProgress = definition.MaxRegionProgress.OrderBy(row => row.Region).Select(row => row.Value).ToArray(),
                    regionGrid = Enumerable.Range(0, lite.MapGroundSize.Y).SelectMany(y =>
                        Enumerable.Range(0, lite.MapGroundSize.X).Select(x =>
                            world.AreaToRegion(host.mapClassId, lite.PosDataArray[y][x].Area))).ToArray()
                };
                var font = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-text-catalog")
                    .Load<TMP_FontAsset>("Assets/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset");
                resourcePointLabels = new MainlineMapLabelContext(data, new MainlineNativeGroundState(host), host,
                    Resources.Load<CombatRatingConfig>("Map13/Native/combat-rating-config"), font,
                    new LocalLanguageFiles(Application.streamingAssetsPath),
                    new OfflineCharacterRankRegionTime(definition, () => host.progressionSave.Save.Player, () => DateTime.Now));
                var assets = Resources.Load<MiniMapResourcePointAssets>(host.MapProfile.Resource("Native/current-resource-point-assets"));
                resourcePointContext = new LocalMiniMapResourcePointContext(assets, resourcePointLabels);
                GameObject instance = Instantiate(assets.ManagerPrefab, parent, false);
                nativeResourcePoints = instance.GetComponent<MiniMapResourcePointManager>();
                nativeResourcePoints.Init(resourcePointContext);
                SetLayerRecursively(instance, 30);
            }

            private void RefreshNativeResourcePoints()
            {
                if (nativeResourcePoints == null) return;
                resourcePointLabels.Refresh();
                bool current = fullMiniMapPanel == null || fullMiniMapClassId == host.mapClassId;
                nativeResourcePoints.gameObject.SetActive(current);
                if (!current) return;
                int boardCount = nativeResourcePoints.transform.childCount;
                foreach (var layer in nativeResourcePoints.ForeachLayer())
                {
                    bool selected = miniMapFilters.TryGetValue(layer.Key.ToString(), out bool value) && value;
                    if (selected) nativeResourcePoints.Select(layer.Key);
                    else nativeResourcePoints.CancelSelect(layer.Key);
                }
                // Original payloads use MiniMap layer 20. The existing host owns
                // a separate camera on layer 30; include lazily created boards.
                if (nativeResourcePoints.transform.childCount != boardCount)
                    SetLayerRecursively(nativeResourcePoints.gameObject, 30);
            }

            private void DisposeNativeResourcePoints()
            {
                if (nativeResourcePoints != null)
                {
                    nativeResourcePoints.gameObject.SetActive(false);
                    Destroy(nativeResourcePoints.gameObject);
                    nativeResourcePoints = null;
                }
                resourcePointContext?.Dispose();
                resourcePointContext = null;
                resourcePointLabels?.Dispose();
                resourcePointLabels = null;
                resourceMarkerCatalog = null;
            }
        }
    }
}
