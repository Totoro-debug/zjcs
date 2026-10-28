using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionPhotographerValidation
    {
        private const string Active = "Zjcs.ProfessionPhotographer.Active";
        private static bool ready;
        private static int expectedErrors, observedErrors;
        private static string unexpectedError;
        private static readonly List<Object> owned = new List<Object>();

        private sealed class Graph : IProfessionPortraitRendererGraph
        {
            public Component Root { get; set; }
            public bool HasSeparator { get; set; }
            public IReadOnlyList<MeshRenderer> SeparatedRenderers { get; set; }
            public IReadOnlyList<ProfessionPortraitRenderPart> LoadedParts { get; set; }
        }
        private sealed class Request : ICharacterPortraitRequestor
        {
            public readonly List<string> Calls;
            public ProfessionPortraitKind Kind;
            public bool ThrowAppearance;
            public Request(List<string> calls) { Calls = calls; }
            public long GetRequestId() => 1;
            public object GetAppearanceData() { Calls.Add("appearance"); if (ThrowAppearance) throw new InvalidOperationException("appearance-fixture-failure"); return this; }
            public ProfessionPortraitKind GetPortraitType() { Calls.Add("type"); return Kind; }
            public int GetSex() => 0;
            public int GetProfession() => 10;
            public string GetAnimationName() => "200/idle_01";
            public bool NeedPersist() => false;
            public bool TryGetCancellationToken(out CancellationToken token) { token = CancellationToken.None; return true; }
            public void OnTextureUpdate(Texture2D texture) { }
            public void StopRequest() { }
        }
        private sealed class Bindings : IProfessionPortraitPhotographerBindings, ICharacterPortraitBuilder
        {
            public readonly List<string> Calls = new List<string>();
            public PlayerPortraitSettings Configuration;
            public Graph Graph;
            public bool ThrowBuilder;
            public PlayerPortraitSettings Settings { get { Calls.Add("settings"); return Configuration; } }
            public ICharacterPortraitBuilder CreateBuilder(ProfessionPortraitKind type) { Calls.Add("create:" + type); return this; }
            public IProfessionPortraitRendererGraph GetRendererGraph(Component animation) { Calls.Add("graph"); return Graph; }
            public void ReleaseAppearance(Component appearance, bool tryUnloadAllAssets) { Calls.Add("release:" + tryUnloadAllAssets); }
            public string DescribeAppearance(object appearance) { Calls.Add("describe"); return "appearance-fixture"; }
            public void HandleAppearanceCharacter(Component appearance, ICharacterPortraitRequestor requestor)
            { Calls.Add("build"); if (ThrowBuilder) throw new InvalidOperationException("builder-fixture-failure"); }
        }
        static ProfessionPhotographerValidation()
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
                Application.runInBackground = true;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Photographer fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                Application.logMessageReceived += OnLog;
                try { ValidateCollectors(); ValidateCapture(); }
                finally { Application.logMessageReceived -= OnLog; }
                Require(observedErrors == 3, "all three explicitly expected setup-failure diagnostics were observed");
                Require(unexpectedError == null, "no unexpected capture diagnostic: " + unexpectedError);
                Debug.Log("Profession original photographer passed: real GPU CommandBuffer/submesh capture and raw texture readback at original 260x310 and 100x120 settings; builder cache, call/release order, setup-failure boundaries, native renderer/separator/part/particle collection, descending world-Z and particle color masks. Capture geometry, appearance and renderer graph are fixtures; the original character studio and full profession UI are not covered.");
                Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (expectedErrors > 0 && (condition.StartsWith("请求对象丢失！") || condition.StartsWith("皮肤设置错误 皮肤数据")))
            { expectedErrors--; observedErrors++; return; }
            unexpectedError = condition;
        }
        private static void ValidateCollectors()
        {
            var root = Renderer("Root", 1); var far = Renderer("FarSeparator", 5); var near = Renderer("NearSeparator", -2); var part = Renderer("Part", 3);
            var particleGo = Own(new GameObject("ParticleFixture", typeof(ParticleSystem)));
            particleGo.transform.position = new Vector3(0, 0, 2);
            var particle = particleGo.GetComponent<ParticleSystemRenderer>();
            var particleMaterial = Own(new Material(Shader.Find("UI/Default")));
            particle.sharedMaterial = particleMaterial;
            var provider = part.gameObject.AddComponent<AppearancePortraitParticleProvider>();
            var sourceList = new List<ParticleSystemRenderer> { null, particle };
            Set(provider, "mParticleSystemRenderers", sourceList);
            var first = provider.GetParticleSystemRenderers();
            sourceList.Clear();
            Require(first.Count == 1 && first[0] == particle && ReferenceEquals(first, provider.GetParticleSystemRenderers()), "original particle provider caches non-null source entries once");
            var graph = new Graph { Root = root, HasSeparator = true, SeparatedRenderers = new[] { near, far }, LoadedParts = new[] { new ProfessionPortraitRenderPart(part, part.gameObject) } };
            var list = new List<Renderer> { root };
            var comparer = new RendererZSortComparer();
            ProfessionPortraitRendererCollector.CollectPortraitAndParticleRenderers(graph, list, comparer, false);
            Require(list.SequenceEqual(new Renderer[] { far, part, near }), "separator replaces root, parts remain and no particle is included");
            ProfessionPortraitRendererCollector.CollectPortraitAndParticleRenderers(graph, list, comparer, true);
            Require(list.SequenceEqual(new Renderer[] { far, part, particle, near }) && particle.material.GetFloat("_ColorMask") == 15, "original particle inclusion, material color mask and descending world-Z");
            owned.Add(particle.material);
            var meshes = new List<MeshRenderer> { root };
            ProfessionPortraitRendererCollector.CollectPortraitRenderers(graph, meshes, Comparer<MeshRenderer>.Create((x, y) => comparer.Compare(x, y)));
            Require(meshes.SequenceEqual(new[] { far, part, near }), "original mesh-only collector excludes particles");
            graph.HasSeparator = false; graph.LoadedParts = null;
            ProfessionPortraitRendererCollector.CollectPortraitAndParticleRenderers(graph, list, comparer, true);
            Require(list.Count == 1 && list[0] == root, "root mesh fallback when there is no separator");
            graph.HasSeparator = true; graph.SeparatedRenderers = Array.Empty<MeshRenderer>();
            ProfessionPortraitRendererCollector.CollectPortraitAndParticleRenderers(graph, list, comparer, true);
            Require(list.Count == 0, "an empty existing separator does not fall back to the root");
        }
        private static void ValidateCapture()
        {
            var go = Own(new GameObject("PhotographerFixture"));
            var photographer = go.AddComponent<CharacterPortraitPhotographer>();
            var cameraGo = Own(new GameObject("CaptureCameraFixture", typeof(Camera)));
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true; camera.nearClipPlane = .3f; camera.farClipPlane = 100;
            var model = Renderer("CaptureMeshFixture", 0);
            model.transform.position = new Vector3(3, -2, 1);
            var mesh = Own(new Mesh { name = "Validation submesh quad" });
            mesh.vertices = new[] { new Vector3(-1, .9f, 0), new Vector3(-1, 2.9f, 0), new Vector3(1, 2.9f, 0), new Vector3(1, .9f, 0) };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(new[] { 0, 1, 2, 2, 3, 0 }, i);
            mesh.RecalculateBounds();
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            var red = Own(new Material(Shader.Find("Sprites/Default"))); red.color = Color.red;
            var blue = Own(new Material(Shader.Find("Sprites/Default"))); blue.color = Color.blue;
            model.sharedMaterials = new[] { red, null, blue };
            var bindings = new Bindings { Configuration = AssetDatabase.LoadAssetAtPath<PlayerPortraitSettings>("Assets/Official/ProfessionUI/Portrait/PlayerPortraitSettings.asset"), Graph = new Graph { Root = model } };
            Require(bindings.Configuration != null, "original native settings");
            Set(photographer, "mCamera", camera); Set(photographer, "mAppearance", model); Set(photographer, "mAppearanceAnimation", model);
            photographer.Configure(bindings);
            var pool = new CharacterPortraitTexturePool(new GAction());
            var textures = new List<CharacterPortraitTexture2D>();
            try
            {
                photographer.Init(pool);
                object command = Field(photographer, "mCommandBuffer");
                photographer.Init(pool);
                Require(!camera.enabled && ReferenceEquals(command, Field(photographer, "mCommandBuffer")), "source Init disables camera and retains its command buffer");
                var request = new Request(bindings.Calls) { Kind = ProfessionPortraitKind.Profession };
                var texture = photographer.CapturePortraitTexture(request, out byte[] bytes); textures.Add(texture);
                Require(texture != null && bytes != null && texture.Width == 260 && texture.Height == 310 && bytes.Length == 260 * 310 * 4, "original profession size and actual raw GPU readback");
                Require(bindings.Calls.SequenceEqual(new[] { "settings", "type", "create:Profession", "type", "appearance", "build", "graph", "release:True" }), "original setup, builder, renderer capture and final appearance release order");
                var p = bindings.Configuration.GetParameters(ProfessionPortraitKind.Profession);
                Require(Mathf.Abs(camera.aspect - 260f / 310) < .000001f && camera.orthographicSize == p.Size && camera.transform.position == model.transform.position + p.GetOffset() && camera.transform.rotation == Quaternion.identity, "original aspect, orthographic size, offset and identity camera rotation");
                var center = texture.Tex.GetPixel(130, 155); var corner = texture.Tex.GetPixel(0, 0);
                Require(center.b > .99f && center.r < .01f && center.a > .99f && corner.a < .01f, "real command-buffer submesh order, null material skip and transparent clear");
                File.WriteAllBytes(ProfessionValidationWorkspace.Output("photographer-submesh-fixture.png"), texture.Tex.EncodeToPNG());
                bindings.Calls.Clear();
                var again = photographer.CapturePortraitTexture(request, out bytes); textures.Add(again);
                Require(!bindings.Calls.Any(x => x.StartsWith("create:")) && bindings.Calls.Last() == "release:True", "source builder cache is reused and appearance is still released");
                bindings.Calls.Clear(); request.Kind = ProfessionPortraitKind.Head;
                var head = photographer.CapturePortraitTexture(request, out bytes); textures.Add(head);
                Require(head.Width == 100 && head.Height == 120 && bytes.Length == 100 * 120 * 4 && bindings.Calls.Contains("create:Head"), "each original portrait kind has its own builder and native camera parameters");
                bindings.Calls.Clear(); expectedErrors = 1;
                Require(photographer.CapturePortraitTexture(null, out bytes) == null && bytes == null && expectedErrors == 0 && !bindings.Calls.Any(x => x.StartsWith("release:")), "null request is caught during setup without inventing release behavior");
                bindings.Calls.Clear(); bindings.ThrowBuilder = true; expectedErrors = 1;
                Require(photographer.CapturePortraitTexture(request, out bytes) == null && bytes == null && expectedErrors == 0 && bindings.Calls.Last() == "describe" && !bindings.Calls.Any(x => x.StartsWith("release:")), "builder setup failure logs acquired appearance and returns before render/release");
                bindings.Calls.Clear(); bindings.ThrowBuilder = false; request.ThrowAppearance = true; expectedErrors = 1;
                Require(photographer.CapturePortraitTexture(request, out bytes) == null && bytes == null && expectedErrors == 0 && !bindings.Calls.Contains("describe") && !bindings.Calls.Contains("build"), "appearance acquisition failure uses original missing-data branch");
            }
            finally
            {
                foreach (var texture in textures) texture?.DestroyTexture();
                pool.Dispose();
            }
        }
        private static MeshRenderer Renderer(string name, float z)
        {
            var go = Own(new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)));
            go.transform.position = new Vector3(0, 0, z);
            return go.GetComponent<MeshRenderer>();
        }
        private static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static void Set(object value, string name, object data) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);
        private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Photographer: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            foreach (var value in owned) if (value) Object.DestroyImmediate(value);
            EditorApplication.Exit(code);
        }
    }
}
