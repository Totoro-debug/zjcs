using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    // Isolated original material/mesh playback. This does not replace the battle
    // prefab, Animator, invitation UI or the formal battle acceptance flow.
    [InitializeOnLoad]
    public static class OfficialPartnerNativeBattlePoseAcceptance
    {
        private const string Active = "Zjcs.PartnerNativeBattlePose";
        static OfficialPartnerNativeBattlePoseAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner battle pose validation").AddComponent<PartnerNativeBattlePoseRunner>();
            };
        }
        public static void Run()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !project.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Partner battle pose validation requires its isolated project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_BATTLE_POSES_PASSED: selected original full-binary meshes, mapped Low textures, "
                + "material parameters, original transforms and current converted shader rendered in Play Mode. "
                + "Animator, battle integration and Windows Player remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeBattlePoseRunner : MonoBehaviour
    {
        private const string Root = "Assets/Official/Combat/NativePartners/";
        private sealed class Actor : IDisposable
        {
            public Dictionary<string, object> Row, Renderer;
            public ProfessionNativeAppearanceSkeleton Skeleton;
            public ProfessionNativeMeshBatches Batches;
            public Transform Transform;
            public Material Material;
            public List<MeshRenderer> Draws = new List<MeshRenderer>();
            public void Dispose() { Batches?.Dispose(); if (Material) Destroy(Material); }
        }
        private int errors;
        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialPartnerNativeBattlePoseAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeBattlePoseAcceptance.Finish(errors == 0 ? null : new InvalidOperationException("Original battle mesh rendering errors: " + errors));
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors++; }
        private IEnumerator Check()
        {
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Original battle pose camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.15f, .18f, .2f); camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(768, 1365, 24); camera.targetTexture = target;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            var catalog = Obj(OfficialEquipmentJson.Parse(File.ReadAllText(Root + "catalog.json")));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/PartnerNativeBattleValidation/OriginalSkeletonEffect.shader");
            if (!shader || !shader.isSupported) throw new InvalidDataException("Original current battle shader is unavailable.");
            foreach (int id in new[] { 11001, 11003, 11005, 11007, 11042 })
            {
                var row = Arr(catalog["fighters"]).Select(Obj).Single(value => Int(value, "npcId") == id);
                var root = new GameObject("Original partner " + id);
                var transforms = new Dictionary<string, Transform>();
                var actors = new List<Actor>();
                try
                {
                    foreach (var part in new[] { row }.Concat(Arr(row["parts"]).Select(Obj)))
                        actors.Add(CreateActor(part, root.transform, transforms, shader));
                    string idle = (string)Obj(row["aliases"])["idle_01"];
                    string skill = actors[0].Skeleton.Source.AnimationNames.First(value => value.Contains("/skill_01", StringComparison.Ordinal));
                    foreach (string name in new[] { idle, skill })
                    {
                        var clip = new ProfessionNativeAnimationChannels(actors[0].Skeleton.Source, name);
                        foreach (float fraction in new[] { 0f, .5f, .8f })
                        {
                            float time = clip.Duration * fraction;
                            foreach (Actor actor in actors)
                            {
                                bool body = actor == actors[0];
                                string animation = body ? name : (string)actor.Renderer["animationName"];
                                var channels = new ProfessionNativeAnimationChannels(actor.Skeleton.Source, animation);
                                ProfessionNativeOrderedConstraintPose.EvaluateBeforePhysics(actor.Skeleton, channels,
                                    time * (body ? 1 : Number(actor.Renderer, "timeScale")), !body && Flag(actor.Renderer, "loop"));
                                Draw(actor);
                            }
                            Bounds bounds = actors.SelectMany(actor => actor.Draws).Where(draw => draw.enabled).First().bounds;
                            foreach (MeshRenderer draw in actors.SelectMany(actor => actor.Draws).Where(draw => draw.enabled)) bounds.Encapsulate(draw.bounds);
                            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 10);
                            camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.12f;
                            yield return null;
                            Capture(camera, target, id + "-" + name.Replace('/', '-') + "-" + fraction.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                        }
                    }
                }
                finally { foreach (Actor actor in actors) actor.Dispose(); Destroy(root); }
                yield return null;
            }
            Destroy(target); Destroy(pipeline);
        }
        private static Actor CreateActor(Dictionary<string, object> row, Transform parent,
            Dictionary<string, Transform> transforms, Shader shader)
        {
            var actor = new Actor { Row = row, Renderer = Obj(row["renderer"]) };
            try
            {
                foreach (var value in Arr(row["transforms"]).Select(Obj))
                {
                    string key = (string)value["sourceKey"];
                    if (!transforms.TryGetValue(key, out Transform node))
                    {
                        node = new GameObject((string)value["name"]).transform; node.SetParent(parent, false);
                        node.gameObject.layer = Int(value, "layer");
                        node.localPosition = Vector(value["position"]); node.localScale = Vector(value["scale"]);
                        var rotation = Obj(value["rotation"]);
                        node.localRotation = new Quaternion(Number(rotation, "x"), Number(rotation, "y"), Number(rotation, "z"), Number(rotation, "w"));
                        node.gameObject.SetActive(Convert.ToBoolean(value["active"])); transforms.Add(key, node);
                    }
                    parent = node;
                }
                actor.Transform = parent;
                var data = ProfessionNativeSkeletonData.LoadPartnerBattle(File.ReadAllBytes(Root + (string)row["package"]), Int(row, "npcId"));
                actor.Skeleton = new ProfessionNativeAppearanceSkeleton(data);
                actor.Skeleton.SetSkin(data.CreateCombinedNpcSkin(Arr(row["skins"]).Select(value => (string)value).ToArray()));
                actor.Batches = new ProfessionNativeMeshBatches(data.SlotCount);
                var materials = Arr(OfficialEquipmentJson.Parse(File.ReadAllText(Root + (string)row["materials"])));
                if (materials.Count != 1) throw new NotSupportedException("Bind original partner multipage draw materials.");
                var material = Obj(materials[0]); var fields = Obj(material["fields"]); var saved = Obj(fields["m_SavedProperties"]);
                actor.Material = new Material(shader) { name = (string)fields["m_Name"],
                    renderQueue = Int(fields, "m_CustomRenderQueue"), enableInstancing = Flag(fields, "m_EnableInstancingVariants"),
                    doubleSidedGI = Flag(fields, "m_DoubleSidedGI"), globalIlluminationFlags = (MaterialGlobalIlluminationFlags)Int(fields, "m_LightmapFlags") };
                actor.Material.shaderKeywords = Arr(fields["m_ValidKeywords"]).Select(value => (string)value).ToArray();
                foreach (var value in Arr(saved["m_Floats"]).Select(Arr)) actor.Material.SetFloat((string)value[0], Convert.ToSingle(value[1]));
                foreach (var value in Arr(saved["m_Ints"]).Select(Arr)) actor.Material.SetInteger((string)value[0], Convert.ToInt32(value[1]));
                foreach (var value in Arr(saved["m_Colors"]).Select(Arr))
                {
                    var color = Obj(value[1]);
                    actor.Material.SetColor((string)value[0], new Color(Number(color, "r"), Number(color, "g"), Number(color, "b"), Number(color, "a")));
                }
                foreach (var value in Arr(saved["m_TexEnvs"]).Select(Arr))
                {
                    var environment = Obj(value[1]); var scale = Obj(environment["m_Scale"]); var offset = Obj(environment["m_Offset"]);
                    actor.Material.SetTextureScale((string)value[0], new Vector2(Number(scale, "x"), Number(scale, "y")));
                    actor.Material.SetTextureOffset((string)value[0], new Vector2(Number(offset, "x"), Number(offset, "y")));
                }
                foreach (var value in Arr(material["runtimeTextures"]).Select(Obj))
                    actor.Material.SetTexture((string)value["property"], AssetDatabase.LoadAssetAtPath<Texture2D>(Root + (string)value["file"]));
                return actor;
            }
            catch { actor.Dispose(); throw; }
        }
        private static void Draw(Actor actor)
        {
            actor.Batches.Build(actor.Skeleton.Pose, actor.Skeleton.CreateDrawSlots(), Flag(actor.Renderer, "useClipping"),
                Flag(actor.Renderer, "pmaVertexColors"), Number(actor.Renderer, "zSpacing"), true);
            var original = Obj(actor.Row["meshRenderer"]);
            int index = 0;
            foreach (var batch in actor.Batches.Batches)
            {
                if (batch.Blend != 0) throw new NotSupportedException("Bind original partner additive draw material.");
                if (index == actor.Draws.Count)
                {
                    var child = new GameObject("Original partner mesh batch " + index, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(actor.Transform, false); actor.Draws.Add(child.GetComponent<MeshRenderer>());
                }
                var draw = actor.Draws[index++]; draw.GetComponent<MeshFilter>().sharedMesh = batch.Mesh;
                draw.sharedMaterial = actor.Material; draw.enabled = Flag(original, "m_Enabled");
                draw.shadowCastingMode = (ShadowCastingMode)Int(original, "m_CastShadows"); draw.receiveShadows = Flag(original, "m_ReceiveShadows");
                draw.sortingLayerID = Int(original, "m_SortingLayerID"); draw.sortingOrder = Int(original, "m_SortingOrder");
            }
            for (; index < actor.Draws.Count; index++) actor.Draws[index].enabled = false;
        }
        private static void Capture(Camera camera, RenderTexture target, string name)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null; Destroy(image);
        }
        private static Dictionary<string, object> Obj(object value) => (Dictionary<string, object>)value;
        private static List<object> Arr(object value) => (List<object>)value;
        private static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key]);
        private static float Number(Dictionary<string, object> value, string key) => Convert.ToSingle(value[key]);
        private static bool Flag(Dictionary<string, object> value, string key) => Convert.ToBoolean(value[key]);
        private static Vector3 Vector(object value) { var row = Obj(value); return new Vector3(Number(row, "x"), Number(row, "y"), Number(row, "z")); }
    }
}
