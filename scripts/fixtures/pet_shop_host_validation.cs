using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeUI;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PetShopHostValidation
{
    private const string Active = "Zjcs.PetShopHost.Active";
    static PetShopHostValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeFurnitureHostFixture").AddComponent<PetShopHostRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Home facility host validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        Zjcs.Editor.ValidationGameView.Portrait(608, 1080);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("PET_SHOP_HOST_PASSED: session, actual shop tab, navigation, logout and disk reopen.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PetShopHostRunner : MonoBehaviour
{
    private HomeFeatureRuntime home;
    private RenderTexture target;
    private string root, slot;
    private int errors;
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; };
        var checks = Environment.GetEnvironmentVariable("ZJCS_PET_SHOP_REOPEN") == "1" ? CheckColdReopen() : Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { home?.Close(); PetShopHostValidation.Finish(error); yield break; }
            yield return current;
        }
        home.Close();
        CloseCapture();
        PetShopHostValidation.Finish(errors == 0 ? null : new Exception("Unexpected home host errors: " + errors));
    }
    private IEnumerator Check()
    {
        string selectionOutput = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "pet-shop-selection-" + Guid.NewGuid().ToString("N"));
        typeof(SaveSelectionProbe).GetField("output", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, selectionOutput);
        root = SaveSelectionProbe.ProfileRoot;
        var creation = CharacterCreationOfflineService.OpenDefault(root);
        slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "结缘测试";
        Require(creation.TryCreateCharacter(slot, draft, out string error), error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
        var slots = new ProfileSlotCatalog(root);
        var writer = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(writer.ApplyCharacterMutationIfChanged(player => { player.SubRank = 5; player.Level = 49; return true; }).IsApplied, "seed locked level with the official level cap");
        var selectionHost = new GameObject("PetShopSelectionHost").AddComponent<CharacterCreationFlowController>();
        var select = (Action)typeof(CharacterCreationFlowController).GetMethod("OpenSlot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selectionHost, new object[] { slot });
        select();
        Require(HomePetShopSession.Current != null && HomePetShopSession.Current.IsLimited, "real character selection starts the locked session");
        var owner = HomePetShopSession.Current;
        var selectionCanvas = (Canvas)typeof(CharacterCreationFlowController).GetField("canvas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(selectionHost);
        Object.Destroy(selectionCanvas.gameObject);
        Object.Destroy(selectionHost.gameObject);
        yield return null;
        Open();
        yield return new WaitForSeconds(1);
        Require(HasNode(home.Catalog.Panel("pet_exchange_shell").root,
            "/PetGachaPanel/BgPanel1/Content/BottomGo/TabBottom /TabGroup/ShopPanelTabItem"),
            "nested official pet panel retains its shop tab");
        Require(HasNode(home.Catalog.Panel("treasure_gacha_main").root,
            "/ChestGachaPanel/BgPanel1"), "flattened treasure panel retains its children");
        Require(ReferenceEquals(owner, HomePetShopSession.Current) && owner.IsLimited && !owner.State.IsNew(18, 7), "the real home preserves the selected player session");
        home.CloseForNavigation(); home = null; CloseCapture();
        yield return null;
        writer.Reload();
        Require(writer.ApplyCharacterMutationIfChanged(player => { player.Level = 50; return true; }).IsApplied, "level persisted away from home");
        var repairWriter = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(new OfflineFruitTreeService(repairWriter).TryRepair(OfflineFruitTreeService.PetFurnitureType, 50, false, 1700000000, "pet-shop-host:repair").IsApplied, "real repair while home is closed");
        Require(owner.State.IsNew(18, 7), "unlock marks the persistent player session");
        Open();
        yield return new WaitForSeconds(1);
        Require(ReferenceEquals(owner, HomePetShopSession.Current) && owner.State.IsNew(18, 7), "mark survives navigation");
        home.Hud.ShowPetGacha();
        yield return new WaitForSeconds(1);
        Capture("pet-shop-gacha-before.png");
        Require(owner.State.IsNew(18, 7), "opening default gacha tab retains mark");
        var shopTab = home.Hud.CanvasTransform.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ShopPanelTabItem");
        Click(shopTab.gameObject);
        yield return new WaitForSeconds(1);
        Require(!owner.State.IsNew(18, 7), "actual selected shop tab clears mark");
        Capture("pet-shop-selected.png");
        home.CloseForNavigation(); home = null; CloseCapture();
        yield return null;
        OfflineMainlineProgressSession.EndPlayerSession();
        Require(HomePetShopSession.Current == null, "logout disposes the session");
        Open();
        yield return new WaitForSeconds(1);
        Require(!HomePetShopSession.Current.IsLimited && !HomePetShopSession.Current.State.IsNew(18, 7), "cold reopen restores repair without a new mark");
        Capture("pet-shop-reopened.png");
        File.WriteAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "pet-shop-reopen.txt"), new[] { root, slot });
    }
    private IEnumerator CheckColdReopen()
    {
        string[] saved = File.ReadAllLines(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "pet-shop-reopen.txt"));
        Require(saved.Length == 2, "isolated profile selection is present");
        root = saved[0]; slot = saved[1];
        Require(Path.GetFullPath(root).StartsWith(Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")) + Path.DirectorySeparatorChar), "cold reopen uses isolated profile");
        Open();
        yield return new WaitForSeconds(1);
        Require(!HomePetShopSession.Current.IsLimited && !HomePetShopSession.Current.State.IsNew(18, 7), "new Unity process restores unlock without a new mark");
        home.Hud.ShowPetGacha();
        yield return new WaitForSeconds(1);
        var shopTab = home.Hud.CanvasTransform.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ShopPanelTabItem");
        Click(shopTab.gameObject);
        yield return new WaitForSeconds(1);
        Capture("pet-shop-cold-reopen.png");
    }
    private void Open()
    {
        home = HomeFeatureRuntime.Begin(slot, root, null);
        home.TutorialAutoStartEnabled = false;
        target = new RenderTexture(608, 1080, 24);
        home.Scene.Camera.targetTexture = target;
        var canvas = home.Hud.CanvasTransform.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = home.Scene.Camera;
        canvas.planeDistance = 1;
    }
    private void CloseCapture()
    {
        if (target != null) { target.Release(); Object.Destroy(target); }
    }
    private void SceneClick(int type)
    {
        Require(home.Scene.TryGetFurnitureClickBounds(type, out Rect bounds), "Original scene collider bounds");
        Vector2 point = home.Scene.Camera.WorldToScreenPoint(bounds.center);
        var pointer = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count == 0, "Scene point is not covered by UI: " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        var input = typeof(HomeFeatureRuntime).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(home);
        foreach (string method in new[] { "PointerDown", "PointerUp" })
            input.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { point, false });
    }
    private void Click(GameObject button)
    {
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(home.Scene.Camera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button || hits[0].gameObject.transform.IsChildOf(button.transform)),
            "Selected tab is top UI hit at " + point + " screen=" + Screen.width + "x" + Screen.height + " hits=" +
            string.Join(",", hits.Select(hit => hit.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        // Preserve the production screen-overlay ordering in a camera capture.
        // World layers otherwise sort above a default camera-space canvas.
        // Original progress windows use a sibling canvas. Include that owned
        // camera's canvases when emulating screen-overlay sorting offscreen.
        foreach (Canvas item in home.Hud.CanvasTransform.GetComponentsInChildren<Canvas>(true)
            .Concat(Object.FindObjectsOfType<Canvas>(true).Where(value => value.worldCamera == home.Scene.Camera)).Distinct())
        {
            item.sortingLayerID = SortingLayer.layers.Last().id;
            if (item.sortingOrder < 30000) item.sortingOrder += 30000;
        }
        var canvas = home.Hud.CanvasTransform.GetComponent<Canvas>();
        canvas.enabled = false;
        Canvas.ForceUpdateCanvases();
        home.Scene.Camera.Render();
        Color sky = SkyPixel();
        canvas.enabled = true;
        Canvas.ForceUpdateCanvases();
        home.Scene.Camera.Render();
        Color retained = SkyPixel();
        Save(name);
        // Native modal masks deliberately tint/blur the whole frame.
        if (!home.Hud.AnyModalOpen)
            Require(Mathf.Abs(sky.r - retained.r) + Mathf.Abs(sky.g - retained.g) + Mathf.Abs(sky.b - retained.b) < 0.02f,
                "HUD capture preserves uncovered world pixels: " + sky + " -> " + retained);
    }
    private Color SkyPixel()
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
        // Stay below the native currency bar and above the reward queue. The
        // bar grows leftward when its count or quick-buy button changes.
        pixel.ReadPixels(new Rect(target.width / 2, target.height - 200, 1, 1), 0, 0); pixel.Apply();
        Color result = pixel.GetPixel(0, 0); Object.Destroy(pixel);
        RenderTexture.active = previous; return result;
    }
    private void Save(string name)
    {
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
        Object.Destroy(texture);
    }
    private static void Require(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    private static bool HasNode(HomeUiNode node, string path)
        => node != null && (node.path == path || (node.children ?? Array.Empty<HomeUiNode>()).Any(child => HasNode(child, path)));
}
