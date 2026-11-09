using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class OfficialBattleHitPlaybackAcceptance
    {
        private const string Key = "Zjcs.NativeHitAcceptance";
        static OfficialBattleHitPlaybackAcceptance()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Original hit validation").AddComponent<NativeHitAcceptanceRunner>();
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt(Key + ".exit", 1));
                }
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetInt(Key + ".exit", 1);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        internal static void Finish(string failure)
        {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "native-hit-result.json"), JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
            SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class Result { public bool passed; public string error; }
    }

    public sealed class NativeHitAcceptanceRunner : MonoBehaviour
    {
        private sealed class Grid : FightHitGridTransformData
        {
            public Pos2d Position;
            public Direction Facing = Direction.Right;
            public bool Mirrored;
            public float CenterX = .5f, CenterY = .5f;
            public Func<Pos2d, Vector3> ToPosition;
            public override Pos2d Pos => Position;
            public override Direction Direction => Facing;
            public override bool FlipX => Mirrored;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d position) => ToPosition(position);
            public override Pos2d PositionToGrid(Vector3 position) => new Pos2d((int)position.x, (int)position.y);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
            public override Pos2d LocalToWorldPos(Pos2d local)
            {
                var result = FightGridTransformMath.CalcLocalToWorldPos(new FightGridPoint(local.X, local.Y),
                    new FightGridPoint(Position.X, Position.Y), CenterX, CenterY, (int)Facing, Mirrored);
                return new Pos2d(result.X, result.Y);
            }
        }
        private sealed class Data : ECViewEntityData
        {
            public int TypeId, Serial;
            public Grid Grid;
            public FightHitBaseComponent Hit;
            public override int ClassId => TypeId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(4, Serial);
            public override T GetComponent<T>()
            {
                object value = typeof(T) == typeof(ECGridTransformData) ? Grid
                    : typeof(T).IsInstanceOfType(Hit) ? Hit : null;
                return value == null ? default : (T)value;
            }
        }
        private sealed class State
        {
            public Data Data;
            public EcEntityIdentifier SkillId;
            public List<HitScopCfg> Scopes = new List<HitScopCfg>();
        }
        private sealed class Fixed : FightHitFixedComponent
        {
            public State State;
            public readonly FightHitFixedComponentInfo Configuration = new FightHitFixedComponentInfo();
            public override EcEntityIdentifier SkillId => State.SkillId;
            public override ECViewEntityData Entity => State.Data;
            public override FightHitGridTransformData GridTransform => State.Data.Grid;
            public override FightHitBaseComponentInfo Info => Configuration;
            public override List<HitScopCfg> GetScopes() => State.Scopes;
        }
        private sealed class Chained : FightHitChainedComponent
        {
            public State State;
            public readonly FightHitChainedComponentInfo Configuration = new FightHitChainedComponentInfo();
            public override EcEntityIdentifier SkillId => State.SkillId;
            public override ECViewEntityData Entity => State.Data;
            public override FightHitGridTransformData GridTransform => State.Data.Grid;
            public override FightHitBaseComponentInfo Info => Configuration;
            public override Pos2d StartGridPos => new Pos2d(2, 3);
            public override List<HitScopCfg> GetScopes() => State.Scopes;
        }
        private sealed class Played
        {
            public string Name;
            public float At;
            public GameObject Source, Target;
            public Vector3 SourcePosition, TargetPosition;
        }

        private readonly List<Played> played = new List<Played>();
        private OfflineBattleNativePlayableScope scope;
        private OfficialBattleSkillEntityPlayback views;
        private Camera cameraView;
        private IDisposable renderPipeline;
        private UnityEngine.Rendering.RenderPipelineAsset previousPipeline, previousQuality;
        private string failure;
        private int serial;
        private FightOfficialCombatCatalog combatCatalog;

        private IEnumerator Start()
        {
            Application.logMessageReceived += Observe;
            var steps = Check();
            while (true)
            {
                object current;
                try { if (!steps.MoveNext()) break; current = steps.Current; }
                catch (Exception error) { failure = error.ToString(); break; }
                yield return current;
            }
            try { views?.Dispose(); }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            views = null;
            scope = null;
            try
            {
                renderPipeline?.Dispose();
                Require(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == previousPipeline
                    && QualitySettings.renderPipeline == previousQuality, "Battle restores the previous render pipelines.");
            }
            catch (Exception error) { failure = failure ?? error.ToString(); }
            Application.logMessageReceived -= Observe;
            Time.timeScale = 1f;
            OfficialBattleHitPlaybackAcceptance.Finish(failure);
        }

        private IEnumerator Check()
        {
            previousPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            var pipelineAssets = Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
            Require(pipelineAssets, "Original battle render pipeline is packaged.");
            renderPipeline = pipelineAssets.Enter();
            Time.maximumDeltaTime = 1f / 60f;
            var cameraObject = new GameObject("Native hit camera");
            cameraView = cameraObject.AddComponent<Camera>();
            cameraView.transform.position = new Vector3(5, 5, -20);
            cameraView.orthographic = true;
            cameraView.orthographicSize = 6;
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(.06f, .09f, .15f);
            var root = new GameObject("Native hit coordinates").transform;
            // Exercise original local/world conversion under a translated container.
            root.position = new Vector3(.25f, -.5f, 0);
            var grid = new FightBattleGrid { Width = 16, Height = 16 };
            var mapper = new OfficialBattleGridMapper(root, grid);
            var units = new[] { Unit(1, 2, 5), Unit(2, 6, 5) };
            var source = new GameObject("Source").transform;
            source.SetParent(root, false);
            source.localPosition = new Vector3(2, 5, 0);
            source.gameObject.AddComponent<MyTransform>();
            var target = new GameObject("Target").transform;
            target.SetParent(root, false);
            target.localPosition = new Vector3(6, 5, 0);
            target.gameObject.AddComponent<MyTransform>();
            scope = new OfflineBattleNativePlayableScope(root, null);
            var previousFactory = StoryPlayableInstantiation.Resolve;
            try
            {
                scope.Dispatch(() =>
                {
                    Require(StoryPlayableInstantiation.Resolve(root) == scope,
                        "Original entity pools resolve their battle instance factory.");
                    throw new InvalidOperationException("owned factory restoration probe");
                });
            }
            catch (InvalidOperationException error) when (error.Message == "owned factory restoration probe") { }
            Require(ReferenceEquals(StoryPlayableInstantiation.Resolve, previousFactory),
                "A failed entity notification restores the previous scene factory.");
            scope.BindNativePrefabAdapter(instance =>
            {
                var playable = instance.GetComponent<MyPlayable>();
                if (playable) playable.OnPlay += value => played.Add(new Played
                {
                    Name = instance.name.Replace("(Clone)", ""), At = Time.time,
                    Source = value.mySource, Target = value.myTarget,
                    SourcePosition = value.mySource ? value.mySource.transform.position : default,
                    TargetPosition = value.myTarget ? value.myTarget.transform.position : default
                });
            });
            views = new OfficialBattleSkillEntityPlayback(root, mapper, scope, id => units.FirstOrDefault(x => x.Id == id));
            var skill = new FightBattleSkillDefinition { EntityId = 10500, Slot = -1, Duration = -1f,
                FlipSource = false, SkillTargetType = 1 };
            var cast = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = skill.EntityId,
                Presentation = FightBattleSkillCastPresentation.Capture(units[0], skill,
                    new List<FightBattleUnitState> { units[1] }) };
            views.Start(cast, null, source, target);
            var skillEntity = root.GetComponentInChildren<MyFightSkill>().myEntity;
            Require(views.GetEntity(skillEntity.Id) == skillEntity, "Hit resolves the actual cast identity.");
            foreach (int id in new[] { 10501, 10721, 10821, 11541 })
                Require(Resources.Load<GameObject>("Combat/SkillGraphs/Prefabs/EcEntity/Fight/FightSkill/"
                    + (id == 10501 ? "10500" : id == 10721 ? "10720" : id == 10821 ? "10820" : "11540")
                    + "/" + id), "Preload original hit " + id);
            yield return null;

            var projectile = CreateFixed(10501, skillEntity.Id, views, 5, 5);
            projectile.Configuration.Offset = Vector3.right;
            projectile.Configuration.DelayDistFactor = .3f;
            var projectileEntity = views.CreateHit(projectile.State.Data);
            Require(ReferenceEquals(StoryPlayableInstantiation.Resolve, previousFactory),
                "Pooled hit creation restores the previous scene factory.");
            Require(played.Any(x => x.Name == "playable10501" && x.Source == source.gameObject
                && x.Target == projectileEntity.gameObject), "Original source-to-hit graph plays before distance delay.");
            projectile.State.Data.Grid.Position = new Pos2d(6, 4);
            projectile.OnHitPosChange.Invoke(projectile.State.Data.Grid.Position);
            Require(Vector3.Distance(projectileEntity.transform.position,
                root.TransformPoint(views.GridToPos(new Pos2d(6, 4)))) < .0001f, "Original hit position notification.");
            yield return new WaitForSeconds(.15f);
            Time.timeScale = 0f;
            Capture("hit-projectile.png");
            int pausedCount = played.Count;
            yield return new WaitForSecondsRealtime(.15f);
            Require(played.Count == pausedCount, "Pause does not advance native hit coroutines.");
            Time.timeScale = 2f;
            yield return new WaitForSeconds(.55f);
            views.RemoveHit(projectile.State.Data.Id);
            yield return null;
            var returnedPosition = projectileEntity ? projectileEntity.transform.position : default;
            projectile.OnHitPosChange.Invoke(new Pos2d(8, 8));
            Require(views.GetEntity(projectile.State.Data.Id) == null
                && (!projectileEntity || (!projectileEntity.gameObject.activeSelf
                    && projectileEntity.transform.position == returnedPosition)),
                "Original hit removal detaches the position subscription and returns or destroys its object.");

            var area = CreateFixed(10721, skillEntity.Id, views, 5, 5);
            area.State.Data.Grid.Mirrored = true;
            area.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(0, 0), Delay = .15f });
            area.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(1, 0), Delay = .05f });
            area.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(0, 1), Delay = .25f });
            int before = played.Count;
            views.CreateHit(area.State.Data);
            yield return new WaitForSeconds(.2f);
            var cells = played.Skip(before).Where(x => x.Name == "playable10721").ToArray();
            Require(cells.Length == 2, "Original unsorted scope entries retain their order.");
            Require(Mathf.Abs(cells[0].At - cells[1].At) < .0001f, "Decreasing scope delay plays in the same frame.");
            for (int i = 0; i < cells.Length; i++)
                Require(Vector3.Distance(cells[i].TargetPosition,
                    root.TransformPoint(views.GridToPos(area.State.Data.Grid.LocalToWorldPos(area.State.Scopes[i].Pos)))) < .0001f,
                    "Scope uses original direction, flip and RealCenter.");
            yield return new WaitForSeconds(.25f);
            cells = played.Skip(before).Where(x => x.Name == "playable10721").ToArray();
            Require(cells.Length == 3 && cells[2].At - cells[1].At >= .195f,
                "Scope waits relative to the preceding authored entry.");
            Capture("hit-scope.png");
            views.RemoveHit(area.State.Data.Id);
            yield return null;
            Require(!root.GetComponentsInChildren<Transform>(true).Any(x => x.name == "tmp_scope_obj"), "Scope anchors reclaimed.");

            var interruptedArea = CreateFixed(10721, skillEntity.Id, views, 5, 5);
            interruptedArea.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(0, 0), Delay = .05f });
            interruptedArea.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(1, 0), Delay = .8f });
            before = played.Count;
            views.CreateHit(interruptedArea.State.Data);
            yield return new WaitForSeconds(.1f);
            Require(played.Skip(before).Count(x => x.Name == "playable10721") == 1,
                "Original scope starts before early hit removal.");
            views.RemoveHit(interruptedArea.State.Data.Id);
            yield return new WaitForSeconds(.9f);
            Require(played.Skip(before).Count(x => x.Name == "playable10721") == 1
                && !root.GetComponentsInChildren<Transform>(true).Any(x => x.name == "tmp_scope_obj"),
                "Original pooled removal cancels remaining scope delays and reclaims their anchors.");

            var chainState = NewState(10821, skillEntity.Id, views, 6, 5);
            var chained = new Chained { State = chainState };
            chainState.Data.Hit = chained;
            chained.Configuration.Offset = new Vector3(.5f, 0, 0);
            var chainEntity = views.CreateHit(chainState.Data);
            var link = played.LastOrDefault(x => x.Name == "playable10820_hit");
            Require(link != null && Vector3.Distance(link.SourcePosition,
                root.TransformPoint(views.GridToPos(chained.StartGridPos)) + chained.Configuration.Offset) < .0001f,
                "Original chained source uses world conversion before its offset.");
            yield return new WaitForSeconds(.15f);
            Capture("hit-chain.png");
            views.RemoveHit(chainState.Data.Id);
            yield return null;
            var chainAnchors = root.GetComponentsInChildren<Transform>(true)
                .Where(x => x.name.StartsWith("ChainedHit_")).ToArray();
            Require(chainAnchors.Length == 1, "Original chained source sibling survives hit removal.");
            var repeatState = NewState(10821, skillEntity.Id, views, 7, 5);
            var repeatEntity = views.CreateHit(Project(repeatState, 10820));
            if (chainEntity)
                Require(repeatEntity == chainEntity && root.GetComponentsInChildren<Transform>(true)
                    .Count(x => x.name.StartsWith("ChainedHit_")) == 1,
                    "Original pooled chained entity reuses its source sibling.");
            views.RemoveHit(repeatState.Data.Id);
            yield return null;

            var summonState = NewState(11541, skillEntity.Id, views, 5, 5);
            before = played.Count;
            views.CreateHit(Project(summonState, 11540));
            yield return new WaitForSeconds(.1f);
            Require(played.Skip(before).Any(x => x.Name == "playable11541"), "Original summon hit graph plays without a damage event.");
            Capture("hit-summon.png");
            views.RemoveHit(summonState.Data.Id);
            yield return null;

            var randomState = NewState(10921, skillEntity.Id, views, 5, 5);
            before = played.Count;
            var randomEntity = views.CreateHit(Project(randomState, 10920));
            yield return new WaitForSeconds(.1f);
            Require(played.Skip(before).Any(x => x.Name == "playable10921"
                && x.Source == randomEntity.gameObject && x.Target == randomEntity.gameObject),
                "Original random-target parent plays its own graph without a damage event.");
            Capture("hit-random-parent.png");
            views.RemoveHit(randomState.Data.Id);
            yield return null;

            var customState = NewState(12063, skillEntity.Id, views, 5, 5);
            before = played.Count;
            var customData = Project(customState, 12060);
            var customEntity = views.CreateHit(customData);
            yield return new WaitForSeconds(.1f);
            customState.Data.Grid.Position = new Pos2d(4, 5);
            customData.PublishPositionChanged();
            Require(!played.Skip(before).Any(x => x.Source == customEntity.gameObject
                    || x.Target == customEntity.gameObject) && Vector3.Distance(customEntity.transform.position,
                root.TransformPoint(views.GridToPos(customState.Data.Grid.Position))) < .0001f,
                "Original custom-target parent has no graph and still receives hit-position updates.");
            views.RemoveHit(customState.Data.Id);
            yield return null;

            var triggerState = NewState(13522, skillEntity.Id, views, 4, 5);
            before = played.Count;
            var triggerEntity = views.CreateHit(Project(triggerState, 13520));
            Require(played.Skip(before).Any(x => x.Name == "playable13522"
                && x.Source == target.gameObject && x.Target == triggerEntity.gameObject),
                "Original trigger-skill trajectory starts at the skill playable target.");
            yield return new WaitForSeconds(.15f);
            Capture("hit-trigger-skill.png");
            views.RemoveHit(triggerState.Data.Id);
            yield return null;
            Require(failure == null, failure ?? "Original native hit flow.");
            views.Dispose();
            views = null;
            scope = null;
            yield return null;
            Require(!root.GetComponentsInChildren<Transform>(true).Any(x =>
                x.name.StartsWith("ChainedHit_") || x.name == "tmp_scope_obj"
                    || x.name == "Battle native hit entities"),
                "Battle disposal releases the original hit pool and its sibling anchors.");
        }

        private State NewState(int id, EcEntityIdentifier skillId, OfficialBattleSkillEntityPlayback playback, int x, int y)
        {
            var state = new State { SkillId = skillId };
            state.Data = new Data { TypeId = id, Serial = ++serial, Grid = new Grid
                { Position = new Pos2d(x, y), ToPosition = playback.GridToPos } };
            return state;
        }
        private OfficialBattleHitEntityData Project(State state, int sourceSkill)
        {
            combatCatalog ??= OfficialCombatAssetCatalog.LoadDefault().Load();
            var definition = combatCatalog.CreateSkill(sourceSkill, 0, 0, 1, 1, 0, null)
                .HitGraphEntities.Single(x => x.EntityId == state.Data.TypeId);
            state.Data.Grid.CenterX = definition.ScopeTransform.RealCenterX;
            state.Data.Grid.CenterY = definition.ScopeTransform.RealCenterY;
            return new OfficialBattleHitEntityData(state.Data.Id, state.SkillId, definition,
                state.Data.Grid, new Pos2d(2, 3));
        }
        private Fixed CreateFixed(int id, EcEntityIdentifier skillId, OfficialBattleSkillEntityPlayback playback, int x, int y)
        {
            var state = NewState(id, skillId, playback, x, y);
            var hit = new Fixed { State = state };
            state.Data.Hit = hit;
            return hit;
        }
        private static FightBattleUnitState Unit(int id, int x, int y) => new FightBattleUnitState
            { Id = id, X = x, Y = y, Direction = 3, HasFlipX = true, Hp = 100, MaxHp = 100, CharacterType = 1 };
        private void Update() { if (views != null) views.Tick(Time.deltaTime); }
        private void LateUpdate() { if (scope != null) scope.LateTick(); }
        private void Observe(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failure = failure ?? text + "\n" + stack;
        }
        private void Capture(string name)
        {
            var render = new RenderTexture(540, 960, 24);
            var previous = RenderTexture.active;
            var previousTarget = cameraView.targetTexture;
            try
            {
                cameraView.targetTexture = render;
                cameraView.Render();
                RenderTexture.active = render;
                var image = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG());
                bool hasEffect = image.GetPixels32()
                    .Select(color => ((int)color.r << 16) | ((int)color.g << 8) | color.b)
                    .Distinct().Take(5).Count() >= 5;
                Destroy(image);
                Require(hasEffect, "Native hit capture contains only its background: " + name);
            }
            finally { cameraView.targetTexture = previousTarget; RenderTexture.active = previous; Destroy(render); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
