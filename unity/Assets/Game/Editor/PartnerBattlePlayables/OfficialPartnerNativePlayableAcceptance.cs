using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativePlayableAcceptance
    {
        private const string Active = "Zjcs.PartnerNativePlayables";
        static OfficialPartnerNativePlayableAcceptance()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner Animator playable validation").AddComponent<PartnerNativePlayableRunner>();
            };
        }
        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            if (string.IsNullOrEmpty(workspace) || !project.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use an isolated partner playable validation project.");
            OfficialPartnerNativeBattleRuntimeAcceptance.Import();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        public static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_PLAYABLES_PASSED: current partner skill Animator nodes through original playables and original controllers. Skill effects, formal invitations and Windows Player remain unvalidated.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativePlayableRunner : MonoBehaviour
    {
        [Serializable] private sealed class Catalog { public string manifest; public Partner[] partners; public Skill[] skills; }
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
                catch (Exception error) { OfficialPartnerNativePlayableAcceptance.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativePlayableAcceptance.Finish(errors == 0 ? null : new InvalidDataException("Partner playable errors: " + errors));
        }
        private IEnumerator Check()
        {
            var data = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("partner-native-playables").text);
            if (data.manifest != "884_156692" || data.partners.Length != 34 || data.skills.Length != 75)
                throw new InvalidDataException("Current partner playable selection differs.");
            var catalog = PartnerNativeBattleAssetCatalog.Load();
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            var camera = new GameObject("Original partner playable camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .2f);
            var target = new RenderTexture(768, 1365, 24); camera.targetTexture = target;
            var light = new GameObject("Validation global 2D light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            int cues = 0, variants = 0;
            foreach (var partner in data.partners)
            {
                var selected = partner.skills.SelectMany(row => new[] { row.EcEntityId }.Concat(row.RandomSkillEntityId ?? Array.Empty<int>())).Distinct();
                foreach (int key in selected)
                foreach (var variant in data.skills.Single(row => row.skill_entity_id == key).variants)
                {
                    using var actor = new PartnerNativeBattleActor(catalog, partner.npc_id, null, "Original playable partner " + partner.npc_id);
                    RuntimeAnimatorController original = actor.Animator.runtimeAnimatorController;
                    using (var playback = new OfficialBattleAnimatorPlayback(actor.Animator, actor.Root))
                    {
                        float elapsed = 0;
                        foreach (var cue in variant.playables)
                        {
                            while (elapsed + .000001f < cue.time)
                            {
                                float delta = Mathf.Min(1f / 60f, cue.time - elapsed);
                                playback.Tick(delta); actor.Advance(delta); elapsed += delta;
                            }
                            if (!OfficialBattleAnimatorPlayback.Supports(actor.Animator, cue))
                                throw new InvalidDataException($"Original partner cue unsupported: {partner.npc_id}/{key}/{variant.field}/{cue.trigger_param}");
                            playback.Play(cue, null);
                            actor.Advance(0);
                            var authored = (AnimatorController)original;
                            var expected = authored.layers[0].stateMachine.states.SelectMany(row => row.state.transitions)
                                .Concat(authored.layers[0].stateMachine.anyStateTransitions)
                                .Where(row => row.conditions.Any(condition => condition.parameter == cue.trigger_param))
                                .Select(row => row.destinationState).Where(row => row != null).ToArray();
                            if (expected.Length == 0) throw new InvalidDataException("Original trigger has no authored destination: " + cue.trigger_param);
                            bool entered = false;
                            for (int frame = 0; frame < 12; frame++)
                            {
                                var current = actor.Animator.GetCurrentAnimatorStateInfo(0);
                                var next = actor.Animator.GetNextAnimatorStateInfo(0);
                                if (expected.Any(state => current.shortNameHash == Animator.StringToHash(state.name)
                                    || (actor.Animator.IsInTransition(0) && next.shortNameHash == Animator.StringToHash(state.name)))) entered = true;
                                playback.Tick(1f / 60f); actor.Advance(1f / 60f); elapsed += 1f / 60f;
                            }
                            if (!entered) throw new InvalidDataException($"Original trigger did not enter authored state: {partner.npc_id}/{key}/{cue.trigger_param}");
                            cues++;
                        }
                        Bounds? bounds = null;
                        foreach (var renderer in actor.Renderers)
                        {
                            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                            Vector3[] vertices = mesh.vertices; Color32[] colors = mesh.colors32;
                            for (int index = 0; index < vertices.Length; index++)
                            {
                                Vector3 point = renderer.transform.TransformPoint(vertices[index]);
                                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                                    throw new InvalidDataException("Partner playable vertex is not finite.");
                                if (colors.Length == vertices.Length && colors[index].a == 0) continue;
                                if (bounds.HasValue) { var value = bounds.Value; value.Encapsulate(point); bounds = value; }
                                else bounds = new Bounds(point, Vector3.zero);
                            }
                        }
                        if (!bounds.HasValue) throw new InvalidDataException("Partner playable has no geometry.");
                        camera.transform.position = new Vector3(bounds.Value.center.x, bounds.Value.center.y, bounds.Value.min.z - 10);
                        camera.orthographicSize = Mathf.Max(bounds.Value.extents.y, bounds.Value.extents.x / camera.aspect) * 1.12f;
                        yield return null;
                        Capture(camera, target, $"playable-{partner.npc_id}-{key}-{variant.field}");
                    }
                    if (actor.Animator.runtimeAnimatorController != original)
                        throw new InvalidDataException("Original partner controller was not restored after playable disposal.");
                    variants++;
                }
            }
            if (cues != 95 || variants != 93) throw new InvalidDataException($"Partner playable coverage differs: cues={cues}, variants={variants}");
            Debug.Log($"Original partner playable coverage: actors=34, skills=75, variants={variants}, cues={cues}.");
            Destroy(target); Destroy(pipeline);
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
