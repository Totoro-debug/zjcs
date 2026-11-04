using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativeSkillGraphProbe
    {
        private const string Active = "Zjcs.PartnerNativeSkillGraphProbe";
        static OfficialPartnerNativeSkillGraphProbe()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner full skill graph probe").AddComponent<PartnerNativeSkillGraphProbeRunner>();
            };
        }
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !File.Exists(Path.Combine(workspace, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use an owned partner full skill graph workspace.");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            ImportEmbeddedTextures();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        public static void ImportEmbeddedTextures()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string path = "Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets/npc-11007-skill-high.png";
            var bindings = Zjcs.Offline.Equipment.OfficialEquipmentJson.Parse(Resources.Load<TextAsset>("Combat/NativePartnerAssets/skill-skeleton-bindings").text)
                as Dictionary<string, object>;
            var row = (Dictionary<string, object>)((List<object>)bindings["graphs"])[0];
            var original = (Dictionary<string, object>)row["texture"];
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.textureShape = TextureImporterShape.Texture2D;
            importer.npotScale = TextureImporterNPOTScale.None; importer.sRGBTexture = Convert.ToInt32(original["color_space"]) == 1;
            importer.alphaIsTransparency = false; importer.mipmapEnabled = Convert.ToInt32(original["mip_count"]) > 1;
            importer.maxTextureSize = Mathf.NextPowerOfTwo(Math.Max(Convert.ToInt32(original["width"]), Convert.ToInt32(original["height"])));
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = (FilterMode)Convert.ToInt32(original["filter_mode"]); importer.anisoLevel = Convert.ToInt32(original["aniso"]);
            importer.mipMapBias = Convert.ToSingle(original["mip_bias"]);
            importer.wrapModeU = (TextureWrapMode)Convert.ToInt32(original["wrap_u"]);
            importer.wrapModeV = (TextureWrapMode)Convert.ToInt32(original["wrap_v"]);
            importer.wrapModeW = (TextureWrapMode)Convert.ToInt32(original["wrap_w"]);
            importer.isReadable = true; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture.width != Convert.ToInt32(original["width"]) || texture.height != Convert.ToInt32(original["height"])
                || texture.mipmapCount != Convert.ToInt32(original["mip_count"]) || texture.filterMode != importer.filterMode
                || texture.anisoLevel != importer.anisoLevel || texture.mipMapBias != importer.mipMapBias
                || texture.wrapModeU != importer.wrapModeU || texture.wrapModeV != importer.wrapModeV || texture.wrapModeW != importer.wrapModeW
                || UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat) != importer.sRGBTexture)
                throw new InvalidDataException("Original embedded high texture import settings differ.");
            var pixels = texture.GetPixels32(); var rgba = new byte[pixels.Length * 4];
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                Color32 color = pixels[(texture.height - 1 - y) * texture.width + x]; int index = (y * texture.width + x) * 4;
                rgba[index] = color.r; rgba[index + 1] = color.g; rgba[index + 2] = color.b; rgba[index + 3] = color.a;
            }
            using var hash = System.Security.Cryptography.SHA256.Create();
            if (BitConverter.ToString(hash.ComputeHash(rgba)).Replace("-", "").ToLowerInvariant() != (string)original["rgba_sha256"])
                throw new InvalidDataException("Original embedded high texture pixels differ.");
            importer.isReadable = Convert.ToBoolean(original["readable"]); importer.SaveAndReimport();
        }
        public static void ImportAndValidate()
        {
            ImportEmbeddedTextures();
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
        }
        internal static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_SKILL_GRAPH_PROBE_PASSED: bounded original prefab playback through entity views. Natural completion, actual audio, formal invitations and Windows Player remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeSkillGraphProbeRunner : MonoBehaviour
    {
        [Serializable] private sealed class Catalog { public Partner[] partners; public Skill[] skills; }
        [Serializable] private sealed class Partner { public int npc_id; public Selection[] skills; }
        [Serializable] private sealed class Selection { public int EcEntityId; public int[] RandomSkillEntityId; }
        [Serializable] private sealed class Skill { public int skill_entity_id; public Variant[] variants; }
        [Serializable] private sealed class Variant { public string field; public OfficialBattleSkillPlayableCue[] playables; }
        private int errors;
        private IEnumerator Start()
        {
            Application.logMessageReceived += (_, __, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; };
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialPartnerNativeSkillGraphProbe.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeSkillGraphProbe.Finish(errors == 0 ? null : new InvalidDataException("Partner full skill graph errors: " + errors));
        }
        private IEnumerator Check()
        {
            var data = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Combat/partner-native-playables").text);
            var assets = PartnerNativeBattleAssetCatalog.Load();
            var graphCatalog = OfficialBattleSkillGraphCatalog.TryLoadDefault()
                ?? throw new InvalidDataException("Original skill graph catalog is not packaged.");
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var output = new GameObject("Original partner graph output", typeof(Camera)).GetComponent<Camera>();
            output.orthographic = true; output.transform.position = new Vector3(4, 4, -10);
            output.clearFlags = CameraClearFlags.SolidColor; output.backgroundColor = new Color(.15f, .18f, .2f);
            var texture = new RenderTexture(768, 1365, 24); output.targetTexture = texture;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            float previousCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60f;
            int variants = 0;
            try
            {
                foreach (var partner in data.partners)
                foreach (int key in partner.skills.SelectMany(row => new[] { row.EcEntityId }.Concat(row.RandomSkillEntityId ?? Array.Empty<int>())).Distinct())
                foreach (var variant in data.skills.Single(row => row.skill_entity_id == key).variants)
                {
                    string only = Environment.GetEnvironmentVariable("ZJCS_PARTNER_GRAPH_ONLY");
                    if (!string.IsNullOrEmpty(only) && only != partner.npc_id.ToString()) continue;
                    Debug.Log($"Partner graph beginning: {partner.npc_id}/{key}/{variant.field}");
                    var world = new GameObject("Partner graph " + key);
                    using (var actor = new PartnerNativeBattleActor(assets, partner.npc_id, world.transform, "Original source"))
                    using (var binding = new PartnerNativeBattleAnimationBinding(actor))
                    using (var target = new PartnerNativeBattleActor(assets, 11001, world.transform, "Original target"))
                    using (var targetBinding = new PartnerNativeBattleAnimationBinding(target))
                    {
                        int direction = variant.field == "DownPlayablePrefab" ? 0 : variant.field == "LeftPlayablePrefab" ? 1
                            : variant.field == "UpPlayablePrefab" ? 2 : 3;
                        var sourceState = new FightBattleUnitState { Id = 1, MonsterId = Convert.ToInt32(actor.OriginalRow["entityId"]),
                            Hp = 100, X = 4, Y = 4, Direction = direction };
                        var targetState = new FightBattleUnitState { Id = 2, MonsterId = Convert.ToInt32(target.OriginalRow["entityId"]),
                            Hp = 100, X = 5, Y = 4, Direction = 1 };
                        var checkpoint = new FightBattleCheckpoint { Units = new List<FightBattleUnitState> { sourceState, targetState } };
                        var mapper = new OfficialBattleGridMapper(world.transform, new FightBattleGrid { Width = 10, Height = 10 });
                        binding.Source.transform.position = mapper.GridToWorld(new FightGridPoint(4, 4));
                        targetBinding.Source.transform.position = mapper.GridToWorld(new FightGridPoint(5, 4));
                        Transform Unit(int id) => id == 1 ? binding.Source.transform : id == 2 ? targetBinding.Source.transform : null;
                        var cameraType = typeof(OfficialBattleSkillGraphPlayback).Assembly.GetType("Zjcs.Presentation.Combat.OfficialBattleCameraView", true);
                        using var camera = (IDisposable)Activator.CreateInstance(cameraType, BindingFlags.Instance | BindingFlags.NonPublic,
                            null, new object[] { output, world.transform, 100, output.transform.position, (Func<int, Transform>)Unit }, null);
                        var refresh = cameraType.GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
                        cameraType.GetMethod("SetVisible", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, new object[] { true });
                        var scope = new OfflineBattleNativePlayableScope(world.transform, null, (IBattleCameraOffsetHost)camera,
                            cameraPerformanceHost: (IBattleCameraPerformanceHost)camera);
                        var entities = new OfficialBattleSkillEntityPlayback(world.transform, mapper, scope,
                            id => checkpoint.Units.SingleOrDefault(unit => unit.Id == id));
                        using (var playback = new OfficialBattleSkillGraphPlayback(graphCatalog, scope, entities))
                        {
                            var definition = combat.CreateSkill(key, 0, 0, 1, 1, 0, new List<FightPropertyValue>());
                            // This bounded graph probe holds the view entity until disposal;
                            // it does not synthesize a gameplay duration or settle damage.
                            var cast = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = key, Slot = 0,
                                TargetIds = new List<int> { 2 }, Presentation = FightBattleSkillCastPresentation.Capture(sourceState, definition,
                                    new List<FightBattleUnitState> { targetState }) };
                            playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { cast } }, checkpoint, Unit);
                            CheckBindings(world);
                            if (key == 58550 && world.GetComponentsInChildren<PartnerNativeSkillSkeleton>(true).Length != 1)
                                throw new InvalidDataException("Original embedded partner adapter was not used.");
                            float lastCue = variant.playables.Length == 0 ? 0 : variant.playables.Max(cue => cue.time);
                            int frames = Mathf.CeilToInt(Mathf.Max(2f, lastCue + .5f) * 60);
                            for (int frame = 0; frame < frames; frame++)
                            {
                                playback.Tick(1f / 60f); actor.Advance(1f / 60f); target.Advance(1f / 60f); playback.LateTick();
                                CheckBindings(world);
                                refresh.Invoke(camera, new object[] { checkpoint, FightBattlePhase.Play });
                                yield return null;
                                if (errors != 0) throw new InvalidDataException($"Original graph emitted errors: {partner.npc_id}/{key}/{variant.field}");
                                if (frame == 12 || frame == 36 || frame == frames - 1)
                                    Capture(output, texture, $"graph-{partner.npc_id}-{key}-{variant.field}-{frame}");
                            }
                        }
                        actor.Root.gameObject.SetActive(false); target.Root.gameObject.SetActive(false);
                    }
                    world.SetActive(false); Destroy(world); variants++; yield return null;
                }
                Debug.Log("Bounded original partner full graph variants: " + variants);
                if (variants == 0 || (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_PARTNER_GRAPH_ONLY")) && variants != 93))
                    throw new InvalidDataException("Original partner full graph variant coverage differs.");
            }
            finally { Time.captureDeltaTime = previousCaptureDelta; }
        }
        private static void CheckBindings(GameObject world)
        {
            foreach (var embedded in world.GetComponentsInChildren<PartnerNativeSkillSkeleton>(true))
            {
                var port = embedded.GetComponent<PartnerNativeBattleRendererPort>();
                var renderer = embedded.transform.Find("0").GetComponent<MeshRenderer>();
                if (!port || port.SkeletonDataName != "npc_11007_SkeletonData" || embedded.GetComponent<MeshRenderer>().enabled
                    || !renderer.enabled || renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount == 0
                    || renderer.sharedMaterial.mainTexture.width != 2048 || renderer.sharedMaterial.mainTexture.height != 668
                    || embedded.GetComponents<MonoBehaviour>().Any(component => component && component.GetType().FullName == "Spine.Unity.SkeletonAnimation"))
                    throw new InvalidDataException("Original embedded partner pose/material/separator binding differs.");
            }
            foreach (var playable in world.GetComponentsInChildren<MyPlayable>(true))
            {
                if (!playable.myInited) continue;
                var attribute = (MyPlayableApplyAttribute)Attribute.GetCustomAttribute(playable.GetType(), typeof(MyPlayableApplyAttribute), true);
                if (attribute != null && attribute.myTypes.Length > 0 && !playable.myApplyComponent)
                    throw new InvalidDataException("Original graph node has no application component: " + playable.GetType().Name + "/" + playable.name);
            }
        }
        private static void Capture(Camera camera, RenderTexture target, string name)
        {
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Destroy(pixels); }
        }
    }
}
