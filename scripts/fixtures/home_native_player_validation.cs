#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeHudUI;

public sealed partial class HomeNativePlayerRunner
{
    int taskNavigationRequests;
    T HudField<T>(string name) => (T)typeof(Zjcs.Presentation.Home.HomeHudView)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home.Hud);
    IEnumerator CheckFormalPlayer()
    {
        var panel = HudField<OfflineHomePlayerPanel>("nativePlayerPanel");
        Require(panel != null && panel.Root.activeInHierarchy, "Formal entry mounts the complete original player panel");
        Require(panel.Root.GetComponentsInChildren<Transform>(true).Length == 22, "All original player-panel objects retained");
        Require(panel.Experience.Level == home.Profile.Save.Player.Level, "Original avatar XP ring has the saved player level");
        Require(taskNavigationRequests == 0, "Rendering the pinned task must not request map navigation");
        var homeUi = HudField<Zjcs.Presentation.Home.HomeUiRenderer>("homeRenderer");
        foreach (string closed in new[] { "TeamMiniPanel", "ApplyConfirmBtn", "ChatMiniPanel", "FunctionEntryChatQuick" })
            Require(homeUi.AllNodes.Where(node => node && node.name == closed).All(node => !node.activeInHierarchy),
                "Confirmed offline team/chat subtree cannot render or intercept pointer input: " + closed);
        foreach (var text in panel.Root.GetComponentsInChildren<TMP_Text>(true))
            Debug.Log("HOME_PLAYER_TEXT: " + text.name + "=" + text.text);
        yield return new WaitForSeconds(3);
        Require(panel.Root.GetComponentsInChildren<RawImage>(true).Any(image => image.texture), "Native portrait studio supplies a runtime texture");
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            Debug.Log("HOME_PLAYER_CANVAS: " + canvas.name + " mode=" + canvas.renderMode + " order=" + canvas.sortingOrder + " camera=" + canvas.worldCamera);
        foreach (var image in UnityEngine.Object.FindObjectsOfType<Image>().Where(value => value.rectTransform.rect.width > 500 && value.rectTransform.rect.height > 800))
            Debug.Log("HOME_PLAYER_LARGE_IMAGE: " + image.name + " parent=" + image.transform.parent?.name + " sprite=" + image.sprite?.name + " canvas=" + image.canvas?.name);
        yield return CaptureFrame("home-native-player.png");
        var info = panel.Icon.myIconMe.InfoUI;
        var button = info.GetComponentsInChildren<Button>(true).FirstOrDefault(value => value.isActiveAndEnabled);
        Require(button, "Original avatar has an active pointer target");
        Click(button);
        yield return new WaitForSeconds(1);
        Require(home.Hud.CharacterPageOpen && !panel.Root.activeInHierarchy, "Simulated avatar click opens character page and hides public panel");
        Require(home.Hud.TryHandleBack(), "Back closes character page");
        yield return null;
        Require(!home.Hud.CharacterPageOpen && panel.Root.activeInHierarchy, "Public panel restored after character page");
        var before = home.Profile.Save.Player.Level;
        var tables = Zjcs.Offline.HomeExperienceUI.OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
        long award = tables.LevelExpParser.GetExp(1, before + 1) - home.Profile.Save.Player.Experience;
        Require(award > 0 && home.DailyDungeonProgression.AwardExperience(award, "native-player-xp").IsApplied, "Actual saved progression commits next-level XP");
        home.Hud.RefreshAll();
        yield return new WaitForSeconds(.25f);
        yield return CaptureFrame("home-native-player-xp-animation.png");
        yield return CheckLevelNotification();
        Require(panel.Experience.Level == before + 1, "Original XP animation reaches committed level");
        home.Hud.RefreshTasks(); home.Hud.RefreshTasks();
        Require(taskNavigationRequests == 0, "Avatar return, XP and repeated task refresh do not activate task actions");
        yield return null;
        var task = HudField<Zjcs.Presentation.Home.HomeUiRenderer>("pinnedTaskRenderer");
        var jump = task.AllNodes.First(node => node && node.name.Trim() == "JumpBtn").GetComponent<Button>();
        Require(jump && jump.gameObject.activeInHierarchy && jump.interactable, "Current pinned task exposes its real jump button");
        Click(jump);
        Require(taskNavigationRequests == 1, "A simulated pointer click requests task navigation exactly once");
        home.Close(); yield return null;
        Open(); yield return new WaitForSeconds(2);
        panel = HudField<OfflineHomePlayerPanel>("nativePlayerPanel");
        Require(home.Profile.Save.Player.Level == before + 1 && panel.Experience.Level == before + 1, "Actual home reopen restores saved level and original XP ring");
        Require(taskNavigationRequests == 1, "Reopening home does not repeat task navigation");
        yield return CaptureFrame("home-native-player-reopened.png");
    }

    IEnumerator CheckLevelNotification()
    {
        Zjcs.Official.HomeLevelUpUI.LevelUpNotifyTip tip = null;
        float deadline = Time.time + 5;
        while (!tip && Time.time < deadline)
        {
            tip = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeLevelUpUI.LevelUpNotifyTip>();
            if (!tip) yield return null;
        }
        Require(tip, "Original reward handler opens the real level-up notification after the XP ring");
        var popup = tip.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
        Require(popup.mShowClip && Mathf.Abs(popup.mShowClip.length - 2.2f) < .001f,
            "Complete original 2.2 second notification clip is bound");
        yield return CaptureFrame("home-native-player-notify-enter.png");
        yield return new WaitForSeconds(.45f);
        yield return CaptureFrame("home-native-player-notify-banner.png");
        yield return new WaitForSeconds(.45f);
        Require(tip && popup.State == Zjcs.Official.WorldUI.WorldPopupWindow.EState.Showing,
            "Original notification remains visible during its show animation");
        var rows = tip.GetComponentsInChildren<Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem>(true);
        Require(rows.Length == 4 && rows.All(row => row.gameObject.activeInHierarchy && row.GetComponent<CanvasGroup>().alpha > .99f),
            "All four original property rows become visible after their original staggered fade");
        string[][] expected = { new[] { "基础攻击", "96", "106" }, new[] { "基础防御", "96", "106" },
            new[] { "基础生命", "481", "534" }, new[] { "基础速度", "76", "85" } };
        for (int index = 0; index < rows.Length; index++)
        {
            var texts = rows[index].GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
            Require(expected[index].All(value => texts.Contains(value)), "Original level 3 to 4 property comparison: " + string.Join(" -> ", texts));
        }
        var banner = tip.transform.Find("Content/TopGo/Banner").GetComponent<Image>();
        Require(banner.sprite && banner.color.a > .99f && banner.transform.localScale.x > .99f && !banner.canvasRenderer.cull,
            "Original banner reaches full size and opacity");
        var systems = tip.GetComponentsInChildren<ParticleSystem>();
        Require(systems.Length > 0 && systems.Any(system => system.particleCount > 0),
            "Original notification particle systems run in the real home canvas");
        Require(tip.transform.Find("Content/TopGo/LevelGo/TitleText").GetComponent<CanvasGroup>().alpha > .99f,
            "Original level-up title is fully visible");
        yield return CaptureFrame("home-native-player-level-notify.png");
        yield return new WaitForSeconds(.65f);
        Require(tip, "Original notification is retained through the property display interval");
        yield return CaptureFrame("home-native-player-notify-hold.png");
        yield return new WaitForSeconds(.9f);
        Require(!tip && HudField<Zjcs.Offline.HomeLevelUpUI.OfflineHomeLevelUpWindows>("nativeLevelUpWindows").WindowCount == 0,
            "Original show completion closes and releases the duplicate notification");
    }

    void Open()
    {
        home = Zjcs.Presentation.Home.HomeFeatureRuntime.Begin(slot, root, null, onOpenMainland: () => taskNavigationRequests++);
        home.TutorialAutoStartEnabled = false;
        if (!UnityEngine.Object.FindObjectOfType<AudioListener>())
            home.Scene.Camera.gameObject.AddComponent<AudioListener>();
    }
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var camera = button.GetComponentInParent<Canvas>().rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : home.Scene.Camera;
        var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Simulated pointer targets the top UI hit: " + string.Join(",", hits.Select(value => value.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    IEnumerator CaptureFrame(string name)
    {
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        Require(texture && texture.width == 608 && texture.height == 1080, "Actual Game View retains the portrait validation size");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
        UnityEngine.Object.Destroy(texture);
    }
    static void Require(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
}
#endif
