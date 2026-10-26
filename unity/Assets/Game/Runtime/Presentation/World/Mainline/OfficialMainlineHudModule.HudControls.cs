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
        private sealed partial class OfficialMainlineHudModule
        {

            public void BindWorldControl(string path, GameObject node)
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                if (path.IndexOf("/WorldStageDialog/", StringComparison.Ordinal) >= 0
                    && path.EndsWith("/BattleBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(host.StartSelectedStage);
                else if (path.IndexOf("/WorldStageDialog/", StringComparison.Ordinal) >= 0
                    && path.EndsWith("/CommonBtn_12", StringComparison.Ordinal))
                    button.onClick.AddListener(host.CloseStageDialog);
                else if (path.IndexOf("MiniMap", StringComparison.OrdinalIgnoreCase) >= 0)
                    button.onClick.AddListener(host.miniMap.ToggleExpandedMinimap);
                else if (path.IndexOf("/FunctionEntryGroup/FunctionEntryFunc/", StringComparison.Ordinal) >= 0
                    && (path.EndsWith("/EntryBtnFolded", StringComparison.Ordinal)
                        || path.EndsWith("/EntryBtnExpanded", StringComparison.Ordinal)))
                    button.onClick.AddListener(host.MapProfile.HasNativeUi ? OpenNativeWorldFunctions : OpenCommerceShop);
                else if (path.IndexOf("BackBtn", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.EndsWith("/ReturnHomeBtn", StringComparison.Ordinal))
                    button.onClick.AddListener(host.CloseMap);
            }

            private void OpenCommerceShop()
            {
                if (commerceScreen != null) return;
                try
                {
                    commerceScreen = CommerceScreenController.OpenShop(host.slotId, null, CloseCommerceShop);
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
                    commerceScreen = CommerceScreenController.OpenShop(host.slotId, null, CloseCommerceShop, true);
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

            public void CreateCommerceHudEntries()
            {
                if (host.MapProfile.HasNativeUi) { CreateNativeFirstPayEntry(); return; }
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
                    firstPayEntry.SetActive(CommerceHudStatus.FirstPayShouldShow(host.slotId));
            }

            public void ShowOfficialNotice(string message)
            {
                ui.SetPanelActive("MapNotifyUI", true);
                ui.SetText("MapNotifyUI", "/MapNotifyUI/Content/Text", message);
                if (noticeRoutine != null) host.StopCoroutine(noticeRoutine);
                noticeRoutine = host.StartCoroutine(HideOfficialNoticeAfterDelay(1.8f));
            }

            private IEnumerator HideOfficialNoticeAfterDelay(float seconds)
            {
                yield return new WaitForSecondsRealtime(seconds);
                HideOfficialNotice();
            }

            public void HideOfficialNotice()
            {
                if (ui != null) ui.SetPanelActive("MapNotifyUI", false);
                noticeRoutine = null;
            }

            public void UpdateWorldLabels()
            {
                RefreshWorldTopBar();
                RefreshPinnedWorldTask();
                ui.SetText("WorldCanvas", "/WorldCanvas/Content/Content/WorldPanel/Anim/Left/PublicPlayerInfo/NameText", host.characterName);
                if (host.MapProfile.HasNativeUi)
                    ui.Find("WorldCanvas", "/WorldCanvas").GetComponentInChildren<WorldPlayerCombatRatingText>(true)
                        .Refresh(host.progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).CombatRating);
            }
        }
    }
}
