using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private int fullMiniMapClassId;
        private GameObject currentMiniMapGeometry, browsedMiniMapGeometry;
        private Action<float> fullMiniMapZoom;
        private readonly Dictionary<int, GameObject> nativeMiniMapTabs = new Dictionary<int, GameObject>();
        private OfficialMap11MiniMapSettings FullMiniMapSettings => miniMapUiData.minimapTabs
            .Single(value => value.mapClassId == (fullMiniMapClassId > 0 ? fullMiniMapClassId : mapClassId)).settings;

        // MiniMapFullScreenPanel.RefreshActivedMapClassIdList / ProvideData.
        // The visited-map records replace MainPlayer.IsWorldMapVisited; opening a
        // tab must never create progress or unlock another map.
        private void InitializeNativeMiniMapTabs()
        {
            nativeMiniMapTabs.Clear();
            currentMiniMapGeometry = minimapCamera.transform.parent.GetComponentInChildren<OfficialMap10MiniMapView>(true).gameObject;
            var source = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel"
                && value.type == "MiniMapSwitchTabItem");
            Transform template = FullMiniMapObject(source.path);
            var assets = Resources.Load<OfficialMap11UiAssets>("Map11/UI/DynamicItems");
            var catalog = OfficialWorldMapAssetCatalog.LoadDefault();
            int group = miniMapUiData.minimapTabs.Single(value => value.mapClassId == mapClassId).group;
            foreach (var map in miniMapUiData.minimapTabs.Where(value => value.group >= 0 && value.group == group
                && progressionSave.Save.World.Maps.Any(progress => progress.MapClassId == value.mapClassId)))
            {
                if (!catalog.TryGet(map.mapClassId, out var entry) || entry.MiniMapPrefab == null) continue;
                GameObject root = Instantiate(template.gameObject, template.parent, false);
                root.name = "MiniMapTab_" + map.mapClassId;
                root.SetActive(true);
                Transform Find(string path) => TabObject(root, source.path, path);
                Find(source.fields.MapImage).GetComponent<Image>().sprite = assets.MiniMapTab(map.mapClassId);
                Find(source.fields.MapNameText).GetComponent<TMP_Text>().text = MiniMapName(map.mapClassId);
                Find(source.fields.Button).GetComponent<Button>().onClick.AddListener(() => SwitchNativeFullMiniMap(map.mapClassId));
                nativeMiniMapTabs.Add(map.mapClassId, root);
            }
            template.gameObject.SetActive(false);
        }

        private static Transform TabObject(GameObject root, string sourceRoot, string path) => path == sourceRoot
            ? root.transform : root.transform.Find(path.Substring(sourceRoot.Length + 1));
        private string MiniMapName(int id) => miniMapUiData.texts.Single(value => value.key == "Mainland_" + id).value;

        // MiniMapFullScreenPanel.SwitchMiniMap / HideCurrentMiniMap / ShowMiniMap,
        // MiniMap.OnShowFullScreenMiniMap and SetMiniMapCameraPos.
        private void SwitchNativeFullMiniMap(int id)
        {
            if (fullMiniMapPanel == null || !nativeMiniMapTabs.ContainsKey(id)) return;
            if (browsedMiniMapGeometry != null)
            {
                browsedMiniMapGeometry.SetActive(false);
                Destroy(browsedMiniMapGeometry);
                browsedMiniMapGeometry = null;
            }
            fullMiniMapClassId = id;
            bool current = id == mapClassId;
            currentMiniMapGeometry.SetActive(current);
            nativeMiniMapCharacter.SetActive(current);
            GameObject geometry = currentMiniMapGeometry;
            if (!current)
            {
                var assets = OfficialWorldMapAssetCatalog.LoadDefault().LoadMap(id);
                browsedMiniMapGeometry = Instantiate(assets.MiniMapPrefab, minimapCamera.transform.parent, false);
                browsedMiniMapGeometry.name = "NativeMiniMap_Browse_" + id;
                browsedMiniMapGeometry.SetActive(true);
                SetLayerRecursively(browsedMiniMapGeometry, 30);
                geometry = browsedMiniMapGeometry;
            }
            var renderers = geometry.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidDataException("Official minimap geometry has no renderers: " + id);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(FullMiniMapSettings.BoundExtendSize * 2f);
            fullMiniMapInput.Data.BorderBounds = bounds;
            var camera = FullMiniMapSettings.CameraControllerSetting;
            fullMiniMapZoom((camera.MinCameraOrthographicSize + camera.MaxCameraOrthographicSize) / 2);
            if (current) fullMiniMapCamera.transform.position = new Vector3(avatar.transform.position.x, avatar.transform.position.y, 0);
            fullMiniMapCamera.transform.position = fullMiniMapInput.Data.ClampPos(fullMiniMapCamera.transform.position);
            var title = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "PanelTitle").fields;
            FullMiniMapObject(title.TitleTxt).GetComponent<TMP_Text>().text = MiniMapName(id);
            var source = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "MiniMapSwitchTabItem");
            foreach (var tab in nativeMiniMapTabs)
                foreach (var reference in source.fields.references.RefIds)
                {
                    var rows = reference.data.mStateGameObjectData?.GoConfig;
                    if (rows == null) continue;
                    var active = rows.Where(value => value.State.mValue == (tab.Key == id ? 1 : 0))
                        .SelectMany(value => value.GameObjects).ToArray();
                    foreach (string path in rows.SelectMany(value => value.GameObjects).Distinct())
                        TabObject(tab.Value, source.path, path).gameObject.SetActive(active.Contains(path));
                }
            RefreshNativeMiniMapMarks();
        }

        private void RestoreCurrentNativeMiniMap()
        {
            if (browsedMiniMapGeometry != null)
            {
                browsedMiniMapGeometry.SetActive(false);
                Destroy(browsedMiniMapGeometry);
            }
            browsedMiniMapGeometry = null;
            if (currentMiniMapGeometry != null) currentMiniMapGeometry.SetActive(true);
            if (nativeMiniMapCharacter != null) nativeMiniMapCharacter.SetActive(true);
            fullMiniMapClassId = 0;
            fullMiniMapZoom = null;
            nativeMiniMapTabs.Clear();
            if (miniMapUiData != null) RefreshNativeMiniMapMarks();
        }
    }
}
