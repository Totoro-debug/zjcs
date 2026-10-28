using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAppearanceSkinClipValidation
    {
        private const string Active = "Zjcs.ProfessionSkinClip.Active";
        private static bool ready;
        private static int expectedErrors;
        private static string unexpected;
        private static readonly List<Object> owned = new List<Object>();

        private sealed class Graph : IProfessionAppearanceSkinClipBindings, IProfessionSkinClipCharacter,
            IProfessionSkinClipAppearance, IProfessionSkinClipSkeleton, IProfessionSkinClipSlot, IProfessionSkinClipBoundingBox,
            IProfessionSkinClipGraphic
        {
            public Component Instance { get; set; }
            public Action OnSkinUpdate { get; set; }
            public bool HasSkeleton = true, HasSlot = true, HasSkin = true, HasMaterial = true, IsGraphic = true;
            public object Attachment { get; set; }
            public int WorldVerticesLength { get; set; } = 8;
            public float[] Vertices = { -.35f, -.35f, .35f, -.35f, .35f, .35f, -.35f, .35f };
            public float MeshScale { get; set; } = 1;
            public int Computes, Finds;
            public Material Material;
            public string Name => "BoundingBoxFixture";
            public IProfessionSkinClipSkeleton Skeleton => HasSkeleton ? this : null;
            public IProfessionSkinClipGraphic Graphic => IsGraphic ? this : null;
            public List<CanvasRenderer> canvasRenderers { get; } = new List<CanvasRenderer>();
            private Action<IProfessionSkinClipGraphic> meshUpdated;
            public event Action<IProfessionSkinClipGraphic> OnMeshAndMaterialsUpdated { add { meshUpdated += value; } remove { meshUpdated -= value; } }
            public int MeshSubscribers => meshUpdated?.GetInvocationList().Length ?? 0;
            public void FireMesh() => meshUpdated?.Invoke(this);
            public void ComputeWorldVertices(IProfessionSkinClipSlot slot, float[] vertices)
            { Require(ReferenceEquals(slot, this), "original slot identity"); Computes++; Array.Copy(Vertices, vertices, 8); }
            public IProfessionSkinClipSlot FindSlot(string name) { Finds++; Require(name == "original-slot", "original slot name"); return HasSlot ? this : null; }
            public bool TryGetSkinName(SkinType type, out string name) { Require(type == SkinType.hair, "original skin enum"); name = "hair"; return HasSkin; }
            public bool TryGetMaterial(string name, out Material material) { Require(name == "hair", "original skin name"); material = Material; return HasMaterial; }
            public Component FindCharacter(Component owner) => owner.transform;
            public Component FindSpine(Component owner) => owner.transform;
            public IProfessionSkinClipCharacter GetCharacter(Component character) => this;
            public IProfessionSkinClipAppearance GetAppearance(Component spine) => this;
        }
        static ProfessionAppearanceSkinClipValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Skin-clip fixture did not enter Play Mode.");
                if (!ready || !EditorApplication.isPlaying) return;
                Application.logMessageReceived += Observe;
                try { CheckGraphic(); CheckWorld(); }
                finally { Application.logMessageReceived -= Observe; }
                Require(expectedErrors == 0 && unexpected == null, "unexpected log: " + unexpected);
                Debug.Log("Profession original skin clipping passed: four-point line/diagonal coefficients, mesh scale and transformed/reflected orientation, concave reorder, canvas matching by material or main texture, all-renderer reset before skin refresh, skin/mesh subscription lifecycle, missing data branches and original vertex-count error. Direct3D original shader output clips the square center and preserves its blue exterior; PNG written. Skeleton/weighted vertices are fixtures; no real actor or complete profession UI is validated.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void CheckGraphic()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            Texture2D blue = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false, true)); blue.SetPixel(0, 0, Color.blue); blue.Apply();
            Material source = Own(new Material(shader)), sharedTexture = Own(new Material(shader)), other = Own(new Material(shader));
            source.mainTexture = sharedTexture.mainTexture = blue; other.mainTexture = Texture2D.whiteTexture;
            var go = Own(new GameObject("SkinClipGraphicFixture", typeof(RectTransform))); go.SetActive(false);
            var graph = new Graph { Instance = go.transform, Material = source }; graph.Attachment = graph;
            ProfessionAppearanceSkinClipBindings.Current = graph;
            foreach (Material material in new[] { source, sharedTexture, other })
            {
                var child = Own(new GameObject("CanvasMaterialFixture", typeof(RectTransform), typeof(CanvasRenderer)));
                child.transform.SetParent(go.transform, false);
                var renderer = child.GetComponent<CanvasRenderer>(); renderer.materialCount = 1; renderer.SetMaterial(material, 0); graph.canvasRenderers.Add(renderer);
            }
            var clip = go.AddComponent<AppearanceSkinClip>(); Set(clip, "mSkinType", SkinType.hair); Set(clip, "mClipMeshProvideSlotName", "original-slot");
            go.SetActive(true);
            Require(graph.OnSkinUpdate?.GetInvocationList().Length == 1 && graph.MeshSubscribers == 1, "original enable subscriptions");
            graph.FireMesh();
            Near(source.GetVector("_ClipLineX"), new Vector4(0, .7f, 0, -.7f));
            Near(source.GetVector("_ClipLineY"), new Vector4(-.7f, 0, .7f, 0));
            Near(source.GetVector("_ClipLineC"), Vector4.one * -.245f);
            Near(source.GetVector("_ClipDiag"), new Vector4(.7f, -.7f, 0, 0));
            Require(source.GetFloat("_EnableHairClip") == 1 && sharedTexture.GetFloat("_EnableHairClip") == 1 && other.GetFloat("_EnableHairClip") == 0, "original material-or-texture matching");
            Capture(source);
            other.SetFloat("_EnableHairClip", 1); int computes = graph.Computes;
            graph.OnSkinUpdate();
            Require(source.GetFloat("_EnableHairClip") == 0 && sharedTexture.GetFloat("_EnableHairClip") == 0 && other.GetFloat("_EnableHairClip") == 0 && graph.Computes == computes, "skin update resets every canvas material before reinitialization");
            graph.FireMesh(); Require(graph.Computes == computes + 1, "mesh update refreshes clipping once");
            go.SetActive(false); Require(graph.OnSkinUpdate == null && graph.MeshSubscribers == 0, "original disable unsubscribes");
            graph.FireMesh(); Require(graph.Computes == computes + 1, "disabled mesh event does not update");
            go.SetActive(true); Require(graph.MeshSubscribers == 1, "reenable subscribes once");
            graph.HasSkin = false; graph.FireMesh(); Require(graph.Computes == computes + 1, "missing skin short circuits"); graph.HasSkin = true;
            graph.HasMaterial = false; graph.FireMesh(); Require(graph.Computes == computes + 1, "missing material short circuits"); graph.HasMaterial = true;
            graph.HasSkeleton = false; graph.OnSkinUpdate(); graph.FireMesh(); Require(graph.Computes == computes + 1, "missing skeleton clears bounding box"); graph.HasSkeleton = true;
            graph.HasSlot = false; graph.OnSkinUpdate(); graph.FireMesh(); Require(graph.Computes == computes + 1, "missing slot"); graph.HasSlot = true;
            graph.Attachment = new object(); graph.OnSkinUpdate(); graph.FireMesh(); Require(graph.Computes == computes + 1, "non-bounding attachment"); graph.Attachment = graph;
            graph.WorldVerticesLength = 6; expectedErrors = 1; graph.OnSkinUpdate(); graph.FireMesh(); Require(graph.Computes == computes + 1 && expectedErrors == 0, "original invalid vertex count rejects the attachment"); graph.WorldVerticesLength = 8;
            graph.OnSkinUpdate(); graph.MeshScale = 2; graph.FireMesh(); Near(source.GetVector("_ClipLineC"), Vector4.one * -.98f);
            go.SetActive(false);
        }
        private static void CheckWorld()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Game/Runtime/Official/World/NativeMap13Entities/Shaders/SpineSkeletonGraphicDye_a2f0973c.shader");
            var go = Own(new GameObject("SkinClipWorldFixture")); go.SetActive(false);
            go.transform.localPosition = new Vector3(3, -2, 0); go.transform.localRotation = Quaternion.Euler(0, 0, 27); go.transform.localScale = new Vector3(-2, 3, 1);
            var graph = new Graph { Instance = go.transform, Material = Own(new Material(shader)), IsGraphic = false }; graph.Attachment = graph;
            ProfessionAppearanceSkinClipBindings.Current = graph;
            var clip = go.AddComponent<AppearanceSkinClip>(); Set(clip, "mSkinType", SkinType.hair); Set(clip, "mClipMeshProvideSlotName", "original-slot"); go.SetActive(true);
            clip.UpdateClipMaterials(); CheckEdge(graph.Material, go.transform.TransformPoint(new Vector3(-.35f, -.35f, 0)), go.transform.TransformPoint(new Vector3(.35f, -.35f, 0)), 0);
            Require(graph.MeshSubscribers == 0 && graph.Material.GetFloat("_EnableHairClip") == 1, "original world branch writes source material without graphic subscription");
            graph.Vertices = new[] { -.35f, -.35f, -.1f, 0f, .35f, .35f, -.35f, .35f };
            clip.UpdateClipMaterials(); CheckEdge(graph.Material, go.transform.TransformPoint(new Vector3(-.1f, 0, 0)), go.transform.TransformPoint(new Vector3(.35f, .35f, 0)), 0);
            go.SetActive(false);
        }
        private static void CheckEdge(Material material, Vector3 a, Vector3 b, int edge)
        {
            float x = material.GetVector("_ClipLineX")[edge], y = material.GetVector("_ClipLineY")[edge], c = material.GetVector("_ClipLineC")[edge];
            Require(Mathf.Abs(x * a.x + y * a.y + c) < .00001f && Mathf.Abs(x * b.x + y * b.y + c) < .00001f, "original transformed edge endpoints");
            Vector3 tangent = b - a;
            Require(Mathf.Abs(x - tangent.y * -1) < .00001f && Mathf.Abs(y - -tangent.x * -1) < .00001f, "original reflected transform orientation");
        }
        private static void Capture(Material material)
        {
            var mesh = Own(new Mesh()); mesh.vertices = new[] { new Vector3(-.9f, -.9f, 0), new Vector3(-.9f, .9f, 0), new Vector3(.9f, .9f, 0), new Vector3(.9f, -.9f, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }; mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white }; mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            var target = RenderTexture.GetTemporary(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); RenderTexture previous = RenderTexture.active;
            try
            {
                AppearanceMaterialUtils.SetMaterialStencil(material, 0, 0, 8, 15, 255, 255);
                using (var commands = new CommandBuffer()) { commands.SetRenderTarget(target); commands.ClearRenderTarget(true, true, Color.clear); commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity); commands.DrawMesh(mesh, Matrix4x4.identity, material); Graphics.ExecuteCommandBuffer(commands); }
                RenderTexture.active = target; var texture = Own(new Texture2D(128, 128, TextureFormat.RGBA32, false, true)); texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); texture.Apply();
                Color32 center = texture.GetPixel(64, 64), edge = texture.GetPixel(100, 64), corner = texture.GetPixel(0, 0);
                Require(center.a == 0 && edge.b > 250 && edge.a > 250 && corner.a == 0, "original shader clips square center and preserves exterior");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("skin-clip-fixture.png"), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Near(Vector4 actual, Vector4 expected) { Require((actual - expected).sqrMagnitude < .000000001f, "original line coefficients"); }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error && expectedErrors > 0 && message.Contains("顶点数数量错误")) expectedErrors--; else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) unexpected = message; }
        private static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            foreach (Object value in owned) if (value) Object.DestroyImmediate(value);
            ProfessionAppearanceSkinClipBindings.Current = null; EditorApplication.Exit(code);
        }
    }
}
