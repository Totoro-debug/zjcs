using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed partial class OfficialMainlineMiniMapModule
        {
            public void CreateMinimap(GameObject prefab)
            {
                if (prefab == null) throw new InvalidOperationException("Official Map10 minimap prefab is missing.");
                var viewRoot = new GameObject("Map10MinimapCameraRoot");
                viewRoot.transform.SetParent(host.transform, false);
                GameObject map = Instantiate(prefab, viewRoot.transform, false);
                map.name = "NativeMiniMap_10";
                map.SetActive(true);
                SetLayerRecursively(map, 30);
                minimapTexture = new RenderTexture(256, 256, host.MapProfile.HasNativeUi ? 0 : 16, RenderTextureFormat.ARGB32)
                {
                    name = "Map10OfficialMinimap",
                    filterMode = FilterMode.Bilinear
                };
                minimapTexture.Create();
                var cameraObject = new GameObject("Map10MinimapCamera");
                cameraObject.transform.SetParent(viewRoot.transform, false);
                minimapCamera = cameraObject.AddComponent<Camera>();
                minimapCamera.orthographic = true;
                minimapCamera.orthographicSize = 32f;
                minimapCamera.aspect = 1f;
                minimapCamera.clearFlags = CameraClearFlags.SolidColor;
                minimapCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                minimapCamera.cullingMask = 1 << 30;
                minimapCamera.targetTexture = minimapTexture;
                minimapCamera.transform.position = new Vector3(0f, 0f, -50f);
                if (host.MapProfile.HasNativeUi)
                {
                    InitializeNativeMiniMap();
                    return;
                }
                var minimapObject = new GameObject("OfficialMap10MiniMap", typeof(RectTransform), typeof(RawImage));
                minimapObject.transform.SetParent(host.uiParent, false);
                minimapImage = minimapObject.GetComponent<RawImage>();
                minimapImage.texture = minimapTexture;
                minimapImage.raycastTarget = false;
                RectTransform rect = minimapObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-18f, -20f);
                rect.sizeDelta = new Vector2(156f, 156f);
            }

            public void ToggleExpandedMinimap()
            {
                if (host.MapProfile.HasNativeUi) { ToggleNativeFullMiniMap(); return; }
                if (minimapImage == null) return;
                RectTransform rect = minimapImage.rectTransform;
                bool expanded = rect.sizeDelta.x < 400f;
                rect.sizeDelta = expanded ? new Vector2(560f, 560f) : new Vector2(156f, 156f);
                rect.anchoredPosition = expanded ? Vector2.zero : new Vector2(-18f, -20f);
                rect.anchorMin = rect.anchorMax = expanded ? new Vector2(0.5f, 0.5f) : Vector2.one;
                rect.pivot = expanded ? new Vector2(0.5f, 0.5f) : Vector2.one;
            }

            public void UpdateMinimapMarker()
            {
                if (host.MapProfile.HasNativeUi)
                {
                    RefreshNativeMiniMap();
                    return;
                }
                if (minimapPlayerMarker == null || host.avatar == null) return;
                Vector2 local = host.avatar.transform.position - new Vector3(host.session.Definition.Navigation.Origin.X,
                    host.session.Definition.Navigation.Origin.Y, 0f);
                float x = Mathf.Clamp01(local.x / host.session.Definition.Navigation.Width);
                float y = Mathf.Clamp01(local.y / host.session.Definition.Navigation.Height);
                minimapPlayerMarker.anchorMin = minimapPlayerMarker.anchorMax = new Vector2(x, y);
            }
        }
    }
}
