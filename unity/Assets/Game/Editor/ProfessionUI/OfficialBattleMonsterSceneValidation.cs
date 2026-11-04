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
    public static class OfficialBattleMonsterSceneValidation
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

        static OfficialBattleMonsterSceneValidation() { EditorApplication.update += Update; }
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true);
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
                steps ??= Steps().GetEnumerator();
                if (!steps.MoveNext()) Finish(0);
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static IEnumerable<int> Steps()
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set the owned validation workspace.");
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
            var assets = BattleNativeMonsterAssets.Load();
            var mapper = new OfficialBattleGridMapper(root.transform, new FightBattleGrid { Width = 6, Height = 6 });
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
                        CheckFollower(ownNative, ownFollower);
                        samples++;
                    }
                    Require(model.IsAnimationFinished, "Nonloop action did not finish at its speed-adjusted duration.");
                    Require(!model.Play("__unbound_action__", false), "Unknown action was silently substituted.");
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
            Require(models == 18 && animations == 224 && samples == 1120, "Native monster coverage changed.");
            File.WriteAllText(Path.Combine(output, "shared-monster-result.json"),
                "{\"models\":" + models + ",\"animations\":" + animations + ",\"samples\":" + samples
                + ",\"shadowPixels\":" + changedPixels + ",\"failures\":[]}");
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
    }
}
