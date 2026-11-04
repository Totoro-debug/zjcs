using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerDetailActorRenderValidation
{
    const string Key = "PartnerDetailActorRenderValidation.Active";
    static PartnerDetailActorRenderValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original partner detail actor validation").AddComponent<PartnerDetailActorRenderRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Original detail actor validation timed out"));
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
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_DETAIL_ACTOR_RENDER_OK");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerDetailActorRenderRunner : MonoBehaviour
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
            catch (Exception error) { PartnerDetailActorRenderValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerDetailActorRenderValidation.Finish(errors == 0 ? null : new Exception("Unexpected original actor errors: " + errors));
    }
    IEnumerator Check()
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while ((Screen.width != 450 || Screen.height != 800) && Time.realtimeSinceStartup < deadline) yield return null;
        Require(Screen.width == 450 && Screen.height == 800, "Original portrait viewport");
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("Original actor camera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.clearFlags = CameraClearFlags.SolidColor;
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.backgroundColor = new Color(.15f, .18f, .2f);
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("Original actor viewport", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("Simulated original actor pointer", typeof(EventSystem)).GetComponent<EventSystem>();
        var assets = Resources.Load<PartnerNativeDetailActorAssets>("Partners/PartnerNativeDetailActorAssets");
        Require(assets, "Packaged original actor catalog");
        // This fixture exercises original loader ownership, not manager-created materials.
        var managerConfig = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
        using var manager = new AppearanceAssetManager(managerConfig,
            action => { Application.LowMemoryCallback callback = () => action(); Application.lowMemory += callback;
                return new Subscription(() => Application.lowMemory -= callback); });
        var textures = new OfflinePartnerUiTextureResources(assets, manager);
        var previousTextures = ProfessionAppearanceTextureBindings.Current; ProfessionAppearanceTextureBindings.Current = textures;
        try
        {
            var model = InstantiateInactive(assets.LoadPrefab("Appearance/Npc/8402/NpcModel_UI_8402"), canvas.transform);
            textures.BindTree(model); model.SetActive(true);
            yield return null; yield return new WaitForSeconds(.5f);
            var native = model.GetComponentInChildren<PartnerNativeUiActor>();
            var combine = model.GetComponentInChildren<Zjcs.Official.PartnersUI.AppearanceCombineSkin>();
            Require(combine.Init && combine.CombineSkins.SequenceEqual(new[] { "body_8402" }), "Original model application combined skin");
            Require(native.Graphic.State.Skin is ProfessionNativeSkin skin && skin.AttachmentCount > 0, "Actual original body attachments");
            Require(textures.LoadedResourceTextureCount > 0 && native.Graphic.CustomTextureOverride.Count > 0, "Original High loader requests real textures");
            Require(model.GetComponentsInChildren<ProfessionNativeUiBatchGraphic>().Length == native.Graphic.canvasRenderers.Count,
                "Original serialized submesh reused before additional batches");
            native.PlayAnimation("npc_8402/idle_03", true);
            yield return new WaitForSeconds(.4f);
            Capture("partner-detail-model-8402.png");
            model.SetActive(false); manager.Update();
            Require(textures.LoadedResourceTextureCount == 0 && native.Graphic.CustomTextureOverride.Count == 0, "Original model disable returns all texture references");
            model.SetActive(true); yield return new WaitForSeconds(.3f);
            Require(textures.LoadedResourceTextureCount > 0, "Original model reopens and reloads High textures");
            model.SetActive(false); Destroy(model); yield return null;
            Require(textures.LoadedResourceTextureCount == 0, "Destroyed model leaves no texture requests");

            string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var npc = JObject.Parse(File.ReadAllText(Path.Combine(repo, "unity/Assets/Official/Partners/partner-rules.json")))["tables"]["npc"]
                .Single(row => (int)row["Id"] == 8402);
            Vector3 Vector(string field) => new Vector3((float)npc[field]["X"], (float)npc[field]["Y"], (float)npc[field]["Z"]);
            var info = new PartnerDetailPresentationInfo { Id = 8402, LiveOffset = Vector("LiveOffset"), LiveScale = Vector("LiveScale") };
            var liveServices = new OfflinePartnerDetailLiveServices(assets, id => id == 8402 ? info : null,
                id => id == 8402 ? (string)npc["FullBodyLiveImage"] : null);
            var live = InstantiateInactive(assets.LoadPrefab("Appearance/NpcLive/NpcDetailLiveItem"), canvas.transform);
            liveServices.BindTree(live); live.SetActive(true);
            var item = live.GetComponent<NpcDetailLiveItem>(); item.ShowNpc(8402);
            yield return new WaitForSeconds(.4f);
            var graphic = live.GetComponentInChildren<PartnerNativeUiActor>();
            Require(graphic.AnimationState.GetCurrent(0).Animation.Name == "idle_01", "Original live sequence starts after data rebuild");
            Capture("partner-detail-live-8402.png");
            var button = (Button)typeof(NpcDetailLiveItem).GetField("mNpcLiveClickBtn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
            Click(button);
            Require(graphic.AnimationState.GetCurrent(0).Animation.Name == "click_01", "Simulated pointer invokes original live click");
            yield return new WaitForSeconds(.2f); Capture("partner-detail-live-click-8402.png");
            float duration = graphic.GetOrLoadAnimation("click_01").Duration;
            yield return new WaitForSeconds(duration + .4f);
            Require(graphic.AnimationState.GetCurrent(0).Animation.Name == "idle_01", "Original click returns to previous idle");
            live.SetActive(false); yield return null; live.SetActive(true); item.ShowNpc(8402);
            yield return new WaitForSeconds(.3f);
            Require(graphic.AnimationState.GetCurrent(0).Animation.Name == "idle_01", "Original live disable and reopen refresh subscriptions");
            live.SetActive(false); Destroy(live); yield return null;
        }
        finally { ProfessionAppearanceTextureBindings.Current = previousTextures; }
    }
    static GameObject InstantiateInactive(GameObject prefab, Transform parent)
    {
        Require(prefab, "Original actor prefab");
        var slot = new GameObject("Inactive original actor binding", typeof(RectTransform)); slot.SetActive(false); slot.transform.SetParent(parent, false);
        var result = Object.Instantiate(prefab, slot.transform, false); result.SetActive(false);
        result.transform.SetParent(parent, false); Object.Destroy(slot); return result;
    }
    void Click(Button button)
    {
        var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(cameraUi, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
            "Original live button receives pointer without occlusion");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
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
    sealed class Subscription : IDisposable
    {
        Action release; public Subscription(Action value) { release = value; }
        public void Dispose() { var value = release; release = null; value?.Invoke(); }
    }
}
