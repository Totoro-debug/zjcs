using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static partial class OfficialBattleMonsterSceneValidation
    {
        private const string Active = "Zjcs.BattleMonsterScene.Validation";
        private static IEnumerator<int> steps;
        private static OfficialBattleSceneView scene;
        private static OfficialBattleLiveMonsterModelView model;
        private static GameObject root;
        private static Camera camera;
        private static RenderTexture target;
        private static RenderPipelineAsset previousDefault, previousQuality;
        private static UniversalRenderPipelineAsset pipeline;
        private static Renderer2DData rendererData;
        [Serializable] private sealed class MovementSource { public int id; public OfficialBattleSkillPlayableCue[] playables; }
        [Serializable] private sealed class MovementSources { public MovementSource[] actions; }

        static OfficialBattleMonsterSceneValidation() { EditorApplication.update += Update; }
        public static void Run() => Begin(false);
        public static void RunNativeGraphs() => Begin(true);
        private static void Begin(bool nativeGraphs)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
            SessionState.SetBool(Active + ".NativeGraphs", nativeGraphs);
            SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorApplication.EnterPlaymode();
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0))
                    throw new TimeoutException("Shared monster scene validation timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                steps ??= (SessionState.GetBool(Active + ".NativeGraphs", false) ? NativeGraphSteps() : Steps()).GetEnumerator();
                if (!steps.MoveNext()) Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static IEnumerable<int> Steps()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set the owned validation workspace.");
            var mapper = InitializeRenderScene();
            var assets = BattleNativeMonsterAssets.Load();
            scene = new OfficialBattleSceneView(null, root.transform, null, mapper, null, null);
            int models = 0, animations = 0, samples = 0, changedPixels = 0;
            foreach (int id in assets.EntityIds.OrderBy(value => value))
            {
                var unit = new FightBattleUnitState { Id = id, MonsterId = 999999, MonsterTemplateId = id,
                    Hp = 100, MaxHp = 100, X = 1, Y = 1 };
                var checkpoint = new FightBattleCheckpoint(); checkpoint.Units.Add(unit);
                scene.Refresh(checkpoint); scene.Tick(.1f);
                Transform entity = root.transform.Find("BattleUnit_" + id);
                var native = entity ? entity.GetComponentInChildren<BattleNativeMonsterRenderer>() : null;
                Require(native && native.EntityRoot == entity, "Shared scene did not choose the full native monster: " + id);
                var follower = entity.GetComponentInChildren<OfficialBattleShadowBoneFollower>();
                Require(follower && follower.skeletonRenderer == native, "Shadow is not bound to its actual monster: " + id);
                Require(native.GetComponent<SortingGroup>().sortingOrder == 18000, "Current source model sorting differs.");
                var sprite = follower.GetComponentInChildren<SpriteRenderer>();
                Require(sprite && sprite.sprite && sprite.sharedMaterial && sprite.sortingOrder == 17990
                    && sprite.sortingLayerID == 0, "Original shadow graphics/sorting are missing.");
                foreach (string name in native.AnimationNames)
                {
                    // Cross the same public model seam used by all battle modes.
                    model = OfficialBattleLiveMonsterModelView.TryCreate(unit, root.transform);
                    if (!model.HasAnimation(name))
                    {
                        model.Dispose(); model = null;
                        yield return 0;
                        continue;
                    }
                    Require(model != null && model.Play(name, false, true), "Shared model rejected original action: " + name);
                    var ownNative = model.ModelTransform.GetComponent<BattleNativeMonsterRenderer>();
                    var ownFollower = model.Transform.GetComponentInChildren<OfficialBattleShadowBoneFollower>();
                    float duration = new ProfessionNativeAnimationChannels(ownNative.State.Source, name, originalBezierSegments: true).Duration;
                    model.SetPlaybackSpeed(2);
                    for (int sample = 0; sample <= 4; sample++)
                    {
                        model.Transform.position = new Vector3(sample * .3f, sample * -.2f, 0);
                        model.Transform.localScale = new Vector3(sample % 2 == 0 ? 1 : -1, 1, 1);
                        model.Tick(sample == 0 ? 0 : duration / 8);
                        if (name == "die_01") Require(ownNative.Animator.GetBool("die")
                            && ownNative.AnimatorFrame.Current.Animation?.Name == name,
                            "Original death parameter/state was released before its owner: " + id);
                        CheckFollower(ownNative, ownFollower);
                        samples++;
                    }
                    for (int tail = 0; tail < 60 && !model.IsAnimationFinished; tail++) model.Tick(1f / 60);
                    Require(model.IsAnimationFinished, "Original nonloop state did not finish or leave its original state.");
                    Require(!model.Play("__unbound_action__", false), "Unknown action was silently substituted.");
                    if (name == "die_01")
                    {
                        Require(model.Play("idle_01", true, true), "Revived monster did not accept its original idle.");
                        model.Tick(1f / 60);
                        Require(!ownNative.Animator.GetBool("die") && !model.IsAnimationFinished
                            && ownNative.AnimatorFrame.Current.Animation?.Name == "idle_01",
                            "Revival retained the original death parameter/state: " + id);
                    }
                    Transform ownedRoot = model.Transform;
                    model.Dispose(); model.Dispose(); model = null;
                    animations++;
                    // Destroy is deferred in Play Mode; do not accumulate native meshes.
                    yield return 0;
                    Require(!ownedRoot, "Shared model disposal leaked the monster/shadow root.");
                }
                unit.X = 3; unit.Y = 2; scene.Refresh(checkpoint); scene.Tick(.1f);
                Require(Vector3.Distance(entity.position, mapper.GridToWorld(new FightGridPoint(3, 2))) < .001f,
                    "Shared grid placement did not move the monster.");
                CheckFollower(native, follower);
                var bounds = native.GetComponent<MeshRenderer>().bounds; bounds.Encapsulate(sprite.bounds);
                camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.2f;
                yield return 0; yield return 0;
                Color32[] visible = Capture(Path.Combine(output, "shared-monster-" + id + ".png"));
                sprite.enabled = false;
                Color32[] hidden = Capture(null);
                sprite.enabled = true;
                int changed = visible.Zip(hidden, (a, b) => Math.Abs(a.r - b.r) > 2 || Math.Abs(a.g - b.g) > 2
                    || Math.Abs(a.b - b.b) > 2).Count(value => value);
                Require(changed > 30, "Actual monster shadow rendered no visible pixels: " + id + " / " + changed);
                changedPixels += changed;
                scene.Refresh(new FightBattleCheckpoint());
                yield return 0;
                Require(!entity, "Removing a shared scene unit leaked its native model/shadow.");
                Debug.Log("[Shared native monster] " + id + " shadowPixels=" + changed);
                models++;
            }
            Require(OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState
                { CharacterType = 1, MonsterTemplateId = 3198 }, root.transform) == null, "Role-player guard became a monster.");
            Require(OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState
                { MonsterId = 3198, MonsterTemplateId = 999999 }, root.transform) == null, "Stat row overrode an explicit entity template.");
            int skills = 0;
            foreach (int step in SkillSteps(assets)) { if (step == 1) skills++; yield return 0; }
            Require(models == 20 && animations == 222 && samples == 1110 && skills == 10, "Native monster Animator coverage changed.");
            File.WriteAllText(Path.Combine(output, "shared-monster-result.json"),
                "{\"models\":" + models + ",\"animations\":" + animations + ",\"samples\":" + samples
                + ",\"shadowPixels\":" + changedPixels + ",\"skillCarriers\":" + skills + ",\"failures\":[]}");
            Debug.Log("[Shared monster scene validation] passed: " + models + " models / " + animations + " actions / " + samples + " follower samples.");
        }
        private static void CheckFollower(BattleNativeMonsterRenderer native, OfficialBattleShadowBoneFollower follower)
        {
            var bone = ((IProfessionFollowerRenderer)native).Skeleton.FindBone(follower.boneName);
            Vector3 expected = native.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0));
            Vector3 actual = follower.transform.position; expected.z = actual.z;
            Require(Vector3.Distance(expected, actual) < .001f, "Original shadow diverged from its posed bone.");
            Require(Vector3.Distance(follower.transform.localScale, new Vector3(bone.ScaleX, bone.ScaleY, 1)) < .001f,
                "Original shadow lost bone local scale.");
            if (native.Attachments != null && Convert.ToInt32(native.Attachments.OriginalRow["entityId"]) == 3207)
                foreach (string name in new[] { "bone29", "bone32" })
                {
                    var attachmentBone = ((IProfessionFollowerRenderer)native).Skeleton.FindBone(name);
                    Transform attachment = native.Attachments.Agent.GetBone(name);
                    Vector3 point = native.transform.TransformPoint(new Vector3(attachmentBone.WorldX, attachmentBone.WorldY, 0));
                    point.z = attachment.position.z;
                    Require(Vector3.Distance(attachment.position, point) < .001f,
                        "The dungeon monster's skill attachment left its live bone: " + name);
                    Vector3 tangent = native.transform.TransformVector(new Vector3(attachmentBone.A, attachmentBone.C, 0));
                    Require(Vector3.Dot(attachment.TransformVector(Vector3.right).normalized, tangent.normalized) > .99999f,
                        "The dungeon monster's skill attachment lost its live rotation: " + name);
                }
        }
        private static IEnumerable<int> SkillSteps(BattleNativeMonsterAssets assets)
        {
            foreach (int id in assets.EntityIds.OrderBy(value => value))
            {
                var row = assets.Find(id);
                var original = assets.Resolve<RuntimeAnimatorController>((string)row["nativeController"]);
                if (!original.animationClips.Any(value => value.name == "shoot")) continue;
                AnimationClip clip = original.animationClips.First(value => value.name.StartsWith("skill_", StringComparison.Ordinal));
                model = OfficialBattleLiveMonsterModelView.TryCreate(new FightBattleUnitState
                    { Id = id, MonsterTemplateId = id, Hp = 100, MaxHp = 100 }, root.transform);
                var native = model.ModelTransform.GetComponent<BattleNativeMonsterRenderer>();
                var cue = new OfficialBattleSkillPlayableCue { kind = 1, origin_clip_name = "shoot",
                    trigger_param = "shoot", animation = clip.name, speed = 1, my_duration = .25f, end_resume_param = true };
                Require(model.PlaySkill(cue, clip, .25f), "Original monster override playable was rejected: " + id);
                bool seen = false;
                for (int frame = 0; frame < 12; frame++)
                {
                    model.Tick(1f / 60);
                    CheckFollower(native, model.Transform.GetComponentInChildren<OfficialBattleShadowBoneFollower>());
                    if (native.AnimatorFrame.Current.Clip == clip && native.AnimatorFrame.Current.Animation.Name == clip.name) seen = true;
                }
                Require(seen, "Original shoot carrier never consumed its actual skill override: " + id);
                var overrides = native.Animator.runtimeAnimatorController as AnimatorOverrideController;
                Require(overrides && overrides["shoot"] == clip, "Original skill did not bind the exact native clip.");
                model.Tick(.1f);
                // The source override persists after playable end; it is released
                // with the owning controller, not reset to a nonexistent pose.
                Require(overrides["shoot"] == clip, "Playable end changed the original persistent override.");
                Require(model.Play("idle_01", true, true), "Original monster did not return to its own idle.");
                Require(!model.PlaySkill(cue, clip, 0), "An ended skill owner restarted a playable.");
                Transform owned = model.Transform;
                model.Dispose(); model.Dispose(); model = null;
                yield return 1;
                Require(!owned && !overrides && original, "Original monster skill disposal leaked an owned controller/root.");
                Debug.Log("[Shared monster skill carrier] " + id + " original shoot -> " + clip.name);
            }
            // Exercise the production scene's public event route, including a
            // parameter-only cue that the sampled native-model route skipped.
            var record = OfficialBattleSkillPlayableCatalog.LoadDefault().Data.skills.First(value => value.playables.Count == 1
                && value.playables[0].kind == 0 && value.playables[0].trigger_param == "skill_01"
                && value.playables[0].pre_state_name_allow_list.Count == 0);
            var checkpoint = new FightBattleCheckpoint();
            checkpoint.Units.Add(new FightBattleUnitState { Id = 3904, MonsterTemplateId = 3904,
                Hp = 100, MaxHp = 100, X = 2, Y = 2 });
            scene.Refresh(checkpoint);
            var nativeScene = root.transform.Find("BattleUnit_3904").GetComponentInChildren<BattleNativeMonsterRenderer>();
            var cast = new FightBattleStepEvent { ActorId = 3904 };
            cast.SkillCasts.Add(new FightBattleSkillCastEvent { ActorId = 3904, SkillEntityId = record.skill_entity_id });
            scene.Refresh(checkpoint, cast);
            bool dispatched = false;
            for (int frame = 0; frame < 12; frame++)
            {
                scene.Tick(1f / 60);
                if (nativeScene.AnimatorFrame.Current.Animation?.Name == "skill_01") dispatched = true;
                yield return 0;
            }
            Require(dispatched, "Shared scene ignored an original parameter-only skill cue.");
            var moves = JsonUtility.FromJson<MovementSources>(Resources.Load<TextAsset>("Combat/move-animators").text);
            var action = moves.actions.First(value => value.playables.Any(cue => cue.my_bool_param == "run"
                && cue.my_bool_value && cue.end_resume_param));
            var unit = checkpoint.Units[0]; unit.X = 1; unit.Y = 1; scene.Refresh(checkpoint);
            unit.X = 3;
            var move = new FightBattleStepEvent { ActorId = unit.Id };
            move.Movements.Add(new FightBattleMovementEvent { UnitId = unit.Id, MoveBehaviourId = action.id,
                MoveDuration = .25f, Path = new List<FightGridPoint> { new FightGridPoint(1, 1),
                    new FightGridPoint(2, 1), new FightGridPoint(3, 1) } });
            scene.Refresh(checkpoint, move);
            bool ran = false;
            for (int frame = 0; frame < 40; frame++)
            {
                scene.Tick(1f / 60);
                if (nativeScene.Animator.GetBool("run") && nativeScene.AnimatorFrame.Current.Animation?.Name == "run_01") ran = true;
                CheckFollower(nativeScene, nativeScene.EntityRoot.GetComponentInChildren<OfficialBattleShadowBoneFollower>());
                yield return 0;
            }
            Require(ran && !nativeScene.Animator.GetBool("run"), "Original per-cell movement owner did not start/resume its run parameter.");
            scene.Refresh(new FightBattleCheckpoint());
            yield return 0;
            Require(!nativeScene, "Shared scene leaked its parameter-driven skill unit.");
            Debug.Log("[Shared monster skill event] consumed original cue " + record.skill_entity_id);
        }
        private static Color32[] Capture(string file)
        {
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                if (file != null) File.WriteAllBytes(file, pixels.EncodeToPNG());
                return pixels.GetPixels32();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels); }
        }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            nativeGraphPlayback?.Dispose(); nativeGraphPlayback = null;
            model?.Dispose(); scene?.Dispose();
            if (camera) camera.targetTexture = null;
            if (root) UnityEngine.Object.Destroy(root);
            if (target) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = previousDefault; QualitySettings.renderPipeline = previousQuality;
                UnityEngine.Object.Destroy(pipeline); UnityEngine.Object.Destroy(rendererData);
            }
            EditorApplication.Exit(code);
        }

        private static OfficialBattleGridMapper InitializeRenderScene()
        {
            previousDefault = GraphicsSettings.defaultRenderPipeline; previousQuality = QualitySettings.renderPipeline;
            rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            root = new GameObject("Shared original monster scene");
            var light = new GameObject("Validation light").AddComponent<Light2D>();
            light.transform.SetParent(root.transform, false);
            light.lightType = Light2D.LightType.Global; light.intensity = 1;
            target = new RenderTexture(450, 800, 24); target.Create();
            camera = new GameObject("Diagnostic monster and shadow camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.65f, .7f, .75f); camera.targetTexture = target;
            return new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 6, Height = 6 });
        }
    }
}
