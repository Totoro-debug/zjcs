using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.PartnersUI;

// Isolated mesh/GPU verification, not a replacement for the official window.
[InitializeOnLoad]
public static class PartnerPortraitGraphicsValidation
{
    static PartnerPortraitGraphicsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerPortraitGraphics", false))
                new GameObject("OriginalPartnerPortraitGeometry").AddComponent<PartnerPortraitGraphicsRunner>();
        };
    }
    public static void Run()
    {
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerPortraitGraphics", true); EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerPortraitGraphics", false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_PORTRAIT_GRAPHICS_PASSED: original 8401 native idle/click mesh and original converted UI shader rendered in Play Mode. Full window, transition mixing and click interaction remain pending.");
        if (error == null) PartnerNativePortraitsValidation.Run();
        else EditorApplication.Exit(1);
    }
}

public sealed class PartnerPortraitGraphicsRunner : MonoBehaviour
{
    private string work;
    private Camera cameraUi;
    private RenderTexture target;
    private Canvas canvas;
    private CanvasRenderer rendererUi;
    private int errors;
    private IEnumerator Start()
    {
        var check = Check();
        while (true)
        {
            object current;
            try { if (!check.MoveNext()) break; current = check.Current; }
            catch (Exception error) { PartnerPortraitGraphicsValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerPortraitGraphicsValidation.Finish(errors == 0 ? null : new Exception("Original portrait rendering errors: " + errors));
    }
    private IEnumerator Check()
    {
        work = Environment.GetEnvironmentVariable("ZJCS_PARTNER_PORTRAIT_DIR");
        Application.logMessageReceived += (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
        var data = ProfessionNativeSkeletonData.LoadPartnerPortrait(File.ReadAllBytes(Path.Combine(work, "native-portraits/8401/portrait.bytes")), 8401);
        var skeleton = new ProfessionNativeAppearanceSkeleton(data); skeleton.SetSkin(data.SkinCatalog.FindSkin("default"));
        using var batches = new ProfessionNativeMeshBatches(data.SlotCount);
        var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        cameraUi = new GameObject("OriginalPortraitCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor;
        cameraUi.backgroundColor = new Color(.15f, .18f, .2f);
        target = new RenderTexture(768, 1365, 24); cameraUi.targetTexture = target;
        canvas = new GameObject("OriginalPortraitCanvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1; canvas.scaleFactor = 768f / 1080;
        var portrait = new GameObject("OriginalPortraitMesh", typeof(RectTransform), typeof(CanvasRenderer)); portrait.transform.SetParent(canvas.transform, false);
        // Actual selected npc config replaces the generic live prefab transform.
        var rect = (RectTransform)portrait.transform; rect.anchoredPosition = new Vector2(-23, 442); rect.localScale = Vector3.one * .41999998688697815f;
        rendererUi = portrait.GetComponent<CanvasRenderer>();
        var material = Instantiate(AssetDatabase.LoadAssetAtPath<Material>("Assets/PartnerCurrentValidation/Portrait/SkeletonGraphicDefault.mat"));
        if (!material || !material.shader.isSupported) throw new InvalidOperationException("Original current UI shader is unavailable.");
        var resource = OfficialPartnerPortraitAsset.Load(8401);
        var texture = resource.TextureForPage(resource.Pages[0].Name);
        if (!texture || texture.width != 556 || texture.height != 1264 || texture.mipmapCount != 1 ||
            texture.filterMode != FilterMode.Bilinear || texture.wrapMode != TextureWrapMode.Repeat)
            throw new InvalidOperationException("Original installed portrait texture descriptor differs.");
        rendererUi.materialCount = 1; rendererUi.SetMaterial(material, 0); rendererUi.SetTexture(texture); rendererUi.SetColor(Color.white);
        foreach (string clip in new[] { "idle_01", "click_01" })
        {
            var animation = new ProfessionNativeAnimationChannels(data, clip);
            foreach (float fraction in new[] { 0f, .25f, .65f })
            {
                PartnerNativePortraitPose.Evaluate(skeleton, animation, animation.Duration * fraction, false);
                batches.Build(skeleton.Pose, skeleton.CreateDrawSlots(), true, false);
                if (batches.Batches.Count != 1 || batches.Batches[0].Blend != 0) throw new InvalidOperationException("Bind additional original portrait draw materials.");
                Mesh mesh = batches.Batches[0].Mesh; mesh.vertices = mesh.vertices.Select(value => value * canvas.referencePixelsPerUnit).ToArray(); mesh.RecalculateBounds();
                // Batch-mode editors do not invoke WaitForEndOfFrame. Capture submits
                // the original UI shader explicitly through the active render pipeline.
                rendererUi.SetMesh(mesh); yield return null;
                Capture("portrait-8401-" + clip + "-" + fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
        }
    }
    private void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); RenderPipeline.SubmitRenderRequest(cameraUi, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(work, name), image.EncodeToPNG()); RenderTexture.active = null; Destroy(image);
    }
}
