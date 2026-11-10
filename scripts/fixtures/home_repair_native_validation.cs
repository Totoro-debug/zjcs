using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeRepairNativeValidation
{
    static HomeRepairNativeValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("HomeRepairNative", false))
                new GameObject("HomeRepairFixture").AddComponent<HomeRepairNativeRunner>();
        };
        EditorApplication.update += () =>
        {
            if (SessionState.GetBool("HomeRepairNative", false) && EditorApplication.timeSinceStartup > SessionState.GetFloat("HomeRepairDeadline", float.MaxValue))
                Finish(new TimeoutException("Original home repair validation timeout."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("HomeRepairNative", true);
        SessionState.SetFloat("HomeRepairDeadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void CompileOnly()
    {
        Debug.Log("HOME_REPAIR_INTEGRATION_COMPILE_PASSED: actual Home presentation assembly dependency closure; no gameplay validation.");
        EditorApplication.Exit(0);
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("HomeRepairNative", false);
        if (error == null) Debug.Log("HOME_REPAIR_NATIVE_PASSED: native bindings, simulated pointer, insufficient cost, successful response ordering, notify, mask and popup clips; isolated window scope.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeRepairNativeRunner : MonoBehaviour, IOfflineHomeRepairState, IWorldWindowBlurManager
{
    private bool hasCost;
    private int repairs, messages, errors, soundPosts;
    private EventSystem events;
    private Camera cameraUi;
    private RenderTexture target;
    private OfflineHomeRepairWindows windows;
    private GameObject active;
    private string activeKey;
    private Material blurMaterial;
    private bool showIncome;
    private double requestedHours;
    public RenderTexture GetRenderTexture() => RenderTexture.GetTemporary(target.width / 13, target.height / 13);
    public void ReleaseRenderTexture(RenderTexture texture) => RenderTexture.ReleaseTemporary(texture);
    public void TakeAShot(RenderTexture texture)
    {
        Canvas.ForceUpdateCanvases();
        cameraUi.Render();
        Zjcs.Presentation.World.OfficialDungeonPopupMask.Blur(target, texture, blurMaterial);
    }
    public bool HasRepairCost(EFurniture furniture) => hasCost;
    public void Repair(EFurniture furniture, Action onSuccess) { repairs++; onSuccess(); }
    public void SystemMessage(string key) { messages++; Require(!key.StartsWith("ui_"), "official localized shortage message"); }
    public void GetValidHangUpIdList(EFurniture furniture, List<int> result)
    { result.Clear(); if (showIncome && furniture == EFurniture.Bed) result.Add(1); }
    public ProfessionPropertyType GetEffectFixedPropType(int id)
    { Require(id == 1, "original bed hang-up identity"); return ProfessionPropertyType.HangUpExpAdd; }
    public double GetHangUpReward(int id, double hours)
    { Require(id == 1, "original bed hang-up reward request"); requestedHours = hours; return 321.9; }

    private IEnumerator Start()
    {
        var check = Check();
        while (true)
        {
            object current;
            try { if (!check.MoveNext()) break; current = check.Current; }
            catch (Exception error) { windows?.Dispose(); HomeRepairNativeValidation.Finish(error); yield break; }
            yield return current;
        }
        windows.Dispose();
        HomeRepairNativeValidation.Finish(errors == 0 ? null : new Exception("Unexpected runtime errors: " + errors));
    }
    private IEnumerator Check()
    {
        AudioListener.volume = 0;
        Application.logMessageReceived += (message, stack, type) =>
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var assets = OfficialHomeRepairAssetCatalog.LoadDefault();
        var images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        images.Entries = Array.Empty<OfflineProfessionImageCatalog.Entry>();
        var localization = new OfflinePartnerLocalizationService(assets.texts);
        ProfessionPropertyBindings.Current = new OfflineProfessionPropertyBindings(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/Partners/partner-property-ui.json"),
            new OfflineProfessionImageResourceLoader(images), localization,
            (owner, path, name, description, control) => { throw new InvalidOperationException("No property details on original home notification row"); },
            (owner, rank, description, effect, noEffect, noSeason) => { throw new InvalidOperationException("No blessing on original home notification row"); });
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
        Require(pipeline != null, "current project render pipeline");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        Require(assets.icons.Length == 13, "all selected original furniture icon prefabs");
        foreach (var icon in assets.icons)
            Require(assets.IconPrefab(icon.furniture) != null, "native icon binding " + icon.furniture);
        var source = Resources.Load<Font>("HomeValidationFont");
        var font = TMP_FontAsset.CreateFontAsset(source);
        Require(font != null, "original Chinese source font");
        cameraUi = new GameObject("HomeRepairCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10);
        cameraUi.clearFlags = CameraClearFlags.SolidColor;
        cameraUi.backgroundColor = new Color(0.13f, 0.16f, 0.18f);
        target = new RenderTexture(608, 1080, 24);
        cameraUi.targetTexture = target;
        var canvas = new GameObject("NativeHomeRepairCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cameraUi;
        canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("InternalSimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var masks = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
        blurMaterial = new Material(masks.BlurMaterial);
        windows = new OfflineHomeRepairWindows(canvas, font,
            masks.BlurMaskPrefab, this, this,
            (clip, owner) => { Require(clip != null && owner != null, "original popup audio binding"); soundPosts++; }, this, assets);
        windows.ActiveWindowChanged += (root, key) => { active = root; activeKey = key; };
        windows.ShowRepair(EFurniture.Bed, false);
        yield return new WaitForSeconds(0.6f);
        var repair = windows.RepairDialog;
        Require(repair != null && activeKey == "repair", "native repair root");
        Require(repair.Name.Text == assets.Text("item_unrepair_Bed"), "original unrepair name");
        Require(repair.Desc.Text == assets.Text("item_unrepair_desc_Bed"), "original unrepair description");
        Require(repair.SubTitleName.Text == assets.Text("ui_furniture_text"), "original HomeSettings classification");
        Require(repair.RepairBtnTxt.Text == assets.Text("ui_fix_text"), "original repair button language");
        Require(repair.ImageSlot.GetComponentInChildren<FurnitureIconWidget>() != null, "instantiated native icon");
        var popup = active.GetComponent<WorldPopupWindow>();
        Require(popup.mShowClip != null && popup.mHideClip != null && popup.State == WorldPopupWindow.EState.Show, "native popup animation completes");
        Capture("repair-bed.png");
        Click(repair.RepairBtn);
        Require(messages == 1 && repairs == 0 && activeKey == "repair", "insufficient cost preserves repair window");
        hasCost = true;
        Click(repair.RepairBtn);
        Require(repairs == 1 && activeKey == "repair_notify", "success opens original notify before closing repair");
        yield return new WaitForSeconds(active.GetComponent<WorldPopupWindow>().mShowClip.length + 0.2f);
        Require(windows.RepairDialog == null, "old repair root closed independently");
        var notify = active.GetComponent<FurnitureRepairedNotifyDialog>();
        Require(notify != null && notify.NameTxt.Text == assets.Text("item_4000_name"), "original repaired item name");
        Require(active.GetComponentsInChildren<ProfessionPlayerPropItem>().Length == 0, "no invented unlocked income rows");
        Capture("repair-notify-bed.png");
        var close = active.GetComponentsInChildren<CloseWindowBtn>().Single();
        float hideDuration = active.GetComponent<WorldPopupWindow>().mHideClip.length;
        Click(close.myButton);
        yield return new WaitForSeconds(hideDuration + 0.2f);
        Require(active == null, "original close button and hide animation");
        windows.ShowRepair(EFurniture.Cart, true);
        yield return new WaitForSeconds(0.6f);
        Require(windows.RepairDialog.RepairBtnTxt.Text == assets.Text("ui_build_text"), "original build variant");
        Require(windows.RepairDialog.SubTitleName.Text == assets.Text("ui_resources_text"), "original cart classification");
        Capture("repair-cart-build.png");
        ClickMask();
        yield return new WaitForSeconds(0.5f);
        Require(active == null, "original blur mask simulated pointer closes popup");
        windows.ShowRepair(EFurniture.Airship, false);
        yield return new WaitForSeconds(0.6f);
        var graphic = active.GetComponentInChildren<Spine.Unity.SkeletonGraphic>();
        Require(graphic != null && graphic.Skeleton != null && graphic.GetLastMesh() != null && graphic.GetLastMesh().vertexCount > 0,
            "original animated airship icon skeleton and mesh");
        Require(windows.RepairDialog.SubTitleName.Text == assets.Text("ui_visitor_text"), "original visitor classification");
        Capture("repair-airship.png");
        Require(soundPosts >= 4, "original popup and button sound events remain active while muted");
        windows.Dispose();
        yield return null;
        showIncome = true;
        windows.ShowRepairedNotify(EFurniture.Bed);
        yield return new WaitForSeconds(active.GetComponent<WorldPopupWindow>().mShowClip.length + 0.2f);
        var income = active.GetComponentsInChildren<ProfessionPlayerPropItem>().Single();
        Require(income.myPropValueText.Text == "321" && requestedHours == 1.0, "original one-hour reward and integer truncation");
        Require(income.myPropTypeText.Text == assets.Text("PropType.HangUpExpAdd") && !income.myPropTypeText.Text.StartsWith("PropType."),
            "original income property language");
        Capture("repair-notify-bed-income.png");
        windows.Dispose();
        yield return null;
    }
    private void ClickMask()
    {
        var pointer = new PointerEventData(events) { position = new Vector2(8, 8), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<WorldWindowBlurMask>() != null,
            "original blur mask receives outside pointer");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Click(Button button)
    {
        var point = RectTransformUtility.WorldToScreenPoint(cameraUi, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "native button is top raycast hit " + button.name + " actual=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        cameraUi.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), texture.EncodeToPNG());
        Object.Destroy(texture);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
