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
    T HudField<T>(string name) => (T)typeof(Zjcs.Presentation.Home.HomeHudView)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home.Hud);
    IEnumerator CheckFormalPlayer()
    {
        var panel = HudField<OfflineHomePlayerPanel>("nativePlayerPanel");
        Require(panel != null && panel.Root.activeInHierarchy, "Formal entry mounts the complete original player panel");
        Require(panel.Root.GetComponentsInChildren<Transform>(true).Length == 22, "All original player-panel objects retained");
        Require(panel.Experience.Level == home.Profile.Save.Player.Level, "Original avatar XP ring has the saved player level");
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
        yield return new WaitForSeconds(1.3f);
        Require(panel.Experience.Level == before + 1, "Original XP animation reaches committed level");
        Require(UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeLevelUpUI.LevelUpNotifyTip>() != null,
            "Original reward handler opens the real level-up notification");
        yield return CaptureFrame("home-native-player-level-notify.png");
        yield return new WaitForSeconds(3);
        home.Close(); yield return null;
        Open(); yield return new WaitForSeconds(2);
        panel = HudField<OfflineHomePlayerPanel>("nativePlayerPanel");
        Require(home.Profile.Save.Player.Level == before + 1 && panel.Experience.Level == before + 1, "Actual home reopen restores saved level and original XP ring");
        yield return CaptureFrame("home-native-player-reopened.png");
    }

    void Open()
    {
        home = Zjcs.Presentation.Home.HomeFeatureRuntime.Begin(slot, root, null);
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
            "Original avatar is the top UI hit: " + string.Join(",", hits.Select(value => value.gameObject.name)));
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
