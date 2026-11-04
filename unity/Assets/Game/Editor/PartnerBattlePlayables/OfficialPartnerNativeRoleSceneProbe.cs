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
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class OfficialPartnerNativeRoleSceneProbe
    {
        private const string Active = "Zjcs.PartnerNativeRoleSceneProbe";
        static OfficialPartnerNativeRoleSceneProbe()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    new GameObject("Original partner role scene probe").AddComponent<PartnerNativeRoleSceneProbeRunner>();
            };
        }
        public static void Run()
        {
            var directory = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(directory) || !File.Exists(Path.Combine(directory, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use an owned partner scene validation directory.");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            OfficialPartnerNativeSkillGraphProbe.ImportEmbeddedTextures();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
        }
        internal static void Finish(Exception error)
        {
            SessionState.SetBool(Active, false);
            if (error != null) Debug.LogException(error);
            else Debug.Log("PARTNER_NATIVE_ROLE_SCENE_PASSED: shared scene creation, original shadows, movement, death/revival and restored death. Invitations, full unit HUD and Windows Player are not validated by this probe.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }

    public sealed class PartnerNativeRoleSceneProbeRunner : MonoBehaviour
    {
        private int errors;
        private RenderTexture target;
        private Camera cameraView;
        private IEnumerator Start()
        {
            Application.logMessageReceived += (_, __, type) => {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            };
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { OfficialPartnerNativeRoleSceneProbe.Finish(error); yield break; }
                yield return current;
            }
            OfficialPartnerNativeRoleSceneProbe.Finish(errors == 0 ? null : new InvalidDataException("Partner role scene errors: " + errors));
        }
        private IEnumerator Check()
        {
            var pipeline = UniversalRenderPipelineAsset.Create(ScriptableObject.CreateInstance<Renderer2DData>());
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
            cameraView = new GameObject("Partner scene validation camera", typeof(Camera)).GetComponent<Camera>();
            cameraView.orthographic = true; cameraView.orthographicSize = 3f;
            cameraView.transform.position = new Vector3(4.5f, 4.5f, -10);
            cameraView.backgroundColor = new Color(.15f, .18f, .2f); cameraView.clearFlags = CameraClearFlags.SolidColor;
            target = new RenderTexture(768, 1365, 24); cameraView.targetTexture = target;
            var light = new GameObject("Partner scene validation light", typeof(Light2D)).GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = 1; light.color = Color.white;
            var assets = PartnerNativeBattleAssetCatalog.Load();
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            var models = OfficialBattleModelCatalog.LoadDefault();
            var npcIds = assets.NpcIds.Where(OfficialBattlePartnerNativeModelView.HasBinding).OrderBy(id => id).ToArray();
            Require(npcIds.Length == 26, "The confirmed open native partner range differs.");
            Require(Enumerable.Range(11015, 8).All(id => !OfficialBattlePartnerNativeModelView.HasBinding(id)), "Closed collaboration partners became available.");
            foreach (int npcId in npcIds)
            {
                int entityId = Convert.ToInt32(assets.Find(npcId)["entityId"]);
                var root = new GameObject("Partner shared scene " + npcId);
                var checkpoint = new FightBattleCheckpoint { Grid = new FightBattleGrid { Width = 10, Height = 10 } };
                var state = new FightBattleUnitState { Id = 1, NpcClassId = npcId, MonsterId = entityId,
                    Hp = 100, MaxHp = 100, X = 4, Y = 4 };
                combat.BindNpcRoleEntity(state, entityId);
                Require(state.RoleEntityId == entityId && state.CharacterType == 4,
                    "Original NPC role entity/type was not retained: " + npcId);
                Require(OfficialBattleUnitWorldHudView.ResolveUnitBarEntityId(state) == entityId,
                    "The partner HUD selected the protagonist entity: " + npcId);
                checkpoint.Units.Add(state);
                var mapper = new OfficialBattleGridMapper(root.transform, checkpoint.Grid);
                using (var scope = new OfflineBattleNativePlayableScope(root.transform, null))
                using (var scene = new OfficialBattleSceneView(models, root.transform, null, mapper, null, null,
                    nativePlayableScope: scope))
                {
                    scene.Refresh(checkpoint);
                    Transform body = Unit(scene, 1);
                    Require(body, "The shared scene did not create partner " + npcId);
                    var controller = body.GetComponent<AnimationController>();
                    Require(controller && controller.myInited && controller.myAnimator,
                        "The original entity animation controller is missing: " + npcId);
                    Animator animator = controller.myAnimator;
                    for (int frame = 0; frame < 40; frame++) { Tick(scope, scene); yield return null; }
                    Require(body.GetComponentsInChildren<MeshFilter>().Any(mesh => mesh.sharedMesh && mesh.sharedMesh.vertexCount > 0),
                        "The native partner mesh is empty: " + npcId);
                    Require(body.GetComponentsInChildren<SpriteRenderer>().Any(sprite => sprite.sprite && sprite.name == "Shadow"),
                        "The original partner shadow is missing: " + npcId);
                    Capture(npcId + "-idle");

                    var movement = combat.CreateRoleMovement(state, false);
                    Require(movement.MoveSkillEntityId > 0 && movement.MoveBehaviourId > 0,
                        "Original NPC movement identity is missing: " + npcId);
                    movement.Path = new List<FightGridPoint> { new FightGridPoint(4, 4), new FightGridPoint(5, 4) };
                    state.X = 5;
                    scene.Refresh(checkpoint, new FightBattleStepEvent { Movements = new List<FightBattleMovementEvent> { movement } });
                    bool sawRun = false;
                    for (int frame = 0; frame < 65; frame++)
                    {
                        Tick(scope, scene);
                        sawRun |= animator.GetCurrentAnimatorClipInfo(0).Concat(animator.GetNextAnimatorClipInfo(0))
                            .Any(clip => clip.weight > .0001f && clip.clip.name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (frame == 12) Capture(npcId + "-move");
                        yield return null;
                    }
                    Require(sawRun, "Original movement playable never entered Run: " + npcId);
                    Require(Vector3.Distance(body.position, mapper.GridToWorld(new FightGridPoint(5, 4))) < .001f,
                        "Shared scene movement did not reach its settled cell: " + npcId);
                    if (movement.MoveBehaviourId == 15321)
                        Require(!animator.GetBool("run_03"), "Original movement owner did not restore its bool parameter: " + npcId);
                    state.Hp = 0; scene.Refresh(checkpoint);
                    Require(RoleLeaf(scope, body, "die"), "Original normal death sequence was not started: " + npcId);
                    bool hasDeathParameter = animator.parameters.Any(parameter => parameter.name == "die");
                    Require(hasDeathParameter ? animator.GetBool("die") : npcId == 11008,
                        "Original normal death playable did not retain its controller contract: " + npcId);
                    for (int frame = 0; frame < 70; frame++) { Tick(scope, scene); yield return null; }
                    Require(Unit(scene, 1) == body, "An indefinite original death view was removed: " + npcId);
                    Capture(npcId + "-death");
                    state.Hp = 100; scene.Refresh(checkpoint);
                    if (hasDeathParameter) Require(!animator.GetBool("die"), "Revival did not restore the original death parameter: " + npcId);
                    for (int frame = 0; frame < 35; frame++) { Tick(scope, scene); yield return null; }
                }
                Destroy(root); yield return null;

                root = new GameObject("Partner restored death " + npcId); state.Hp = 0;
                using (var scope = new OfflineBattleNativePlayableScope(root.transform, null))
                using (var scene = new OfficialBattleSceneView(models, root.transform, null,
                    new OfficialBattleGridMapper(root.transform, checkpoint.Grid), null, null,
                    nativePlayableScope: scope, restoringNativeUnits: true))
                {
                    scene.Refresh(checkpoint);
                    var controller = Unit(scene, 1)?.GetComponent<AnimationController>();
                    Require(controller && RoleLeaf(scope, Unit(scene, 1), "immediately_die"),
                        "Restored dead partner did not start original on-start death: " + npcId);
                    Require(controller.myAnimator.parameters.Any(parameter => parameter.name == "immediately_die")
                        ? controller.myAnimator.GetBool("immediately_die") : npcId == 11008,
                        "Restored death changed the original controller parameter contract: " + npcId);
                    Require(Vector3.Distance(Unit(scene, 1).position,
                        new Vector3(checkpoint.Grid.OriginX + state.X, checkpoint.Grid.OriginY + state.Y, checkpoint.Grid.OriginZ)) < .001f,
                        "Restored dead partner did not retain its settled grid position: " + npcId);
                    for (int frame = 0; frame < 10; frame++) { Tick(scope, scene); yield return null; }
                }
                Destroy(root); yield return null;
                Debug.Log("PARTNER_NATIVE_ROLE_SCENE_CHECKED " + npcId);
            }
        }
        private static void Tick(OfflineBattleNativePlayableScope scope, OfficialBattleSceneView scene)
        { scope.Tick(1f / 60); scene.Tick(1f / 60); scope.LateTick(); }
        private static bool RoleLeaf(OfflineBattleNativePlayableScope scope, Transform body, string parameter)
            => scope.PlayableSlot.GetComponentsInChildren<Zjcs.Official.StoryAnimation.MyPlayableAnimator>(true)
                .Any(leaf => leaf.myBoolParam == parameter && leaf.mySource == body.gameObject
                    && leaf.myStatus == Zjcs.Official.StoryPlayables.MyPlayable.PlayableStatus.Play);
        private static Transform Unit(OfficialBattleSceneView scene, int id) => (Transform)typeof(OfficialBattleSceneView)
            .GetMethod("Body", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(scene, new object[] { id });
        private void Capture(string name)
        {
            cameraView.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"), image.EncodeToPNG());
            Destroy(image); RenderTexture.active = previous;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
