using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryGroundEffects;
using Zjcs.Official.StoryPlayables;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleMovementEffectAcceptance
    {
        private const string Key = "Zjcs.Battle.MovementEffectAcceptance";
        private static bool started;
        private static int errors;
        [Serializable] private sealed class Data { public Entry[] skills, actions; }
        [Serializable] private sealed class Entry { public int id, followNodes; public string resource; }
        public static void Run()
        {
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            Start();
        }
        public static void RunIsolated() => Start();
        private static void Start()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Movement effect verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Movement effects verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps = Steps();
            try
            {
                while (true)
                {
                    object next;
                    try { if (!steps.MoveNext()) break; next = steps.Current; }
                    catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; yield break; }
                    yield return next;
                }
                if (errors == 0)
                {
                    SessionState.SetInt(Key + ".result", 0);
                    Debug.Log("Original movement effects Play Mode passed: 10 native graphs/17 reachable follow nodes, original sequence cue times, native particles/materials/shaders, original terrain selection, local grid adapter, per-cell ownership and cleanup.");
                }
                EditorApplication.isPlaying = false;
            }
            finally { (steps as IDisposable)?.Dispose(); }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Movement effect: " + message); }
        private static IEnumerator Steps()
        {
            var pipeline = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
            Require(pipeline != null, "original battle render pipeline binding");
            using var pipelineScope = pipeline.Enter();
            var world = new GameObject("Movement effects world"); world.transform.position = new Vector3(3, 4, 0);
            var source = new GameObject("Official entity"); source.transform.SetParent(world.transform, false);
            source.transform.localPosition = new Vector3(2, 2, 0);
            var body = new GameObject("body"); body.transform.SetParent(source.transform, false);
            var view = new GameObject("Movement effects output camera").AddComponent<Camera>();
            view.transform.SetParent(world.transform, false);
            view.transform.position = source.transform.position + new Vector3(0, 0, -20);
            view.orthographic = true; view.orthographicSize = 4;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.black;
            var grid = new FightBattleGrid { Width = 8, Height = 8, DefaultGroundType = 0 };
            var mapper = new OfficialBattleGridMapper(world.transform, grid);
            Require(mapper.GetGroundType(source.transform.localPosition) == GroundType.None, "local-position ground lookup");
            Require(mapper.GetGroundType(new Vector3(30, 30, 0)) == GroundType.Block, "original missing-cell fallback");
            var library = new OfficialBattleGroundEffectLibrary(() => mapper);
            var data = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("Combat/movement-effects").text);
            var entries = new List<Entry>(data.skills); entries.AddRange(data.actions);
            Require(entries.Count == 10, "reachable graph count");
            int follows = 0, nativeParticles = 0;
            foreach (var entry in entries)
            {
                var prefab = Resources.Load<GameObject>(entry.resource); Require(prefab != null, "native graph " + entry.id);
                var instance = UnityEngine.Object.Instantiate(prefab, world.transform, false);
                var center = new MyPlayableCenter();
                var sequence = instance.GetComponent<MyPlayableSequence>(); Require(sequence != null, "root sequence " + entry.id);
                var nodes = instance.GetComponentsInChildren<MyPlayableFollowBone>(true);
                Require(nodes.Length == entry.followNodes, "native serialized follow bindings " + entry.id); follows += nodes.Length;
                var starts = new List<float>();
                foreach (var node in nodes) node.OnPlay += _ => starts.Add(sequence.Time);
                foreach (var playable in instance.GetComponentsInChildren<MyPlayable>(true)) playable.MyPlayableCenter = center;
                foreach (var apply in instance.GetComponentsInChildren<ApplyByGroundType>(true)) apply.Host = library;
                // Same attachment contract as the shared scene view, without
                // touching any actor/model scale or transform.
                var agent = source.GetComponent<Zjcs.Official.StoryAttachment.AttachAgent>();
                if (agent == null)
                {
                    agent = source.AddComponent<Zjcs.Official.StoryAttachment.AttachAgent>();
                    agent.myBoneNameList.Add("body"); agent.myBoneList.Add(body.transform); agent.Init();
                }
                sequence.Init(source, source); sequence.TryPlay(); yield return null;
                for (int frame = 0; frame < 120; frame++)
                {
                    center.Update(.05f); center.LateUpdate();
                    if (frame == 5)
                    {
                        foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
                            if (particle.gameObject.activeInHierarchy) particle.Simulate(.15f, false, true, true);
                        foreach (var renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                            foreach (var material in renderer.sharedMaterials)
                                if (material != null) Require(material.shader != null && material.shader.isSupported, "Windows shader " + material.name);
                        if (entry.id == 19830 || entry.id == 19850) Capture(source.transform.position, entry.id.ToString());
                    }
                    yield return null;
                }
                Require(starts.Count == nodes.Length, "all original effect cues execute " + entry.id);
                foreach (var node in nodes)
                    Require(node.myApplyComponent != null, "original inline/terrain applicator binding " + entry.id);
                if (entry.id == 19830)
                {
                    Require(Mathf.Abs(starts[0]) < .001f && Mathf.Abs(starts[1] - 2.03f) <= .051f
                        && Mathf.Abs(starts[2] - 3.35f) <= .051f, "original 19830 sequence times");
                }
                nativeParticles += prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
                sequence.Remove(); UnityEngine.Object.Destroy(instance); center.RemoveAll(); yield return null;
            }
            Require(follows == 17 && nativeParticles > 0, "native effect closure totals");
            // A delayed two-cell action must stop/cancel with its unit, while
            // another battle's effects remain alive.
            body.name = "Mapped body attachment";
            var effects = new OfficialBattleMovementEffects(world.transform, library);
            var other = new OfficialBattleMovementEffects(world.transform, library);
            var step = new FightBattleStepEvent();
            step.Movements.Add(new FightBattleMovementEvent { UnitId = 1, MoveBehaviourId = 15321,
                Delay = .2f, MoveDuration = .5f, Path = new List<FightGridPoint> {
                    new FightGridPoint(2, 2), new FightGridPoint(3, 2), new FightGridPoint(4, 2) } });
            effects.Schedule(1, source.transform, step, mapper.GridToWorld);
            other.Schedule(1, source.transform, step, mapper.GridToWorld);
            Require(world.GetComponentsInChildren<MyPlayableSequence>(true).Length == 0, "movement delay precedes graph creation");
            yield return null; effects.Tick(.25f); other.Tick(.25f);
            Require(world.GetComponentsInChildren<MyPlayableSequence>(true).Length == 2, "independent hosts each start the first cell");
            effects.RemoveUnit(1); yield return null;
            var first = world.GetComponentsInChildren<MyPlayableSequence>(true);
            Require(first.Length == 1, "unit removal leaves the other host alive");
            Require(first[0].Time == 0f, "original creation frame does not advance the graph");
            int firstInstance = first[0].GetInstanceID();
            int due = 0, afterEnd = 0;
            AddTimingLeaf(first[0], .3f, () => due++);
            AddTimingLeaf(first[0], .8f, () => afterEnd++);
            other.Tick(.49f); yield return null;
            Require(due == 1 && afterEnd == 0,
                "the old cell's final interval executes due leaves and cancels leaves beyond its end");
            var second = world.GetComponentsInChildren<MyPlayableSequence>(true);
            Require(second.Length == 1 && second[0].GetInstanceID() != firstInstance,
                "cell boundary stops the first graph and starts a distinct second graph");
            other.Tick(.5f); yield return null;
            Require(world.GetComponentsInChildren<MyPlayableSequence>(true).Length == 0, "last cell releases its graph");
            step.Movements[0].Delay = 0f;
            step.Movements[0].Path.Add(new FightGridPoint(5, 2));
            effects.Schedule(1, source.transform, step, mapper.GridToWorld);
            var largeFrame = world.GetComponentsInChildren<MyPlayableSequence>(true);
            Require(largeFrame.Length == 1, "large update starts from one active cell");
            int largeDue = 0, largeAfterEnd = 0;
            AddTimingLeaf(largeFrame[0], .3f, () => largeDue++);
            AddTimingLeaf(largeFrame[0], .8f, () => largeAfterEnd++);
            yield return null;
            effects.Tick(1.25f); yield return null;
            Require(largeDue == 1 && largeAfterEnd == 0
                && world.GetComponentsInChildren<MyPlayableSequence>(true).Length == 1,
                "one large update traverses two cell boundaries and retains only the third cell");
            effects.Tick(.25f); yield return null;
            var finalCell = world.GetComponentsInChildren<MyPlayableSequence>(true);
            string remaining = "";
            foreach (var sequence in finalCell)
                remaining += " " + sequence.name + ":" + sequence.myStatus + ":" + sequence.Time;
            Require(finalCell.Length == 0,
                "the exact final deadline releases the third cell; remaining=" + remaining);
            effects.Dispose(); other.Dispose();
            var sceneRoot = new GameObject("Movement scene snapshot fixture");
            sceneRoot.transform.SetParent(world.transform, false);
            var sceneMapper = new OfficialBattleGridMapper(sceneRoot.transform, grid);
            var model = new GameObject("Movement scene attached model");
            using (var scene = new OfficialBattleSceneView(OfficialBattleModelCatalog.LoadDefault(),
                sceneRoot.transform, _ => null, sceneMapper, null, (target, state) =>
                {
                    var scale = target.localScale;
                    scale.x = Mathf.Abs(scale.x) * (state.HasFlipX && state.FlipX ? -1f : 1f);
                    target.localScale = scale;
                }))
            {
                var unit = new FightBattleUnitState { Id = 1, CharacterType = 1,
                    X = 2, Y = 2, Hp = 100, MaxHp = 100 };
                var checkpoint = new FightBattleCheckpoint { Grid = grid };
                checkpoint.Units.Add(unit);
                scene.Refresh(checkpoint);
                scene.AttachUnitTransform(unit.Id, model.transform);
                var movement = new FightBattleStepEvent { ActorId = unit.Id };
                movement.Movements.Add(new FightBattleMovementEvent { UnitId = unit.Id,
                    MoveBehaviourId = 15321, Delay = .2f, MoveDuration = .5f,
                    Path = new List<FightGridPoint> { new FightGridPoint(2, 2),
                        new FightGridPoint(3, 2), new FightGridPoint(4, 2), new FightGridPoint(5, 2) } });
                unit.X = 5;
                scene.Refresh(checkpoint, movement);
                scene.Tick(.25f); yield return null;
                var graphs = sceneRoot.GetComponentsInChildren<MyPlayableSequence>(true);
                Require(graphs.Length == 1, "production scene creates the first movement graph after its delay");
                Vector3 cueSource = new Vector3(float.NaN, 0, 0);
                AddTimingLeaf(graphs[0], .3f, () => cueSource = graphs[0].mySource.transform.position);
                scene.Tick(1f); yield return null;
                Require((cueSource - sceneMapper.GridToWorld(new FightGridPoint(3, 2))).sqrMagnitude < 1e-8f,
                    "a first-cell cue samples that cell's landing instead of the large update's final actor position");
                var follow = sceneRoot.GetComponentInChildren<MyPlayableFollowBone>(true);
                Require(follow != null && follow.myApplyComponent != null, "production scene retains the third cell's native effect");
                Vector3 expectedFollow = follow.myAttach.GetBone(follow).TransformPoint(follow.myAttach.myOffset);
                Require((follow.myApplyComponent.transform.position - expectedFollow).sqrMagnitude < 1e-8f,
                    "native effect follows the actor after the remaining partial cell interval");
                Capture(scene.TryGetUnitTransform(unit.Id).position, "scene-movement-native-follow");
                scene.Tick(.5f); yield return null;
                Require((scene.TryGetUnitTransform(unit.Id).position
                    - sceneMapper.GridToWorld(new FightGridPoint(5, 2))).sqrMagnitude < 1e-8f,
                    "production movement and effect clocks share the exact last landing");
                Require(sceneRoot.GetComponentsInChildren<MyPlayableSequence>(true).Length == 0,
                    "production scene releases the final cell graph");
                var reverse = new FightBattleStepEvent { ActorId = unit.Id };
                reverse.Movements.Add(new FightBattleMovementEvent { UnitId = unit.Id,
                    MoveBehaviourId = 15321, MoveDuration = .5f, HasFacing = true, EndFlipX = false,
                    Path = new List<FightGridPoint> { new FightGridPoint(5, 2),
                        new FightGridPoint(4, 2), new FightGridPoint(3, 2) } });
                unit.X = 3; unit.HasFlipX = true; unit.FlipX = false;
                scene.Refresh(checkpoint, reverse);
                Transform flipTarget = model.transform.parent;
                Require(flipTarget.localScale.x < 0f,
                    "the first cell faces travel before its original graph is created, without waiting for Tick");
                scene.Tick(0f);
                scene.Refresh(checkpoint);
                Require(flipTarget.localScale.x < 0f, "paused refresh preserves the current movement facing");
                scene.Tick(.5f);
                Require(flipTarget.localScale.x > 0f, "the last cell restores authoritative facing at its start");
                scene.Tick(.5f); yield return null;
                reverse.Movements[0].Delay = .25f;
                scene.Refresh(checkpoint, reverse);
                Require(flipTarget.localScale.x > 0f, "delayed movement retains authoritative facing before action start");
                scene.Tick(.25f);
                Require(flipTarget.localScale.x < 0f, "delayed first-cell graph starts after the original turn");
                scene.Tick(1f); yield return null;
                Require(flipTarget.localScale.x > 0f && Mathf.Abs(flipTarget.localScale.x - .3f) < 1e-7f,
                    "movement completion preserves the original visual scale magnitude");
            }
            UnityEngine.Object.Destroy(model); UnityEngine.Object.Destroy(sceneRoot); yield return null;
            var monsterRoot = new GameObject("Original monster movement fixture");
            monsterRoot.transform.SetParent(world.transform, false);
            var monsterMapper = new OfficialBattleGridMapper(monsterRoot.transform, grid);
            using (var scope = new Zjcs.Offline.BattleNativePlayables.OfflineBattleNativePlayableScope(monsterRoot.transform, null))
            using (var scene = new OfficialBattleSceneView(null, monsterRoot.transform, null, monsterMapper,
                null, (target, state) =>
                {
                    var scale = target.localScale;
                    scale.x = Mathf.Abs(scale.x) * (state.HasFlipX && state.FlipX ? -1f : 1f);
                    target.localScale = scale;
                }, nativePlayableScope: scope))
            {
                var monster = new FightBattleUnitState { Id = 2, MonsterId = 3206, MonsterTemplateId = 3206,
                    CharacterType = 2, X = 2, Y = 2, Hp = 100, MaxHp = 100 };
                var checkpoint = new FightBattleCheckpoint { Grid = grid };
                checkpoint.Units.Add(monster); scene.Refresh(checkpoint);
                var entity = scene.TryGetUnitTransform(monster.Id);
                var native = entity.GetComponentInChildren<Zjcs.Offline.ProfessionUI.BattleNativeMonsterRenderer>();
                Require(native != null && native.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0,
                    "original 3206 monster uses its native skeleton, atlas and live mesh");
                Require(!(native.Animator.runtimeAnimatorController is AnimatorOverrideController),
                    "monster movement retains its original application controller");
                var rules = OfficialCombatAssetCatalog.LoadDefault().Load();
                var movement = rules.CreateRoleMovement(monster, false);
                Require(movement.MoveBehaviourId == 19811 && movement.MoveDuration < 0f
                    && Mathf.Approximately(movement.MoveSpeed, 5f), "original 3206 movement configuration");
                movement.Path.AddRange(new[] { new FightGridPoint(2, 2),
                    new FightGridPoint(3, 2), new FightGridPoint(4, 2) });
                movement.Delay = .25f; rules.ApplyMovementFacing(monster, movement);
                var monsterStep = new FightBattleStepEvent { ActorId = monster.Id }; monsterStep.Movements.Add(movement);
                monster.X = 4; scene.Refresh(checkpoint, monsterStep);
                bool ran = false, followed = false;
                for (int frame = 0; frame < 90; frame++)
                {
                    scope.Tick(1f / 60); scene.Tick(1f / 60); scope.LateTick();
                    ran |= native.Animator.GetBool("run") && native.AnimatorFrame.Current.Animation?.Name == "run_01";
                    foreach (var follow in monsterRoot.GetComponentsInChildren<MyPlayableFollowBone>(true))
                        if (follow.myStatus != MyPlayable.PlayableStatus.Stop && follow.myApplyComponent != null)
                        {
                            Vector3 expected = follow.myAttach.GetBone(follow).TransformPoint(follow.myAttach.myOffset);
                            Require((follow.myApplyComponent.transform.position - expected).sqrMagnitude < 1e-7f,
                                "original monster effect follows its actual body attachment");
                            followed = true;
                        }
                    if (frame == 20 || frame == 40) Capture(entity.position, "monster-3206-movement-" + frame);
                    yield return null;
                }
                Require(ran && followed, "actual monster movement drives the original run animation and native effect");
                Require((entity.position - monsterMapper.GridToWorld(new FightGridPoint(4, 2))).sqrMagnitude < 1e-7f
                    && !native.Animator.GetBool("run"), "original monster lands and releases its run state");
            }
            UnityEngine.Object.Destroy(monsterRoot); yield return null;
            var fades = FadeSequenceSteps(world.transform);
            while (fades.MoveNext()) yield return fades.Current;
            UnityEngine.Object.Destroy(world); yield return null;
        }
        private static void AddTimingLeaf(MyPlayableSequence sequence, float delay, Action onPlay)
        {
            var go = new GameObject("Movement timing fixture leaf");
            go.transform.SetParent(sequence.transform, false);
            var leaf = go.AddComponent<MovementTimingLeaf>();
            leaf.MyPlayableCenter = sequence.MyPlayableCenter;
            leaf.myTriggerList = new List<MyPlayableTriggerTimeCfg>();
            leaf.myFadeoutTrigerList = new List<MyPlayableTrigger>();
            leaf.myEndTrigerList = new List<MyPlayableTrigger>();
            leaf.Init(sequence.mySource, sequence.myTarget);
            leaf.OnPlay += _ => onPlay();
            sequence.myPlayables.Add(new MyPlayableSequence.MyPlayableTimeCfg { myTime = delay, myPlayable = leaf });
        }
        public sealed class MovementTimingLeaf : MyPlayable
        {
            public override float GetDuration() => -1f;
        }
        private static void Capture(Vector3 position, string name)
        {
            var go = new GameObject("Movement effect fixture camera"); var camera = go.AddComponent<Camera>();
            camera.transform.position = position + new Vector3(0, 0, -20);
            camera.orthographic = true; camera.orthographicSize = 4;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var rt = RenderTexture.GetTemporary(450, 800, 24); camera.targetTexture = rt;
            var previous = RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active = rt;
                var image = new Texture2D(450, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 450, 800), 0, 0); image.Apply();
                int visible = 0; foreach (var pixel in image.GetPixels32()) if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) visible++;
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "movement-effect-" + name + ".png"), image.EncodeToPNG());
                UnityEngine.Object.Destroy(image); Require(visible > 20, "actual native effect pixels " + name);
                Debug.Log("Original movement effect pixels " + name + ": " + visible);
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(go); }
        }
        public sealed class Runner : MonoBehaviour { }
    }
}
