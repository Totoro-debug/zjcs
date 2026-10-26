using System;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private GameObject nativeMiniMap;
        private TMP_Text miniMapRegionInfo, miniMapRegionProgress;
        private OfficialMap11UiData miniMapUiData;
        private GameObject nativeMiniMapCharacter, fullMiniMapPanel;
        private Camera fullMiniMapCamera;
        private Zjcs.Official.WorldUI.WorldMiniMapTouchInput fullMiniMapInput;

        // MiniMapMaskedUILoader.OnEnableAndInited / MiniMapMaskedUI.Start.
        private void InitializeNativeMiniMap()
        {
            miniMapUiData = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            OfficialMap11UiComponent loader = miniMapUiData.components.Single(value =>
                value.panel == "WorldCanvas" && value.type == "MiniMapMaskedUILoader");
            Transform slot = ui.Find("WorldCanvas", loader.fields.mContextSlot)?.transform;
            GameObject prefab = Resources.Load<GameObject>("Map11/UI/MiniMapMaskedUI");
            if (slot == null || prefab == null) throw new InvalidDataException("Official minimap loader binding is missing.");
            nativeMiniMap = Instantiate(prefab, slot, false);
            nativeMiniMap.name = "MiniMapMaskedUI";
            Transform Find(string path) => nativeMiniMap.transform.Find(path.Substring("/MiniMapMaskedUI/".Length));
            // PublicMapBranchLineBtn.IsShowBtn requires public region and line
            // records; neither exists in this local private-map session.
            var branch = miniMapUiData.components.Single(value => value.panel == "MiniMapMaskedUI"
                && value.type == "PublicMapBranchLineBtn").fields;
            Find(branch.mContent).gameObject.SetActive(false);
            OfficialMap11UiComponentFields fields = miniMapUiData.components.Single(value =>
                value.panel == "MiniMapMaskedUI" && value.type == "MiniMapMaskedUI").fields;
            minimapImage = Find(fields.mRawImage).GetComponent<RawImage>();
            minimapImage.texture = minimapTexture;
            minimapImage.raycastTarget = true;
            Find(fields.MiniMapBtn).GetComponent<Button>().onClick.AddListener(ToggleExpandedMinimap);
            fields = miniMapUiData.components.Single(value => value.panel == "MiniMapMaskedUI" && value.type == "AreaInfoUI").fields;
            miniMapRegionInfo = Find(fields.MyRegionInfoText).GetComponent<TMP_Text>();
            miniMapRegionProgress = Find(fields.MyRegionProgress).GetComponent<TMP_Text>();
            minimapCamera.orthographicSize = miniMapUiData.minimapSettings.MiniMapUICameraOrthographicSize;
            nativeMiniMapCharacter = Instantiate(Resources.Load<GameObject>("Map11/UI/MinimapCharacter"), minimapCamera.transform.parent, false);
            SetLayerRecursively(nativeMiniMapCharacter, 30);
        }

        // MiniMapCharacterManager.SetPosition / AreaInfoUI.UpdateContent.
        private void RefreshNativeMiniMap()
        {
            if (nativeMiniMap == null || avatar == null || minimapCamera == null || session == null) return;
            Vector3 position = avatar.transform.position;
            minimapCamera.transform.position = new Vector3(position.x, position.y, -50);
            nativeMiniMapCharacter.transform.position = new Vector3(position.x, position.y, 0);
            if (!OfficialMapNavigationView.TryGetCell(session.Definition.Navigation, currentGrid.X, currentGrid.Y,
                out int area, out _)) return;
            var config = regionConfigs.Values.FirstOrDefault(value => value.Areas.Contains(area));
            if (config == null) return;
            int region = config.Region;
            string Text(string key) => miniMapUiData.texts.Single(value => value.key == key).value;
            string infoFormat = miniMapUiData.panels.Single(value => value.key == "MiniMapMaskedUI").nodes
                .Single(value => value.path.EndsWith("/Info/RegionInfo", StringComparison.Ordinal)).text_value;
            miniMapRegionInfo.text = string.Format(CultureInfo.CurrentCulture, infoFormat, Text("region_11_" + region),
                string.Format(CultureInfo.CurrentCulture, Text("map_pos_info"), currentGrid.X, currentGrid.Y));
            string progressFormat = miniMapUiData.panels.Single(value => value.key == "MiniMapMaskedUI").nodes
                .Single(value => value.path.EndsWith("/AreaPrecentText", StringComparison.Ordinal)).text_value;
            int percent = regionPointLimits.TryGetValue(region, out int maximum) && maximum > 0
                && regionPoints.TryGetValue(region, out int points) ? points * 100 / maximum : 0;
            miniMapRegionProgress.text = string.Format(CultureInfo.CurrentCulture, progressFormat, percent);
        }

        private void ToggleNativeFullMiniMap()
        {
            if (fullMiniMapPanel != null) { CloseNativeFullMiniMap(); return; }
            fullMiniMapPanel = Instantiate(Resources.Load<GameObject>("Map11/UI/MiniMapFullScreenPanel"), ui.OverlayRoot, false);
            Transform Find(string path) => path == "/MiniMapFullScreenPanel" ? fullMiniMapPanel.transform
                : fullMiniMapPanel.transform.Find(path.Substring("/MiniMapFullScreenPanel/".Length));
            var fields = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel"
                && value.type == "MiniMapFullScreenPanel").fields;
            var title = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "PanelTitle").fields;
            Find(title.TitleTxt).GetComponent<TMP_Text>().text = miniMapUiData.texts.Single(value => value.key == "Mainland_11").value;
            var controller = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel"
                && value.type == "MiniMapCameraController").fields;
            var cameraSettings = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "Camera").fields;
            fullMiniMapCamera = Find(controller.mMiniMapCamera).gameObject.AddComponent<Camera>();
            fullMiniMapCamera.orthographic = true;
            fullMiniMapCamera.nearClipPlane = cameraSettings.nearClip;
            fullMiniMapCamera.farClipPlane = cameraSettings.farClip;
            fullMiniMapCamera.backgroundColor = cameraSettings.backgroundColor;
            fullMiniMapCamera.clearFlags = (CameraClearFlags)cameraSettings.clearFlags;
            fullMiniMapCamera.depth = worldCamera.depth + 1;
            fullMiniMapCamera.cullingMask = 1 << 30;
            fullMiniMapCamera.aspect = (float)Screen.width / Screen.height;
            // The original camera clips -5..5 around the minimap geometry plane.
            fullMiniMapCamera.transform.position = new Vector3(avatar.transform.position.x, avatar.transform.position.y, 0);
            var input = miniMapUiData.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "UITouchInput").fields;
            fullMiniMapInput = Find(controller.myInput).gameObject.AddComponent<Zjcs.Official.WorldUI.WorldMiniMapTouchInput>();
            fullMiniMapInput.Data = input.dragSettings;
            fullMiniMapInput.Data.Target = fullMiniMapCamera.transform;
            Slider slider = Find(fields.Slider).GetComponent<Slider>();
            slider.interactable = true;
            void Zoom(float value)
            {
                float minimum = FullMiniMapSettings.CameraControllerSetting.MinCameraOrthographicSize;
                float maximum = FullMiniMapSettings.CameraControllerSetting.MaxCameraOrthographicSize;
                fullMiniMapCamera.orthographicSize = Mathf.Clamp(value, minimum, maximum);
                slider.SetValueWithoutNotify((fullMiniMapCamera.orthographicSize - minimum) / (maximum - minimum));
                fullMiniMapInput.SetScreenOffsetScale(fullMiniMapCamera);
                fullMiniMapInput.Data.SetTargetBounds(new Bounds(fullMiniMapCamera.transform.position,
                    new Vector3(fullMiniMapCamera.orthographicSize * 2 * fullMiniMapCamera.aspect,
                        fullMiniMapCamera.orthographicSize * 2, 0)));
                RefreshNativeMiniMapMarkScale();
            }
            fullMiniMapZoom = Zoom;
            slider.onValueChanged.AddListener(value => Zoom(Mathf.Lerp(
                FullMiniMapSettings.CameraControllerSetting.MinCameraOrthographicSize,
                FullMiniMapSettings.CameraControllerSetting.MaxCameraOrthographicSize, value)));
            fullMiniMapInput.Zoom = factor => Zoom(fullMiniMapCamera.orthographicSize + factor * controller.ScaleFactor);
            InitializeNativeMiniMapTabs();
            SwitchNativeFullMiniMap(mapClassId);
            foreach (var reference in fields.references.RefIds)
            {
                var rows = reference.data.mStateGameObjectData?.GoConfig;
                if (rows == null) continue;
                var active = rows.Where(value => value.State.mValue == miniMapUiData.minimapSettings.ShowMapSwitch)
                    .SelectMany(value => value.GameObjects).ToArray();
                foreach (string path in rows.SelectMany(value => value.GameObjects).Distinct())
                    Find(path).gameObject.SetActive(active.Contains(path));
            }
            InitializeMiniMapFilter();
            Find("/MiniMapFullScreenPanel/Content/CloseBtn_dark").GetComponent<Button>().onClick.AddListener(CloseNativeFullMiniMap);
            ui.SetPanelActive("WorldCanvas", false);
        }

        private void CloseNativeFullMiniMap()
        {
            bool wasOpen = fullMiniMapPanel != null;
            if (fullMiniMapPanel != null) Destroy(fullMiniMapPanel);
            fullMiniMapPanel = null;
            fullMiniMapCamera = null;
            fullMiniMapInput = null;
            RestoreCurrentNativeMiniMap();
            RefreshNativeMiniMapMarkScale();
            if (wasOpen) ui?.SetPanelActive("WorldCanvas", true);
        }
    }
}
