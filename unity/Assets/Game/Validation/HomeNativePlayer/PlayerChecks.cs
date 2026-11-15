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
    long collectedExperience;
    Zjcs.Official.HomeHangUpWindowsUI.FurnitureBedDialog collectedBed;
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
        Require(award > 0, "Bed collection can advance the real saved player to the next level");
        yield return CollectBedExperience(award, tables.LevelExpParser.GetExp(1, before + 2));
        yield return CaptureFrame("home-native-player-xp-animation.png");
        yield return CheckLevelNotification();
        Require(collectedBed && collectedBed.isActiveAndEnabled, "Original regular collection retains the bed through the level-up notification");
        ClickOutside(collectedBed.gameObject);
        float closeDeadline = Time.time + 5;
        while (collectedBed && Time.time < closeDeadline) yield return null;
        Require(!collectedBed, "Original outside-mask pointer completes the bed hide animation and releases its window");
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
        Require(home.Profile.Save.Player.Experience == collectedExperience && home.Session.Save.Inventory.Count == 0,
            "Reopening restores the bed XP exactly once and retains no owed reward");
        Require(taskNavigationRequests == 1, "Reopening home does not repeat task navigation");
        yield return CaptureFrame("home-native-player-reopened.png");
    }

    IEnumerator CollectBedExperience(long award, long followingLevelExperience)
    {
        Require(!home.Session.OwnsFurniture(0), "Isolated new character starts with the original unrepaired bed");
        SceneClick(0);
        yield return new WaitForSeconds(.8f);
        var repair = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeUI.FurnitureRepairDialog>();
        Require(repair, "Scene pointer opens the original bed repair dialog");
        Click(repair.RepairBtn);
        yield return new WaitForSeconds(.8f);
        var repaired = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeUI.FurnitureRepairedNotifyDialog>();
        Require(home.Session.OwnsFurniture(0) && repaired, "Original repair button commits bed ownership and opens the repaired notification");
        yield return WaitForPopupShow(repaired.gameObject);
        yield return CaptureFrame("home-native-player-bed-repaired.png");
        Click(repaired.GetComponentsInChildren<Zjcs.Official.ProfessionUI.CloseWindowBtn>()
            .First(value => value.myButton.isActiveAndEnabled).myButton);
        yield return new WaitForSeconds(.6f);
        SceneClick(0);
        yield return new WaitForSeconds(.8f);
        var bed = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeHangUpWindowsUI.FurnitureBedDialog>();
        Require(bed, "Repaired scene bed opens the complete original income dialog");
        collectedBed = bed;
        yield return WaitForPopupShow(bed.gameObject);
        // Only this isolated test save receives a prepared settled balance. Collection,
        // reward presentation, progression and persistence use the production path.
        Require(home.Session.Mutate("native-player-earned-bed-xp", save => {
            var income = save.Income.Single(value => value.HangUpId == 1);
            income.StoredSeconds = 0; income.SettledCount = award;
            var now = DateTimeOffset.UtcNow;
            save.LastLocalUnixSeconds = now.ToUnixTimeSeconds();
            save.LastIncomeSubsecond = (now.UtcDateTime.Ticks % TimeSpan.TicksPerSecond) / (double)TimeSpan.TicksPerSecond;
        }), "Prepare already earned bed XP in the isolated home ledger");
        long before = home.Profile.Save.Player.Experience;
        Click(bed.GetHangUpRewardBtn);
        collectedExperience = home.Profile.Save.Player.Experience;
        Require(collectedExperience >= before + award && collectedExperience < followingLevelExperience,
            "Original bed collect pointer commits the real level 3 to 4 experience reward");
        Require(home.Session.Save.Inventory.Count == 0 && home.Session.Save.Income.Single(value => value.HangUpId == 1).SettledCount == 0,
            "Bed collection acknowledges the durable reward and consumes its settled balance");
        home.DrainHomeInventory();
        Require(home.Profile.Save.Player.Experience == collectedExperience, "Repeating pending reward recovery cannot grant the bed XP twice");
        var awards = UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeAwardUI.AwardListPanel>();
        Require(awards && awards.GetComponent<Canvas>().overrideSorting
            && awards.GetComponent<Canvas>().sortingOrder > bed.GetComponent<Canvas>().sortingOrder,
            "Original bed collection opens its real reward queue above the bed");
        yield return new WaitForSeconds(.4f);
        Require(UnityEngine.Object.FindObjectOfType<Zjcs.Official.HomeAwardUI.AwardListPanelItem>(),
            "Original reward queue instantiates the bed's actual award item");
        yield return CaptureFrame("home-native-player-bed-collected.png");
    }

    void ClickOutside(GameObject root)
    {
        var popup = root.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
        var mask = UnityEngine.Object.FindObjectsOfType<Zjcs.Official.WorldUI.WorldWindowMask>()
            .Single(value => value.AttachWindow == popup);
        var pointer = new PointerEventData(EventSystem.current) {
            position = new Vector2(8, Screen.height - 8), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == mask.Button.gameObject,
            "Outside pointer first hits this popup's original attached mask");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    IEnumerator WaitForPopupShow(GameObject root)
    {
        var popup = root.GetComponent<Zjcs.Official.WorldUI.WorldPopupWindow>();
        float deadline = Time.time + 5;
        while (popup && popup.State != Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show && Time.time < deadline)
            yield return null;
        Require(popup && popup.State == Zjcs.Official.WorldUI.WorldPopupWindow.EState.Show,
            "Original popup completes its show animation before pointer input");
    }

    void SceneClick(int type)
    {
        Require(home.Scene.TryGetFurnitureClickBounds(type, out Rect bounds), "Furniture has its original scene click bounds");
        Vector2 point = home.Scene.Camera.WorldToScreenPoint(bounds.center);
        var pointer = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count == 0, "Furniture scene pointer is not obscured by UI: " + string.Join(",", hits.Select(value => value.gameObject.name)));
        var input = typeof(Zjcs.Presentation.Home.HomeFeatureRuntime).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home);
        foreach (string method in new[] { "PointerDown", "PointerUp" })
            input.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { point, false });
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
        var notifyCanvas = tip.GetComponent<Canvas>();
        var bedCanvas = collectedBed.GetComponent<Canvas>();
        Require(notifyCanvas.sortingOrder > bedCanvas.sortingOrder,
            "Original level-up notification renders above the still-open original bed");
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
