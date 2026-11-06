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
using Zjcs.Offline.Progression;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerAllDetailActorRenderValidation
{
    const string Key = "PartnerAllDetailActorRenderValidation.Active";
    static PartnerAllDetailActorRenderValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original partner detail actor validation").AddComponent<PartnerAllDetailActorRenderRunner>();
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
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 1200);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_ALL_DETAIL_ACTOR_RENDER_OK");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerAllDetailActorRenderRunner : MonoBehaviour
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
            catch (Exception error) { PartnerAllDetailActorRenderValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerAllDetailActorRenderValidation.Finish(errors == 0 ? null : new Exception("Unexpected original actor errors: " + errors));
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
            string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
            var proof = JObject.Parse(File.ReadAllText(Path.Combine(repo, "assets/reference/partner-detail-actor-native-source-manifest.json")))["actor_data"];
            Require(proof["model_templates"].Count() == 91 && !proof["unresolved_models"].Any(), "Complete selected open partner inventory");
            var configuration = new OfflinePartnerDetailConfiguration(OfficialPartnerAssetCatalog.LoadDefault(),
                OfficialPartnerGiftAssetCatalog.LoadDefault(), File.ReadAllText(Path.Combine(repo,
                    "unity/Assets/Official/Partners/UI/DetailPresentation/partner-detail-region-images.json")));
            foreach (var row in proof["model_templates"])
            {
                int id = (int)row["npcId"];
                if (!Selected(id, "model")) continue;
                Debug.Log("PARTNER_ALL_MODEL_BEGIN " + id);
                string path = ((string)(row["requested_path"] ?? row["path"])).Replace("Assets/Prefabs/", "").Replace(".prefab", "");
                var model = InstantiateInactive(assets.LoadPrefab(path), canvas.transform);
                textures.BindTree(model); model.SetActive(true);
                yield return null; yield return new WaitForSeconds(.15f);
                var proxy = model.GetComponent<SkeletonGraphicProxy>();
                Require(proxy, "Original model proxy " + id);
                if (proxy.SkeletonGraphic)
                {
                    var native = proxy.SkeletonGraphic.GetComponent<PartnerNativeUiActor>();
                    Require(native && native.Graphic.State != null, "Original model initialized " + id);
                    string requested = OfflinePartnerDetailConfiguration.GetNpcStartingAnimName(id);
                    string previous = native.AnimationState.GetCurrent(0)?.Animation.Name;
                    bool found = native.GetOrLoadAnimation(requested) != null;
                    native.PlayAnimation(requested, true);
                    yield return new WaitForSeconds(.15f);
                    Require(native.AnimationState.GetCurrent(0)?.Animation.Name == (found ? requested : previous),
                        "Original named animation or unchanged track on missing request " + id);
                    Require(MeshVertices(model) > 0, "Actual original model meshes " + id);
                    CheckSeparatorParents(native.Graphic);
                }
                else Require((bool?)row["staticImage"] == true && model.GetComponentsInChildren<Image>().Any(image => image.sprite),
                    "Original static model sprite " + id);
                Capture("partner-all-model-" + id + ".png");
                model.SetActive(false); Destroy(model); yield return null; manager.Update();
                Require(textures.LoadedResourceTextureCount == 0, "Model releases High textures " + id);
                Debug.Log("PARTNER_ALL_MODEL_OK " + id);
                if (id % 8 == 0) yield return Resources.UnloadUnusedAssets();
            }
            var liveServices = new OfflinePartnerDetailLiveServices(assets, configuration.GetNpcInfo,
                id => configuration.TryGetFullBodyImagePath(id, PartnerFullBodyImageType.Live, out var path) ? path : null);
            foreach (var row in proof["model_templates"])
            {
                int id = (int)row["npcId"];
                if (!Selected(id, "live")) continue;
                if (configuration.GetFullBodyImagePath(id, out _) != PartnerFullBodyImageType.Live) continue;
                Debug.Log("PARTNER_ALL_LIVE_BEGIN " + id);
                var prefab = assets.LoadPrefab("Appearance/NpcLive/NpcDetailLiveItem_" + id);
                if (!prefab) prefab = assets.LoadPrefab("Appearance/NpcLive/NpcDetailLiveItem");
                var live = InstantiateInactive(prefab, canvas.transform);
                foreach (var renderer in live.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var streams = new List<ParticleSystemVertexStream>();
                    renderer.GetActiveVertexStreams(streams);
                    Require(streams.All(stream => Enum.IsDefined(typeof(ParticleSystemVertexStream), stream)),
                        "Original packed particle vertex-stream bytes " + id + "/" + renderer.name);
                }
                liveServices.BindTree(live); textures.BindTree(live); live.SetActive(true);
                var item = live.GetComponent<NpcDetailLiveItem>(); item.ShowNpc(id);
                yield return null; yield return new WaitForSeconds(.2f);
                var component = (Component)typeof(NpcDetailLiveItem).GetField("mNpcLiveGraphic", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
                var native = component.GetComponent<PartnerNativeUiActor>();
                Require(native.AnimationState.GetCurrent(0).Animation.Name == "idle_01", "Original live idle " + id);
                Require(MeshVertices(live) > 0, "Actual original live meshes " + id);
                CheckSeparatorParents(native.Graphic);
                Capture("partner-all-live-" + id + ".png");
                var button = (Button)typeof(NpcDetailLiveItem).GetField("mNpcLiveClickBtn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(item);
                Click(button);
                Require(native.AnimationState.GetCurrent(0).Animation.Name == "click_01", "Simulated original live click " + id);
                yield return new WaitForSeconds(.15f);
                Capture("partner-all-live-click-" + id + ".png");
                live.SetActive(false); Destroy(live); yield return null; manager.Update();
                Require(textures.LoadedResourceTextureCount == 0, "Live item releases textures " + id);
                Debug.Log("PARTNER_ALL_LIVE_OK " + id);
                if (id % 8 == 0) yield return Resources.UnloadUnusedAssets();
            }
        }
        finally { ProfessionAppearanceTextureBindings.Current = previousTextures; }
    }
    static int MeshVertices(GameObject root)
    {
        Canvas.ForceUpdateCanvases();
        int vertices = 0;
        foreach (var renderer in root.GetComponentsInChildren<CanvasRenderer>())
        { var mesh = renderer.GetMesh(); if (mesh) vertices += mesh.vertexCount; }
        return vertices;
    }
    static bool Selected(int id, string kind)
    {
        string selection = Environment.GetEnvironmentVariable("ZJCS_PARTNER_ACTOR_SELECTION");
        return string.IsNullOrEmpty(selection) || selection.Split(',').Contains(kind + ":*") || selection.Split(',').Contains(kind + ":" + id);
    }
    static void CheckSeparatorParents(ProfessionNativeUiGraphic graphic)
    {
        if (!graphic.enableSeparatorSlots) return;
        Require(graphic.separatorPartInstructions.Count == graphic.separatorSlotNames.Length + 1,
            "Every original separator range is retained");
        foreach (var renderer in graphic.canvasRenderers.Where(renderer => renderer.gameObject.activeInHierarchy))
            Require(graphic.SeparatorParts.Contains(renderer.transform.parent), "Batches draw under the original separator transforms");
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
