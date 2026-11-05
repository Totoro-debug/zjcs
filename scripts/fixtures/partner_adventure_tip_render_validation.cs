using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using Profession = Zjcs.Official.ProfessionUI.Profession;

[InitializeOnLoad]
public static class PartnerAdventureTipRenderValidation
{
    const string Key = "PartnerAdventureTipRenderValidation.Active";
    static PartnerAdventureTipRenderValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original adventure tip validation").AddComponent<PartnerAdventureTipRenderRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Adventure tip validation timed out"));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) || !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated Unity project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        typeof(PartnerPanelHostValidation).GetMethod("SetGameViewSize", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 450, 800 });
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) Debug.LogException(error); else Debug.Log("PARTNER_ADVENTURE_TIP_RENDER_OK");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerAdventureTipRenderRunner : MonoBehaviour
{
    int errors;
    Camera cameraUi;
    RenderTexture target;
    EventSystem events;
    IEnumerator Start()
    {
        Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var run = Check();
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (Exception error) { PartnerAdventureTipRenderValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerAdventureTipRenderValidation.Finish(errors == 0 ? null : new Exception("Unexpected adventure tip errors: " + errors));
    }
    IEnumerator Check()
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while ((Screen.width != 450 || Screen.height != 800) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(Screen.width == 450 && Screen.height == 800, "Original portrait viewport");
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("Original tip camera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.clearFlags = CameraClearFlags.SolidColor; cameraUi.backgroundColor = new Color(.15f, .18f, .2f);
        cameraUi.transform.position = new Vector3(0, 0, -10);
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("Original tip viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Canvas.ForceUpdateCanvases();
        events = new GameObject("Simulated original tip pointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var assets = Resources.Load<PartnerNativeDetailPresentationAssets>("Partners/PartnerNativeDetailPresentationAssets");
        Require(assets && assets.AdventureTip && assets.Settings && assets.Regions, "Packaged original presentation catalog");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.FindAssets("FZY4K SDF - dynamic t:TMP_FontAsset")
            .Select(AssetDatabase.GUIDToAssetPath).First(path => path.Contains("/Official/Partners/")));
        typeof(TMP_FontAsset).GetField("m_SourceFontFile", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(font, AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        font.fallbackFontAssetTable.Clear();
        WorldMultiLanguageText.LanguageFont = () => font;
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        var proof = JObject.Parse(File.ReadAllText(Path.Combine(repo, "assets/reference/partner-detail-presentation-native-source-manifest.json")));
        foreach (var row in proof["sprites"])
        {
            var sprite = assets.LoadSprite((string)row["address"]);
            Require(sprite && sprite.texture && sprite.vertices.Length > 0 && sprite.triangles.Length > 0,
                "Actual original presentation geometry and texture: " + row["address"]);
        }
        Require(JObject.Parse(assets.Regions.text)["rows"].Count() == 8, "All original region overrides retained");
        int closes = 0;
        var services = new OfflinePartnerAdventureTipServices(assets, cameraUi, window => { closes++; window.SetActive(false); });
        var inactive = new GameObject("Inactive tip binding", typeof(RectTransform)); inactive.SetActive(false); inactive.transform.SetParent(canvas.transform, false);
        var root = Object.Instantiate(assets.AdventureTip, inactive.transform, false); root.SetActive(false);
        root.transform.SetParent(canvas.transform, false); Destroy(inactive);
        // Original UIUtils.Instantiate(GameObject,parent,All) calls ResetUI.
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        services.BindTree(root);
        var tip = root.GetComponent<PopTipNpcAdventure>();
        var rect = (RectTransform)typeof(PopTipNpcAdventure).GetField("mTransform", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tip);
        var text = root.GetComponentInChildren<WorldMultiLanguageText>(true);
        var image = (Image)typeof(PopTipNpcAdventure).GetField("mConditionImage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tip);
        var probes = proof["adventure_icons"].ToArray();
        int probe = 0;
        foreach (var row in probes)
        {
            int category = (int)row["category"], value = (int)row["value"];
            root.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var point = new Vector2(probe % 2 == 0 ? 4 : 446, probe % 3 == 0 ? 796 : 4);
            tip.Init(point, category == 0 ? value : 0, category == 1 ? (Profession)value : Profession.None,
                category == 2 ? (NpcGiftType)value : NpcGiftType.None);
            yield return null; Canvas.ForceUpdateCanvases();
            Require(services.PointerSubscriberCount == 1 && services.ActiveIconLeases == 1, "Original enabled lifetime owns one pointer and icon");
            Require(image.sprite == assets.LoadIcon((string)row["official_asset"]["guid"]), "Original settings selects exact icon");
            Require(text.Text == assets.GetText(text.myKey) && !text.Text.StartsWith("ui_"), "Original dynamic localized text");
            Require(font.HasCharacters(text.Text, out uint[] missing, false, true), "Original font renders every localized glyph: " +
                (missing == null ? string.Empty : string.Join(",", missing)));
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(cameraUi, corner);
                Require(screen.x >= -1 && screen.x <= 451 && screen.y >= -1 && screen.y <= 801, "Original tooltip stays within the viewport");
            }
            var screenCorners = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(cameraUi, corner)).ToArray();
            Require(point.x < 225 ? screenCorners.Min(corner => corner.x) <= 6 : screenCorners.Max(corner => corner.x) >= 444,
                "Original tooltip follows the pointer at the horizontal screen edge");
            Require(point.y < 400 ? screenCorners.Min(corner => corner.y) <= 10 : screenCorners.Max(corner => corner.y) >= 790,
                "Original tooltip follows the pointer at the vertical screen edge");
            var inside = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center));
            SimulatedPointerDown(services, inside);
            Require(root.activeInHierarchy && closes == probe, "Inside pointer retains original tooltip");
            if (probes.Take(probe).All(previous => (int)previous["category"] != category)) Capture("partner-adventure-tip-" + category + ".png");
            var outside = new Vector2(inside.x > 225 ? 2 : 448, inside.y > 400 ? 2 : 798);
            Require(!PartnerAdventureUiUtils.CheckIfClickInsideUI(rect, outside, cameraUi), "Outside probe does not touch original tooltip");
            SimulatedPointerDown(services, outside);
            Require(!root.activeInHierarchy && closes == probe + 1 && services.PointerSubscriberCount == 0 && services.ActiveIconLeases == 0,
                "Outside pointer closes and releases original subscriptions and resources");
            services.DispatchPointerDown(outside);
            Require(closes == probe + 1, "Disabled tooltip has no stale global pointer subscription");
            probe++;
        }
        Destroy(root); yield return null;
        Require(services.ActiveIconLeases == 0 && services.PointerSubscriberCount == 0, "Destroyed original tip has no outstanding ownership");
        Debug.Log("Verified original adventure icon branches: " + probe + "; presentation sprites: " + proof["sprites"].Count());
    }
    void SimulatedPointerDown(OfflinePartnerAdventureTipServices services, Vector2 point)
    {
        var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
        services.DispatchPointerDown(point);
        if (hits.Count > 0) ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        var previous = RenderTexture.active; RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), pixels.EncodeToPNG());
        RenderTexture.active = previous; Destroy(pixels);
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
