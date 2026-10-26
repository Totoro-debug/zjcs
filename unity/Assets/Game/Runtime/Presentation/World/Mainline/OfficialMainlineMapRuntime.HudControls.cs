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
        private void CreateMinimap(GameObject prefab)
        {
            if (prefab == null) throw new InvalidOperationException("Official Map10 minimap prefab is missing.");
            var viewRoot = new GameObject("Map10MinimapCameraRoot");
            viewRoot.transform.SetParent(transform, false);
            GameObject map = Instantiate(prefab, viewRoot.transform, false);
            map.name = "NativeMiniMap_10";
            map.SetActive(true);
            SetLayerRecursively(map, 30);
            minimapTexture = new RenderTexture(256, 256, mapClassId == 11 ? 0 : 16, RenderTextureFormat.ARGB32)
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
            if (mapClassId == 11)
            {
                InitializeNativeMiniMap();
                return;
            }
            var minimapObject = new GameObject("OfficialMap10MiniMap", typeof(RectTransform), typeof(RawImage));
            minimapObject.transform.SetParent(uiParent, false);
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

        private void BindWorldControl(string path, GameObject node)
        {
            Button button = node.GetComponent<Button>();
            if (button == null) return;
            if (path.IndexOf("/WorldStageDialog/", StringComparison.Ordinal) >= 0
                && path.EndsWith("/BattleBtn", StringComparison.Ordinal))
                button.onClick.AddListener(StartSelectedStage);
            else if (path.IndexOf("/WorldStageDialog/", StringComparison.Ordinal) >= 0
                && path.EndsWith("/CommonBtn_12", StringComparison.Ordinal))
                button.onClick.AddListener(CloseStageDialog);
            else if (path.IndexOf("MiniMap", StringComparison.OrdinalIgnoreCase) >= 0)
                button.onClick.AddListener(ToggleExpandedMinimap);
            else if (path.IndexOf("/FunctionEntryGroup/FunctionEntryFunc/", StringComparison.Ordinal) >= 0
                && (path.EndsWith("/EntryBtnFolded", StringComparison.Ordinal)
                    || path.EndsWith("/EntryBtnExpanded", StringComparison.Ordinal)))
                button.onClick.AddListener(mapClassId == 11 ? OpenNativeWorldFunctions : OpenCommerceShop);
            else if (path.IndexOf("BackBtn", StringComparison.OrdinalIgnoreCase) >= 0
                || path.EndsWith("/ReturnHomeBtn", StringComparison.Ordinal))
                button.onClick.AddListener(CloseMap);
        }

        private void OpenCommerceShop()
        {
            if (commerceScreen != null) return;
            try
            {
                commerceScreen = CommerceScreenController.OpenShop(slotId, null, CloseCommerceShop);
            }
            catch (Exception exception)
            {
                ShowOfficialNotice("商城无法打开：" + exception.Message);
            }
        }

        private void OpenCommerceFirstPay()
        {
            if (commerceScreen != null) return;
            try
            {
                commerceScreen = CommerceScreenController.OpenShop(slotId, null, CloseCommerceShop, true);
            }
            catch (Exception exception)
            {
                ShowOfficialNotice("首购无法打开：" + exception.Message);
            }
        }

        private void CloseCommerceShop()
        {
            commerceScreen = null;
            RefreshCommerceHud();
        }

        private void CreateCommerceHudEntries()
        {
            if (mapClassId == 11) { CreateNativeFirstPayEntry(); return; }
            try
            {
                Transform container = ui.Find("WorldCanvas",
                    "/WorldCanvas/Content/Content/WorldPanel/Anim/Right/EventAndPurchaseEntrys")?.transform;
                if (container == null) return;
                firstPayEntry = CommerceScreenController.CreateOfficialElement("commerce_first_entry", container,
                    (path, node) =>
                    {
                        Button button = node.GetComponent<Button>();
                        if (button == null) return;
                        if (path.EndsWith("/AccumulatedPayEntryGo/BgImg", StringComparison.Ordinal))
                            button.onClick.AddListener(OpenCommerceFirstPay);
                    });
                firstPayEntry.name = "CommerceFirstPayEntry";
                RectTransform rect = firstPayEntry.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-12f, -12f);
                Transform title = firstPayEntry.transform.Find("TitleTxt");
                if (title != null && title.GetComponent<TMPro.TMP_Text>() is TMPro.TMP_Text text)
                    text.text = "首购";
                Transform redDot = firstPayEntry.transform.Find("RedDotGo");
                if (redDot != null) redDot.gameObject.SetActive(true);
                RefreshCommerceHud();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Commerce HUD entry unavailable: " + exception.Message);
            }
        }

        private void RefreshCommerceHud()
        {
            if (firstPayEntry != null)
                firstPayEntry.SetActive(CommerceHudStatus.FirstPayShouldShow(slotId));
        }

        private void ToggleExpandedMinimap()
        {
            if (mapClassId == 11) { ToggleNativeFullMiniMap(); return; }
            if (minimapImage == null) return;
            RectTransform rect = minimapImage.rectTransform;
            bool expanded = rect.sizeDelta.x < 400f;
            rect.sizeDelta = expanded ? new Vector2(560f, 560f) : new Vector2(156f, 156f);
            rect.anchoredPosition = expanded ? Vector2.zero : new Vector2(-18f, -20f);
            rect.anchorMin = rect.anchorMax = expanded ? new Vector2(0.5f, 0.5f) : Vector2.one;
            rect.pivot = expanded ? new Vector2(0.5f, 0.5f) : Vector2.one;
        }

        private void ShowOfficialNotice(string message)
        {
            ui.SetPanelActive("MapNotifyUI", true);
            ui.SetText("MapNotifyUI", "/MapNotifyUI/Content/Text", message);
            if (noticeRoutine != null) StopCoroutine(noticeRoutine);
            noticeRoutine = StartCoroutine(HideOfficialNoticeAfterDelay(1.8f));
        }

        private IEnumerator HideOfficialNoticeAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            HideOfficialNotice();
        }

        private void HideOfficialNotice()
        {
            if (ui != null) ui.SetPanelActive("MapNotifyUI", false);
            noticeRoutine = null;
        }

        private void UpdateWorldLabels()
        {
            RefreshWorldTopBar();
            RefreshPinnedWorldTask();
            ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", characterName);
            if (mapClassId == 11)
                ui.Find("WorldCanvas", "/WorldCanvas").GetComponentInChildren<WorldPlayerCombatRatingText>(true)
                    .Refresh(progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating);
        }

        private void UpdateMinimapMarker()
        {
            if (mapClassId == 11)
            {
                RefreshNativeMiniMap();
                return;
            }
            if (minimapPlayerMarker == null || avatar == null) return;
            Vector2 local = avatar.transform.position - new Vector3(session.Definition.Navigation.Origin.X,
                session.Definition.Navigation.Origin.Y, 0f);
            float x = Mathf.Clamp01(local.x / session.Definition.Navigation.Width);
            float y = Mathf.Clamp01(local.y / session.Definition.Navigation.Height);
            minimapPlayerMarker.anchorMin = minimapPlayerMarker.anchorMax = new Vector2(x, y);
        }
    }
}
