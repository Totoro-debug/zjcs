using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.PartnersUI;

[InitializeOnLoad]
public static class PartnerPanelNativeGraphicValidation
{
    private const string Active = "PartnerPanelNativeGraphic";
    static PartnerPanelNativeGraphicValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("OriginalNativeCanvasValidation").AddComponent<PartnerPanelNativeGraphicRunner>();
        };
    }
    public static void Run()
    {
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "native-graphic-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() ?? "" }, true));
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_NATIVE_CANVAS_PASSED: complete original prefab imports; independent Canvas draws actual source portraits, updates native poses/material events, masks and releases. Full window interaction and player avatar animation are not covered.");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] private sealed class Result { public bool success; public string error; }
}

public sealed class PartnerPanelNativeGraphicRunner : MonoBehaviour
{
    private sealed class Animation : IProfessionAppearanceAnimation { public string Name { get; set; } }
    private sealed class Track : IProfessionAppearanceTrack { public IProfessionAppearanceAnimation Animation { get; set; } }
    // Explicit unmixed pose producer for drawing verification only. Production
    // transitions must be supplied by the original application animation graph.
    private sealed class PoseSource : IProfessionNativeUiPoseSource, IProfessionAppearanceAnimationState
    {
        private readonly OfficialPartnerPortraitAsset resource;
        private readonly Material material;
        private ProfessionNativeAnimationChannels animation;
        private bool loop;
        internal float Position;
        private readonly Track track = new Track();
        public ScriptableObject Asset => resource;
        public IProfessionAppearanceAnimationState AnimationState => this;
        internal PoseSource(OfficialPartnerPortraitAsset resource, Material material) { this.resource = resource; this.material = material; }
        public ProfessionNativeAppearanceSkeleton CreateSkeleton(string initialSkin)
        {
            var state = new ProfessionNativeAppearanceSkeleton(resource.CreateSkeleton());
            state.SetSkin(state.Data.FindSkin(initialSkin)); state.SetToSetupPose(); state.UpdateCache(); return state;
        }
        public IProfessionAppearanceTrack GetCurrent(int index) => index == 0 ? track : null;
        public void Play(ProfessionNativeAppearanceSkeleton skeleton, string name, bool loop)
        {
            animation = new ProfessionNativeAnimationChannels(skeleton.Source, name);
            track.Animation = new Animation { Name = name }; Position = 0; this.loop = loop;
        }
        public void Apply(ProfessionNativeAppearanceSkeleton skeleton, float deltaTime)
        { Position += deltaTime; PartnerNativePortraitPose.Evaluate(skeleton, animation, Position, loop); }
        public Material MaterialForPage(string page, int blend)
        { if (blend != 0) throw new NotSupportedException("Bind the original alternate slot material."); return material; }
        public Texture TextureForPage(string page) => resource.TextureForPage(page);
    }
    private int errors;
    private string work;
    private Camera cameraUi;
    private RenderTexture target;
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var check = Check();
        while (true)
        {
            object value;
            try { if (!check.MoveNext()) break; value = check.Current; }
            catch (Exception error) { PartnerPanelNativeGraphicValidation.Finish(error); yield break; }
            yield return value;
        }
        PartnerPanelNativeGraphicValidation.Finish(errors == 0 ? null : new Exception("Runtime errors: " + errors));
    }
    private void OnLog(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    private IEnumerator Check()
    {
        work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var catalog = OfficialPartnerPanelAssetCatalog.Load();
        var prototypes = catalog.PanelPrefab.GetComponentsInChildren<ProfessionNativeUiGraphic>(true);
        Require(prototypes.Length == 6 && prototypes.All(value => !value.skeletonDataAsset), "Original six deferred avatar prototypes");
        Require(catalog.PanelPrefab.GetComponentsInChildren<Transform>(true).All(value => value.GetComponents<Component>().All(component => component)), "All original panel components resolve");
        Require(prototypes.All(value => value.startingAnimation == "home/idle_M_01" && value.material && value.meshGenerator.settings.useClipping), "Original serialized avatar settings/material");
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("OriginalNativeCanvasCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.backgroundColor = new Color(.15f, .18f, .2f);
        cameraUi.clearFlags = CameraClearFlags.SolidColor;
        target = new RenderTexture(450, 800, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("OriginalNativeCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1; canvas.scaleFactor = 450f / 1080;
        var mask = new GameObject("NativeCanvasMask", typeof(RectTransform), typeof(RectMask2D)); mask.transform.SetParent(canvas.transform, false);
        var maskRect = (RectTransform)mask.transform; maskRect.sizeDelta = new Vector2(1080, 1920);
        foreach (int id in new[] { 8401, 10412 })
        {
            var owner = new GameObject("OriginalNativeCanvasGraphic", typeof(RectTransform), typeof(CanvasRenderer));
            owner.SetActive(false); owner.transform.SetParent(mask.transform, false);
            var graphic = owner.AddComponent<ProfessionNativeUiGraphic>();
            EditorUtility.CopySerialized(prototypes[0], graphic);
            owner.SetActive(true); yield return null;
            Require(graphic.State == null && graphic.canvasRenderers.Count == 0, "Null prototype has no fabricated geometry");
            var resource = OfficialPartnerPortraitAsset.Load(id);
            var pose = new PoseSource(resource, graphic.material);
            graphic.Configure(pose); graphic.skeletonDataAsset = resource; graphic.startingAnimation = "idle_01";
            graphic.allowMultipleCanvasRenderers = true; graphic.freeze = true;
            graphic.rectTransform.anchoredPosition = resource.LiveOffset; graphic.rectTransform.localScale = resource.LiveScale;
            int geometryEvents = 0, materialEvents = 0, rebuilds = 0;
            graphic.AssignMeshOverrideMultipleRenderers += (count, meshes, materials, textures) => {
                Require(count == 1 && meshes[0].vertexCount > 0 && materials[0] && textures[0], "Actual original canvas batch arrays"); geometryEvents++;
            };
            graphic.OnMeshAndMaterialsUpdated += value => materialEvents++;
            graphic.OnAnimationRebuild += value => rebuilds++;
            graphic.Initialize(true);
            Require(rebuilds == 1 && geometryEvents == 1 && materialEvents == 1 && graphic.canvasRenderers.Count == 1, "Native initialize event order/count");
            Vector3[] first = null;
            foreach (float time in new[] { 0f, .37f, .81f })
            {
                pose.Position = time; graphic.ApplyAnimation(); yield return null;
                var mesh = graphic.canvasRenderers[0].GetMesh();
                Require(mesh && mesh.vertexCount > 0 && mesh.vertices.All(value => float.IsFinite(value.x) && float.IsFinite(value.y)), "Finite original Canvas mesh");
                if (first == null) first = mesh.vertices;
                else Require(!first.SequenceEqual(mesh.vertices), "Actual native idle pose changes Canvas vertices");
                Capture("native-canvas-" + id + "-" + time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
            owner.SetActive(false); owner.SetActive(true); yield return null;
            Require(graphic.canvasRenderers[0].GetMesh().vertexCount > 0, "Frozen graphic redraws when reopened");
            graphic.MatchRectTransformWithBounds(); Require(graphic.rectTransform.sizeDelta.x > 0 && graphic.rectTransform.sizeDelta.y > 0, "Native mesh bounds drive the rect");
            maskRect.sizeDelta = new Vector2(540, 800); yield return null; Capture("native-canvas-" + id + "-masked.png", maskRect);
            maskRect.sizeDelta = new Vector2(1080, 1920);
            graphic.Release(); graphic.Release(); Require(graphic.State == null && graphic.MaterialsMultipleCanvasRenderers.Length == 0, "Idempotent geometry release");
            Destroy(owner); yield return null;
        }
    }
    private void Capture(string name, RectTransform expectedMask = null)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
        if (expectedMask)
        {
            var corners = new Vector3[4]; expectedMask.GetWorldCorners(corners);
            var points = corners.Select(value => cameraUi.WorldToScreenPoint(value)).ToArray();
            float left = points.Min(value => value.x), right = points.Max(value => value.x);
            float bottom = points.Min(value => value.y), top = points.Max(value => value.y);
            var pixels = texture.GetPixels32(); var background = pixels[0]; int inside = 0;
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                var pixel = pixels[y * texture.width + x];
                bool visible = Math.Abs(pixel.r - background.r) > 1 || Math.Abs(pixel.g - background.g) > 1 || Math.Abs(pixel.b - background.b) > 1;
                bool outside = x < left - 1 || x > right + 1 || y < bottom - 1 || y > top + 1;
                Require(!visible || !outside, "Actual GPU pixels stay inside the original RectMask2D boundary");
                if (visible) inside++;
            }
            Require(inside > 1000, "Original portrait remains visible inside the GPU mask");
        }
        File.WriteAllBytes(Path.Combine(work, name), texture.EncodeToPNG()); RenderTexture.active = null; Destroy(texture);
    }
    private static void Require(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); }
}
