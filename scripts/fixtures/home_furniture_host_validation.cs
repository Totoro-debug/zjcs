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
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeFurnitureHostValidation
{
    private const string Active = "Zjcs.HomeFurnitureHost.Active";
    static HomeFurnitureHostValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HomeFurnitureHostFixture").AddComponent<HomeFurnitureHostRunner>();
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
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 240);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_FURNITURE_HOST_PASSED: actual home entry, simulated scene and native repair input, four-state binding and saved restoration.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeFurnitureHostRunner : MonoBehaviour
{
    private HomeFeatureRuntime home;
    private RenderTexture target;
    private string root, slot;
    private int errors;
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; };
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { home?.Close(); HomeFurnitureHostValidation.Finish(error); yield break; }
            yield return current;
        }
        home.Close();
        CloseCapture();
        HomeFurnitureHostValidation.Finish(errors == 0 ? null : new Exception("Unexpected home host errors: " + errors));
    }
    private IEnumerator Check()
    {
        root = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "home-host-saves-" + Guid.NewGuid().ToString("N"));
        var creation = CharacterCreationOfflineService.OpenDefault(root);
        slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "家园测试";
        Require(creation.TryCreateCharacter(slot, draft, out string error), error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
        Require(profile.ApplyCharacterMutation(player => { player.Level = 3; player.Experience = character.CumulativeLevelExperience[3]; player.SubRank = 1; }, "host-level").IsApplied, "Seed real level 3 profile");
        Open();
        yield return new WaitForSeconds(1);
        Require(home.Rules.FurnitureVersion == "884_156692" && !home.Session.OwnsFurniture(0), "Actual home uses current furniture table");
        Require(home.HomeProgression.FurnitureState(home.Session, 0, home.Rules.CurrentContext) == HomeFurnitureState.CanRepair, "Actual home bed is repairable");
        Capture("home-current-before-repair.png");
        SceneClick(0);
        yield return new WaitForSeconds(0.8f);
        var repair = Object.FindObjectOfType<FurnitureRepairDialog>();
        Require(repair != null, "Scene pointer opens native repair dialog");
        Capture("home-current-repair-window.png");
        Click(repair.RepairBtn);
        yield return new WaitForSeconds(0.8f);
        Require(home.Session.OwnsFurniture(0) && Object.FindObjectOfType<FurnitureRepairedNotifyDialog>() != null, "Native pointer commits actual home transaction and opens notification");
        Capture("home-current-repair-notify.png");
        home.Close();
        CloseCapture();
        yield return null;
        Open();
        yield return new WaitForSeconds(1);
        Require(home.Session.OwnsFurniture(0) && home.HomeProgression.FurnitureState(home.Session, 0, home.Rules.CurrentContext) == HomeFurnitureState.Functional, "Actual home reopen restores repaired bed");
        Capture("home-current-reopened.png");
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
    private void Click(Button button)
    {
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(home.Scene.Camera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "Repair button is top UI hit");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        // Preserve the production screen-overlay ordering in a camera capture.
        // World layers otherwise sort above a default camera-space canvas.
        foreach (Canvas item in home.Hud.CanvasTransform.GetComponentsInChildren<Canvas>(true))
        {
            item.sortingLayerID = SortingLayer.layers.Last().id;
            item.sortingOrder = 30000;
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
        pixel.ReadPixels(new Rect(target.width / 2, target.height - 40, 1, 1), 0, 0); pixel.Apply();
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
}
