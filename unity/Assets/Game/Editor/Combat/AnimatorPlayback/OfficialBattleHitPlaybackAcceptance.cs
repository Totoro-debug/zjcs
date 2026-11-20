using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleTrackPlayables;
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
            SessionState.SetBool(Key + ".playerJump", false);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }
        public static void RunWithPlayerJump()
        {
            Run();
            SessionState.SetBool(Key + ".playerJump", true);
        }
        internal static bool PlayerJump => SessionState.GetBool(Key + ".playerJump", false);
        internal static void Finish(string failure)
        {
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "native-hit-result.json"), JsonUtility.ToJson(new Result { passed = failure == null, error = failure }, true));
            SessionState.SetInt(Key + ".exit", failure == null ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
        [Serializable] private sealed class Result { public bool passed; public string error; }
    }

    public sealed partial class NativeHitAcceptanceRunner : MonoBehaviour
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
            public int Frame;
            public GameObject Source, Target;
            public Vector3 SourcePosition, TargetPosition;
        }

        private sealed class StatusClockDuringCast : IFightBattleImpactGate, IFightBattleCastPreparationObserver
        {
            public Action Start;
            public void CastPrepared(FightBattleSkillCastEvent cast) { var action = Start; Start = null; action?.Invoke(); }
            public void AwaitImpact(FightBattlePendingImpact impact) { }
        }

        private sealed class HitArrival
        {
            public EcEntityIdentifier SkillId, HitId;
            public Pos2d Position;
            public int ScopeIndex;
            public float At;
        }

        private sealed class SelectionArrival
        {
            public FightBattleSkillCastEvent Cast;
            public FightBattleHitSelectionSnapshot Selection;
            public int CenterIndex, ScopeIndex;
            public Pos2d Position;
        }

        private readonly List<Played> played = new List<Played>();
        private readonly List<HitArrival> arrivals = new List<HitArrival>();
        private readonly List<SelectionArrival> selectionArrivals = new List<SelectionArrival>();
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
            var steps = new Stack<IEnumerator>();
            steps.Push(Check());
            while (steps.Count > 0)
            {
                object current;
                try
                {
                    if (!steps.Peek().MoveNext()) { steps.Pop(); continue; }
                    current = steps.Peek().Current;
                    if (current is IEnumerator nested) { steps.Push(nested); continue; }
                }
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
            AudioListener.volume = 0f;
            Application.runInBackground = true;
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
                    Name = instance.name.Replace("(Clone)", ""), At = Time.time, Frame = Time.frameCount,
                    Source = value.mySource, Target = value.myTarget,
                    SourcePosition = value.mySource ? value.mySource.transform.position : default,
                    TargetPosition = value.myTarget ? value.myTarget.transform.position : default
                });
            });
            views = new OfficialBattleSkillEntityPlayback(root, mapper, scope, id => units.FirstOrDefault(x => x.Id == id));
            views.HitViewArrived += (skillId, hitId, position, index) => arrivals.Add(new HitArrival {
                SkillId = skillId, HitId = hitId, Position = position, ScopeIndex = index, At = Time.time });
            views.HitSelectionViewArrived += (owner, selection, center, position, index) =>
                selectionArrivals.Add(new SelectionArrival { Cast = owner, Selection = selection,
                    CenterIndex = center, Position = position, ScopeIndex = index });
            if (Environment.GetEnvironmentVariable("ZJCS_STUN_STRIKE_ACCEPTANCE") == "1")
            {
                yield return CheckStunStrike(root, grid);
                yield break;
            }
            if (Environment.GetEnvironmentVariable("ZJCS_BOUNCING_SHIELD_IMPACT_ACCEPTANCE") == "1")
            {
                yield return CheckBouncingShieldImpact(root, grid, false);
                yield return CheckBouncingShieldImpact(root, grid, true);
                yield break;
            }
            if (Environment.GetEnvironmentVariable("ZJCS_FLAME_SPRAY_ACCEPTANCE") == "1")
            {
                yield return CheckFlameSpray(root, grid);
                yield break;
            }
            if (Environment.GetEnvironmentVariable("ZJCS_CUSTOM_TARGET_ACCEPTANCE") == "1")
            {
                combatCatalog = OfficialCombatAssetCatalog.LoadDefault().Load();
                yield return ValidateCustomReceipts(root, source, target);
                yield break;
            }
            var skill = new FightBattleSkillDefinition { EntityId = 10100, Slot = -1, Duration = -1f,
                FlipSource = false, SkillTargetType = 1 };
            var cast = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = skill.EntityId,
                Presentation = FightBattleSkillCastPresentation.Capture(units[0], skill,
                    new List<FightBattleUnitState> { units[1] }) };
            views.Start(cast, source, target);
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
            int pausedArrivals = arrivals.Count;
            yield return new WaitForSecondsRealtime(.15f);
            Require(played.Count == pausedCount && arrivals.Count == pausedArrivals,
                "Pause does not advance native hit coroutines or emit impact arrivals.");
            Time.timeScale = 2f;
            yield return new WaitForSeconds(.55f);
            Require(arrivals.Count(x => x.SkillId.Equals(skillEntity.Id)
                && x.HitId.Equals(projectile.State.Data.Id) && x.ScopeIndex == -1
                && x.Position.Equals(new Pos2d(6, 4))) == 1,
                "The native projectile reports its actual arrival and updated hit position once.");
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
            var arrivedCells = arrivals.Where(x => x.HitId.Equals(area.State.Data.Id)
                && x.ScopeIndex >= 0).ToArray();
            Require(arrivedCells.Length == 2, "Each played scope reports one arrival.");
            Require(Mathf.Abs(cells[0].At - cells[1].At) < .0001f, "Decreasing scope delay plays in the same frame.");
            for (int i = 0; i < cells.Length; i++)
            {
                Require(Vector3.Distance(cells[i].TargetPosition,
                    root.TransformPoint(views.GridToPos(area.State.Data.Grid.LocalToWorldPos(area.State.Scopes[i].Pos)))) < .0001f,
                    "Scope uses original direction, flip and RealCenter.");
                Require(arrivedCells[i].SkillId.Equals(skillEntity.Id) && arrivedCells[i].ScopeIndex == i
                    && arrivedCells[i].Position.Equals(area.State.Data.Grid.LocalToWorldPos(area.State.Scopes[i].Pos))
                    && Mathf.Abs(arrivedCells[i].At - cells[i].At) < .0001f,
                    "Scope arrival follows its actual original playable and transformed grid cell.");
            }
            yield return new WaitForSeconds(.25f);
            cells = played.Skip(before).Where(x => x.Name == "playable10721").ToArray();
            arrivedCells = arrivals.Where(x => x.HitId.Equals(area.State.Data.Id) && x.ScopeIndex >= 0).ToArray();
            Require(cells.Length == 3 && cells[2].At - cells[1].At >= .195f,
                "Scope waits relative to the preceding authored entry.");
            Require(arrivedCells.Length == 3 && arrivedCells[2].ScopeIndex == 2
                && Mathf.Abs(arrivedCells[2].At - cells[2].At) < .0001f,
                "The later scope reports its own actual playback boundary.");
            Capture("hit-scope.png");
            views.RemoveHit(area.State.Data.Id);
            yield return null;
            Require(!root.GetComponentsInChildren<Transform>(true).Any(x => x.name == "tmp_scope_obj"), "Scope anchors reclaimed.");

            var blast = CreateFixed(10801, skillEntity.Id, views, 5, 5);
            blast.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(0, 0), Delay = 0f });
            blast.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(1, 0), Delay = 0f });
            blast.State.Scopes.Add(new HitScopCfg { Pos = new Pos2d(0, 1), Delay = .1f });
            before = played.Count;
            views.CreateHit(blast.State.Data);
            yield return null;
            yield return null;
            var explosion = played.Skip(before).Single(x => x.Name == "playable10801");
            var blastArrivals = arrivals.Where(x => x.HitId.Equals(blast.State.Data.Id)).ToArray();
            Require(blastArrivals.Length == 3 && blastArrivals.Count(x => x.ScopeIndex == -1) == 1
                && blastArrivals.Count(x => x.ScopeIndex == 0) == 1 && blastArrivals.Count(x => x.ScopeIndex == 1) == 1
                && blastArrivals.All(x => Mathf.Abs(x.At - explosion.At) < .0001f)
                && blastArrivals.Single(x => x.ScopeIndex == 1).Position.Equals(
                    blast.State.Data.Grid.LocalToWorldPos(blast.State.Scopes[1].Pos)),
                "A shared original impact playable reports each simultaneous scope at its actual start.");
            Capture("hit-shared-explosion.png");
            yield return new WaitForSeconds(.15f);
            Require(arrivals.Count(x => x.HitId.Equals(blast.State.Data.Id)) == 3,
                "A scope with a later configured boundary must not be invented by the shared impact.");
            views.RemoveHit(blast.State.Data.Id);
            yield return null;

            var selectedA = new FightBattleHitSelectionSnapshot { Sequence = 1, HitEntityId = 10101,
                SourceX = 2, SourceY = 5 };
            selectedA.EffectCenters.Add(new FightGridPoint(5, 5));
            selectedA.EffectCenters.Add(new FightGridPoint(6, 5));
            selectedA.EffectDelays.AddRange(new[] { 0f, 0f });
            var selectedB = new FightBattleHitSelectionSnapshot { Sequence = 1, HitEntityId = 10101,
                SourceX = 2, SourceY = 5 };
            selectedB.EffectCenters.Add(new FightGridPoint(5, 5)); selectedB.EffectDelays.Add(0f);
            var castA = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 10100,
                Presentation = FightBattleSkillCastPresentation.Capture(units[0], skill, new List<FightBattleUnitState> { units[1] }) };
            var castB = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 10100,
                Presentation = FightBattleSkillCastPresentation.Capture(units[0], skill, new List<FightBattleUnitState> { units[1] }) };
            castA.Presentation.Duration = castB.Presentation.Duration = .2f;
            castA.HitSelections.Add(selectedA); castB.HitSelections.Add(selectedB);
            int previousSelections = selectionArrivals.Count;
            views.Start(castA, source, target); views.Start(castB, source, target);
            yield return null;
            yield return null;
            var selectedArrivals = selectionArrivals.Skip(previousSelections).Where(x => x.ScopeIndex == -1).ToArray();
            Require(selectedArrivals.Length == 3
                && selectedArrivals.Count(x => ReferenceEquals(x.Cast, castA) && ReferenceEquals(x.Selection, selectedA)
                    && x.CenterIndex == 0 && x.Position.Equals(new Pos2d(5, 5))) == 1
                && selectedArrivals.Count(x => ReferenceEquals(x.Cast, castA) && ReferenceEquals(x.Selection, selectedA)
                    && x.CenterIndex == 1 && x.Position.Equals(new Pos2d(6, 5))) == 1
                && selectedArrivals.Count(x => ReferenceEquals(x.Cast, castB) && ReferenceEquals(x.Selection, selectedB)
                    && x.CenterIndex == 0 && x.Position.Equals(new Pos2d(5, 5))) == 1,
                "Actual hit arrivals preserve repeated cast, selection and centre identities.");
            views.Tick(.25f);

            var castC = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 10100,
                Presentation = FightBattleSkillCastPresentation.Capture(units[0], skill, new List<FightBattleUnitState> { units[1] }) };
            var skillC = views.Start(castC, source, target);
            var selectedC = new FightBattleHitSelectionSnapshot { Sequence = 1, HitEntityId = 10101,
                SourceX = 2, SourceY = 5, Delay = .4f };
            selectedC.EffectCenters.Add(new FightGridPoint(4, 5)); selectedC.EffectDelays.Add(.4f);
            selectedC.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2 });
            castC.HitSelections.Add(selectedC);
            int beforeLiveSelection = selectionArrivals.Count;
            using var liveLatch = new FightBattleImpactLatch();
            using var liveBridge = new OfficialBattleImpactViewBridge(views, liveLatch);
            var pendingImpact = new FightBattlePendingImpact { Cast = castC, HitSelection = selectedC,
                TargetId = 2, EffectCenterIndex = 0, OccurrenceIndex = 0 };
            var liveWorker = System.Threading.Tasks.Task.Run(() => {
                liveLatch.HitSelected(castC, selectedC);
                liveLatch.AwaitImpact(pendingImpact);
            });
            FightBattleSelectedHit offered = null;
            float handoffDeadline = Time.realtimeSinceStartup + 5f;
            while (!liveLatch.TryTakeSelection(out offered) && Time.realtimeSinceStartup < handoffDeadline)
                yield return null;
            Require(offered != null && !liveWorker.IsCompleted, "The live executor must wait for hit-view selection.");
            liveBridge.QueueSelection(offered, skillC, .1f);
            bool duplicateSelectionRejected = false;
            try { views.QueueSelectedHit(castC, skillC, selectedC, .1f); }
            catch (InvalidOperationException) { duplicateSelectionRejected = true; }
            Require(duplicateSelectionRejected, "A live hit selection cannot be queued twice.");
            views.Tick(.29f);
            yield return null;
            Require(selectionArrivals.Count == beforeLiveSelection && !liveWorker.IsCompleted,
                "A live hit must wait for its original cast boundary.");
            views.Tick(.02f);
            yield return null;
            yield return null;
            Require(selectionArrivals.Skip(beforeLiveSelection).Count(x => ReferenceEquals(x.Cast, castC)
                && ReferenceEquals(x.Selection, selectedC) && x.CenterIndex == 0 && x.ScopeIndex == -1
                && x.Position.Equals(new Pos2d(4, 5))) == 1,
                "A live selected hit must emit exactly one native arrival for its cast and centre.");
            float arrivalDeadline = Time.realtimeSinceStartup + 5f;
            while (!liveWorker.IsCompleted && Time.realtimeSinceStartup < arrivalDeadline)
                yield return null;
            Require(liveWorker.IsCompleted && !liveWorker.IsFaulted && !liveWorker.IsCanceled,
                "The native hit arrival must release the matching local executor impact.");
            liveBridge.Dispose(); liveLatch.Dispose();

            // Full official fireball rule step, routed through the original native hit graph.
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var liveCatalog = combatAssets.Load();
            var formula = combatAssets.LoadDamageFormula();
            FightBattleUnitState LiveUnit(int id, int side, int x) {
                var value = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
                    Level = 50, SubRank = 5, X = x, Y = 5, Direction = 3, Hp = 1000000, MaxHp = 1000000,
                    LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
                value.Turn.Id = id; value.Turn.Hp = value.Hp; value.Turn.Speed = id == 1 ? 1000 : 10;
                foreach (var prop in new Dictionary<int, long> { {1,1000}, {2,10}, {3,value.MaxHp}, {4,value.Turn.Speed} })
                    value.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
                foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                    "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                    value.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
                value.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
                return value;
            }
            // Healing is settled when the original target particles actually appear.
            foreach (int healingDirection in new[] { 0, 1, 2, 3, 4 })
            {
                bool self = healingDirection == 4;
                var healUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 1, 6), LiveUnit(3, 2, 9) };
                foreach (var unit in healUnits) foreach (var name in new[] { "BaseCureAdd", "BaseBeCureAdd" })
                    unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
                healUnits[0].Profession = 10; healUnits[0].Direction = healingDirection % 4;
                var healTarget = self ? healUnits[0] : healUnits[1];
                healTarget.Hp = healTarget.Turn.Hp = 400000;
                if (!self) {
                    var rootStatus = liveCatalog.CreateStatus(24020, 2, 3, 101, 0, 0, 2, 20, 1);
                    var freezeStatus = liveCatalog.CreateStatus(24215, 2, 3, 102, 0, 0, 2, 20, 1);
                    rootStatus.DurationRound = freezeStatus.DurationRound = 5;
                    healTarget.Statuses.Add(rootStatus); healTarget.Statuses.Add(freezeStatus);
                }
                var healSkill = liveCatalog.CreateSkill(10720, 0, 10020, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10720, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                healSkill.LimitedTimes = 1; healUnits[0].Skills.Add(healSkill);
                healUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(healSkill, 0));
                var healCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                healCheckpoint.Units.AddRange(healUnits);
                var healWave = new FightBattleWaveState(); healWave.UnitIds.Add(3); healCheckpoint.Waves.Add(healWave);
                FightBattleExecutor.Begin(healCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                string healCommitted = FightBattleCheckpointSerializer.Save(healCheckpoint);
                var healBaseline = FightBattleCheckpointSerializer.Load(healCommitted);
                var baselineResult = FightBattleExecutor.Restore(healBaseline, formula, new FightDamageSettlement(), liveCatalog,
                    combatAssets.LoadSkillItems()).Step(0, 1);
                Require(healBaseline.Units[healTarget.Id - 1].Hp > healTarget.Hp,
                    "The healing baseline must restore health to the lowest-health ally.");
                if (!self) Require(healBaseline.Units[1].Statuses.Count == 1,
                    "The healing baseline must remove exactly one of two negative effects.");
                var healDisplay = FightBattleCheckpointSerializer.Load(healCommitted).Units.ToArray();
                var healRoot = new GameObject("Isolated healing " + healingDirection).transform; healRoot.SetParent(root, false);
                var healBodies = new Dictionary<int, Transform>();
                foreach (var unit in healUnits) {
                    var body = new GameObject("Healing unit " + unit.Id).transform; body.SetParent(healRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    healBodies.Add(unit.Id, body);
                }
                var healAppearance = NativeJumpAppearanceFixture.Create(healBodies[1].gameObject, 10);
                var healTargetAppearance = NativeJumpAppearanceFixture.Create(healBodies[2].gameObject);
                var healScope = new OfflineBattleNativePlayableScope(healRoot, null);
                ParticleSystem healParticle = null; bool healResumed = false;
                int healPausedFrames = 0, healReceipts = 0, healFrame = -1, dispelReceipts = 0;
                healScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10721" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    healParticle = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true)
                        .Single(value => value.name == "lizishui (2)");
                    healParticle.Pause(false);
                };
                using (var healViews = new OfficialBattleSkillEntityPlayback(healRoot, new OfficialBattleGridMapper(healRoot, grid),
                    healScope, id => healDisplay.FirstOrDefault(value => value.Id == id)))
                using (var healGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), healScope, healViews))
                {
                    healViews.BindDamageUI(healRoot, 0);
                    foreach (var pair in healBodies) healViews.EnsureUnit(pair.Key, pair.Value);
                    Transform HealBody(int id) => healBodies.TryGetValue(id, out var body) ? body : null;
                    healGraph.BindGridEntities(healRoot, new OfficialBattleGridMapper(healRoot, grid), healCheckpoint, HealBody, _ => true);
                    var healVisualExecutor = FightBattleExecutor.Restore(FightBattleCheckpointSerializer.Load(healCommitted),
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                    healGraph.BindAirCombo(healVisualExecutor, healRoot, HealBody);
                    void HealDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(damage.SkillEntityId == 10720 && damage.HitEntityId == 10721 && damage.DamageEntityId == 10722
                            && damage.TargetId == healTarget.Id && damage.IsCure && healResumed && healParticle
                            && healParticle.particleCount > 0 && projected.Hp > healTarget.Hp,
                            "Healing and its projected health must wait for the original visible particles.");
                        var floats = healRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>(true);
                        Require(floats.Any(ui => ui.EntityId == new EcEntityIdentifier(1, damage.TargetId)
                            && ui.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == Math.Abs(damage.DamageValue).ToString())),
                            "The healing number must appear on the recipient in the same frame.");
                        healReceipts++;
                        healFrame = Time.frameCount;
                        if (healingDirection == 0) Capture("healing-native-impact.png", false);
                        healDisplay[Array.FindIndex(healDisplay, value => value.Id == damage.TargetId)] = projected;
                    }
                    using (var healDriver = healGraph.CreateLiveStep(healCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), HealBody, null, HealDamage))
                    {
                        healDriver.DispersePresented += damage => {
                            var floats = healRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>(true);
                            Require(!self && damage.TargetId == healTarget.Id && damage.HasDisperseNotification
                                && healFrame == Time.frameCount && floats.Any(ui => ui.gameObject.activeInHierarchy
                                    && ui.EntityId == new EcEntityIdentifier(1, healTarget.Id)
                                    && ui.Type == Zjcs.Official.BattleDamageUI.EffectUIType.DisperseDamageUI),
                                "A successful dispel must show its original target notification in the healing frame.");
                            dispelReceipts++;
                            if (healingDirection == 0) Capture("healing-native-dispel.png", false);
                        };
                        healDisplay = healCheckpoint.Units.Select(value => healDriver.DisplayUnit(value.Id)).ToArray();
                        float healDeadline = Time.realtimeSinceStartup + 15f;
                        while (!healDriver.IsCompleted && Time.realtimeSinceStartup < healDeadline)
                        {
                            healDriver.Pump(Time.deltaTime); healGraph.Tick(Time.deltaTime); healGraph.LateTick();
                            if (healParticle && !healResumed && ++healPausedFrames >= 8) {
                                Require(healParticle.isPaused && healReceipts == 0 && healDriver.DisplayUnit(healTarget.Id).Hp == healTarget.Hp,
                                    "Paused target particles cannot settle healing or change displayed health.");
                                healParticle.Play(false); healResumed = true;
                            }
                            yield return null;
                        }
                        Require(healDriver.TryTakeCompleted(out var executor, out var result) && healResumed && healReceipts == 1
                            && dispelReceipts == (self ? 0 : 1)
                            && FightBattleCheckpointSerializer.Save(healCheckpoint) == healCommitted
                            && FightBattleCheckpointSerializer.Save(result.Checkpoint) == FightBattleCheckpointSerializer.Save(healBaseline),
                            "Healing, random dispel and final checkpoint must match the official rule step.");
                    }
                }
                healScope.Dispose(); healTargetAppearance.Dispose(); healAppearance.Dispose(); Destroy(healRoot.gameObject);
            }
            // Each original water-dragon burst releases its corresponding scope row.
            foreach (var dragonDirection in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 },
                new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
            foreach (bool bigTarget in new[] { false, true })
            {
                int dx = dragonDirection[0], dy = dragonDirection[1], sx = -dy, sy = dx;
                var dragonUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7) };
                dragonUnits[0].Y = 7; dragonUnits[0].Profession = 10; dragonUnits[0].Direction = dragonDirection[2];
                if (bigTarget)
                {
                    var near = LiveUnit(2, 2, 7 + dx); near.Y = 7 + dy; dragonUnits.Add(near);
                    var big = LiveUnit(3, 2, 7 + 2 * dx); big.Y = 7 + 2 * dy;
                    big.BodyOffsets.AddRange(new[] { new FightGridPoint(0, 0), new FightGridPoint(sx, sy),
                        new FightGridPoint(dx, dy), new FightGridPoint(dx + sx, dy + sy) }); dragonUnits.Add(big);
                }
                else for (int row = 1; row <= 4; row++) for (int side = -1; side <= 1; side++) {
                    var unit = LiveUnit(dragonUnits.Count + 1, 2, 7 + row * dx + side * sx);
                    unit.Y = 7 + row * dy + side * sy; dragonUnits.Add(unit);
                }
                var dragonSkill = liveCatalog.CreateSkill(10550, 0, 10003, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10550, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                dragonSkill.LimitedTimes = 1; dragonUnits[0].Skills.Add(dragonSkill);
                dragonUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(dragonSkill, 0));
                var dragonCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                dragonCheckpoint.Units.AddRange(dragonUnits); var dragonWave = new FightBattleWaveState();
                dragonWave.UnitIds.AddRange(dragonUnits.Skip(1).Select(value => value.Id)); dragonCheckpoint.Waves.Add(dragonWave);
                FightBattleExecutor.Begin(dragonCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                string dragonCommitted = FightBattleCheckpointSerializer.Save(dragonCheckpoint);
                var dragonBaseline = FightBattleCheckpointSerializer.Load(dragonCommitted);
                FightBattleExecutor.Restore(dragonBaseline, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
                var dragonDisplay = FightBattleCheckpointSerializer.Load(dragonCommitted).Units.ToArray();
                var dragonRoot = new GameObject("Water dragon " + dx + "/" + dy + "/" + bigTarget).transform; dragonRoot.SetParent(root, false);
                var dragonBodies = new Dictionary<int, Transform>();
                foreach (var unit in dragonUnits) {
                    var body = new GameObject("Dragon unit " + unit.Id).transform; body.SetParent(dragonRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    dragonBodies.Add(unit.Id, body);
                }
                var dragonAppearance = NativeJumpAppearanceFixture.Create(dragonBodies[1].gameObject, 10);
                var dragonTargetAppearance = NativeJumpAppearanceFixture.Create(dragonBodies[2].gameObject);
                var dragonScope = new OfflineBattleNativePlayableScope(dragonRoot, null);
                var dragonParticles = new ParticleSystem[4]; var dragonResumed = new bool[4];
                dragonScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10550" || !leaf.myApplyComponent) return;
                    for (int row = 0; row < 4; row++) if (leaf.name == "effect (" + (row + 2) + ")") {
                        Require(!dragonParticles[row], "A water-dragon row started more than once.");
                        dragonParticles[row] = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true)
                            .Single(value => value.name == "hit (3)"); dragonParticles[row].Pause(false);
                    }
                };
                var dragonHits = new List<(int Target, int Wave, int Frame, long Damage)>(); int dragonPauseFrames = 0, dragonNextWave = 0;
                cameraView.transform.position = new Vector3(7, 7, -20);
                using (var dragonViews = new OfficialBattleSkillEntityPlayback(dragonRoot, new OfficialBattleGridMapper(dragonRoot, grid),
                    dragonScope, id => dragonDisplay.FirstOrDefault(value => value.Id == id)))
                using (var dragonGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), dragonScope, dragonViews))
                {
                    dragonViews.BindDamageUI(dragonRoot, 0); foreach (var pair in dragonBodies) dragonViews.EnsureUnit(pair.Key, pair.Value);
                    Transform DragonBody(int id) => dragonBodies.TryGetValue(id, out var body) ? body : null;
                    void DragonDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        int row = damage.HitEntityId - 10551;
                        Require(damage.SkillEntityId == 10550 && damage.DamageEntityId == 10555 && damage.ElementType == 101
                            && row >= 0 && row < 4 && dragonResumed[row] && dragonParticles[row]
                            && dragonParticles[row].particleCount > 0,
                            "Water damage must wait for the visible burst in its own original row.");
                        var floats = dragonRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>(true);
                        Require(floats.Any(ui => ui.EntityId == new EcEntityIdentifier(1, damage.TargetId)
                            && ui.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == Math.Abs(damage.DamageValue).ToString())),
                            "Each water-dragon damage number must start on the struck target in the impact frame.");
                        dragonHits.Add((damage.TargetId, row, Time.frameCount, Math.Abs(damage.Damage)));
                        if (dx == 0 && dy == 1 && !bigTarget && row == 1 && dragonHits.Count(value => value.Wave == 1) == 3)
                            Capture("water-dragon-native-impact.png", false);
                        dragonDisplay[Array.FindIndex(dragonDisplay, value => value.Id == damage.TargetId)] = projected;
                    }
                    using (var driver = dragonGraph.CreateLiveStep(dragonCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), DragonBody, null, DragonDamage))
                    {
                        dragonDisplay = dragonCheckpoint.Units.Select(value => driver.DisplayUnit(value.Id)).ToArray();
                        float dragonDeadline = Time.realtimeSinceStartup + 20f;
                        while (!driver.IsCompleted && Time.realtimeSinceStartup < dragonDeadline)
                        {
                            driver.Pump(Time.deltaTime); dragonGraph.Tick(Time.deltaTime); dragonGraph.LateTick();
                            if (dragonParticles.All(value => value) && dragonNextWave < 4 && ++dragonPauseFrames >= 5)
                            {
                                if (dragonNextWave == 0) Require(dragonHits.Count == 0 && dragonParticles.All(value => value.isPaused),
                                    "Paused bursts cannot settle any water-dragon hits.");
                                dragonParticles[dragonNextWave].Play(false); dragonResumed[dragonNextWave++] = true; dragonPauseFrames = 0;
                            }
                            yield return null;
                        }
                        Require(driver.TryTakeCompleted(out var executor, out var result) && dragonResumed.All(value => value)
                            && dragonHits.Count == (bigTarget ? 5 : 12)
                            && dragonHits.GroupBy(value => value.Wave).All(group => group.Select(value => value.Frame).Distinct().Count() == 1)
                            && FightBattleCheckpointSerializer.Save(dragonCheckpoint) == dragonCommitted
                            && FightBattleCheckpointSerializer.Save(result.Checkpoint) == FightBattleCheckpointSerializer.Save(dragonBaseline),
                            "Water-dragon rows, same-frame targets, repeated body hits and checkpoint must match the original step.");
                        if (bigTarget) {
                            var hits = dragonHits.Where(value => value.Target == 3).ToArray();
                            Require(hits.Length == 4 && hits.Select(value => value.Wave).SequenceEqual(new[] {1,1,2,2}),
                                "The large target must be hit once per struck body cell.");
                            for (int i = 1; i < hits.Length; i++) Require(hits[i].Damage == (long)(hits[0].Damage * Math.Pow(.75, i)),
                                "The repeated water hits must apply the original per-target falloff.");
                        }
                    }
                }
                dragonScope.Dispose(); dragonTargetAppearance.Dispose(); dragonAppearance.Dispose(); Destroy(dragonRoot.gameObject);
            }
            // Seven original feather waves launch one projectile for each affected target.
            foreach (var featherDirection in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 },
                new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
            {
                int dx = featherDirection[0], dy = featherDirection[1], sx = -dy, sy = dx;
                var featherUnits = new[] { LiveUnit(1, 1, 7), LiveUnit(2, 2, 7 + 3 * dx), LiveUnit(3, 2, 7 + 3 * dx + sx) };
                featherUnits[0].Y = 7; featherUnits[0].Profession = 10; featherUnits[0].Direction = featherDirection[2];
                featherUnits[1].Y = 7 + 3 * dy; featherUnits[2].Y = 7 + 3 * dy + sy;
                var featherSkill = liveCatalog.CreateSkill(10910, 0, 10036, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10910, 2, 20, 1).Select(value => new FightPropertyValue { Id=value.Key, Value=value.Value }).ToList());
                featherSkill.LimitedTimes = 1; featherUnits[0].Skills.Add(featherSkill);
                featherUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(featherSkill, 0));
                var featherCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                featherCheckpoint.Units.AddRange(featherUnits); var featherWave = new FightBattleWaveState();
                featherWave.UnitIds.AddRange(new[] { 2, 3 }); featherCheckpoint.Waves.Add(featherWave);
                var featherExecutor = FightBattleExecutor.Begin(featherCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var featherResult = featherExecutor.Step(0, 1); var featherRoot = new GameObject("Wind feather native").transform; featherRoot.SetParent(root, false);
                var featherBodies = new Dictionary<int, Transform>();
                foreach (var unit in featherUnits) {
                    var body = new GameObject("Feather unit " + unit.Id).transform; body.SetParent(featherRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer; featherBodies.Add(unit.Id, body);
                }
                var featherAppearance = NativeJumpAppearanceFixture.Create(featherBodies[1].gameObject, 10);
                var featherTargetAppearance = NativeJumpAppearanceFixture.Create(featherBodies[2].gameObject);
                var featherScope = new OfflineBattleNativePlayableScope(featherRoot, null);
                var featherLaunches = new List<float>(); var featherImpacts = new List<float>(); int featherAnimations = 0;
                featherScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name == "playable10910" && leaf.name == "anim") featherAnimations++;
                    if (prefab.name == "playable11051" && leaf.name == "track") featherLaunches.Add(Time.time);
                    if (prefab.name == "playable11052" && leaf.name == "HitEffect") featherImpacts.Add(Time.time);
                };
                cameraView.transform.position = new Vector3(7, 7, -20);
                using (var featherViews = new OfficialBattleSkillEntityPlayback(featherRoot, new OfficialBattleGridMapper(featherRoot, grid),
                    featherScope, id => featherCheckpoint.Units.FirstOrDefault(value => value.Id == id)))
                using (var featherGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), featherScope, featherViews)) {
                    featherViews.BindDamageUI(featherRoot, 0); foreach (var pair in featherBodies) featherViews.EnsureUnit(pair.Key, pair.Value);
                    Transform FeatherBody(int id) => featherBodies.TryGetValue(id, out var body) ? body : null;
                    float featherStart = Time.time; featherGraph.Play(featherResult.Event, featherCheckpoint, FeatherBody);
                    bool captured = false;
                    while (Time.time - featherStart < 2.2f) {
                        featherGraph.Tick(Time.deltaTime); featherGraph.LateTick();
                        if (!captured && Time.time - featherStart > .9f) {
                            if (dx == 0 && dy == 1) Capture("wind-feather-native.png", false); captured = true;
                        }
                        yield return null;
                    }
                    Require(captured && featherAnimations == 1 && featherLaunches.Count == 14 && featherImpacts.Count == 14,
                        "The original feather animation must play seven waves for both targets.");
                    for (int row = 0; row < 7; row++) {
                        float expected = featherStart + .37f + row * .1f;
                        Require(Math.Abs(featherLaunches[row * 2] - expected) < .16f
                            && Math.Abs(featherLaunches[row * 2 + 1] - expected) < .16f
                            && featherImpacts[row * 2] > featherLaunches[row * 2],
                            "Each original projectile must launch after its triggering wave and hit after travel.");
                    }
                }
                featherScope.Dispose(); featherTargetAppearance.Dispose(); featherAppearance.Dispose(); Destroy(featherRoot.gameObject);
            }
            // The original fire ring expands through separate near and outer scopes.
            for (int ringDirection = 0; ringDirection < 4; ringDirection++)
            {
                var ringUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7) };
                ringUnits[0].Y = 7; ringUnits[0].Profession = 10; ringUnits[0].Direction = ringDirection;
                for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++)
                    if (Math.Abs(x) + Math.Abs(y) > 0 && Math.Abs(x) + Math.Abs(y) <= 2)
                    { var unit = LiveUnit(ringUnits.Count + 1, 2, 7 + x); unit.Y = 7 + y; ringUnits.Add(unit); }
                var ringSkill = liveCatalog.CreateSkill(10830, 0, 10031, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10830, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                ringSkill.LimitedTimes = 1; ringUnits[0].Skills.Add(ringSkill);
                ringUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(ringSkill, 0));
                var ringCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                ringCheckpoint.Units.AddRange(ringUnits);
                var ringWave = new FightBattleWaveState(); ringWave.UnitIds.AddRange(ringUnits.Skip(1).Select(value => value.Id));
                ringCheckpoint.Waves.Add(ringWave);
                FightBattleExecutor.Begin(ringCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var ringDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(ringCheckpoint)).Units.ToArray();
                var ringRoot = new GameObject("Isolated fire ring " + ringDirection).transform; ringRoot.SetParent(root, false);
                var ringBodies = new Dictionary<int, Transform>();
                foreach (var unit in ringUnits)
                {
                    var body = new GameObject("Fire ring unit " + unit.Id).transform; body.SetParent(ringRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    ringBodies.Add(unit.Id, body);
                }
                var ringAppearance = NativeJumpAppearanceFixture.Create(ringBodies[1].gameObject, 10);
                var ringTargetAppearance = NativeJumpAppearanceFixture.Create(ringBodies[8].gameObject);
                var ringScope = new OfflineBattleNativePlayableScope(ringRoot, null);
                ParticleSystem fireRing = null; int pauseFrames = 0; bool resumed = false;
                ringScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10830" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    fireRing = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "difeng (3)");
                    fireRing.Pause(false);
                };
                var ringDamage = new List<(int Target, int Frame, float Age)>();
                bool capturedRing = false;
                cameraView.transform.position = new Vector3(7, 7, -20);
                using (var ringViews = new OfficialBattleSkillEntityPlayback(ringRoot, new OfficialBattleGridMapper(ringRoot, grid),
                    ringScope, id => ringDisplay.FirstOrDefault(value => value.Id == id)))
                using (var ringGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), ringScope, ringViews))
                {
                    ringViews.BindDamageUI(ringRoot, 0);
                    foreach (var pair in ringBodies) ringViews.EnsureUnit(pair.Key, pair.Value);
                    Transform RingBody(int id) => ringBodies.TryGetValue(id, out var body) ? body : null;
                    void RingDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        var buffer = new ParticleSystem.Particle[fireRing.main.maxParticles];
                        int count = fireRing.GetParticles(buffer);
                        Require(count > 0, "Fire ring damage must wait for the original visible particle.");
                        float age = buffer[0].startLifetime - buffer[0].remainingLifetime;
                        ringDamage.Add((damage.TargetId, Time.frameCount, age));
                        var floats = ringRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>(true);
                        Require(floats.Any(ui => ui.EntityId == new EcEntityIdentifier(1, damage.TargetId)
                            && ui.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == Math.Abs(damage.DamageValue).ToString())),
                            "Fire ring damage number must start on its target in the impact frame.");
                        ringDisplay[Array.FindIndex(ringDisplay, value => value.Id == damage.TargetId)] = projected;
                    }
                    using (var ringDriver = new OfficialBattleLiveGraphDriver(ringCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), ringViews,
                        id => id == 10830, RingBody, null, RingDamage))
                    {
                        ringDriver.SecondsStatusChanged += _ => { };
                        ringDisplay = ringCheckpoint.Units.Select(value => ringDriver.DisplayUnit(value.Id)).ToArray();
                        float ringDeadline = Time.realtimeSinceStartup + 10f;
                        while (!ringDriver.IsCompleted && Time.realtimeSinceStartup < ringDeadline)
                        {
                            ringDriver.Pump(Time.deltaTime); ringViews.Tick(Time.deltaTime); ringScope.LateTick();
                            if (fireRing && !resumed)
                            {
                                Require(ringDamage.Count == 0, "A paused original fire ring cannot settle damage.");
                                if (++pauseFrames == 8) { fireRing.Play(false); resumed = true; }
                            }
                            if (ringDirection == 0 && !capturedRing && ringDamage.Count > 0)
                            { Capture("fire-ring-native-impact.png", false); capturedRing = true; }
                            yield return null;
                        }
                        Require(ringDriver.IsCompleted && resumed && pauseFrames == 8 && ringDamage.Count == 12,
                            "The official fire ring must complete after all twelve selected enemies are hit.");
                        var near = ringDamage.Where(value => Math.Abs(ringUnits[value.Target - 1].X - 7)
                            + Math.Abs(ringUnits[value.Target - 1].Y - 7) == 1).ToArray();
                        var outer = ringDamage.Where(value => Math.Abs(ringUnits[value.Target - 1].X - 7)
                            + Math.Abs(ringUnits[value.Target - 1].Y - 7) == 2).ToArray();
                        Require(near.Length == 4 && outer.Length == 8 && near.Select(value => value.Frame).Distinct().Count() == 1
                            && outer.Select(value => value.Frame).Distinct().Count() == 1 && near[0].Frame < outer[0].Frame
                            && near.All(value => value.Age >= .05f - .0001f) && outer.All(value => value.Age >= .1f - .0001f),
                            "Near and outer enemies must settle once, together, at the original expanding ring.");
                    }
                }
                ringScope.Dispose(); ringTargetAppearance.Dispose(); ringAppearance.Dispose(); Destroy(ringRoot.gameObject);
                cameraView.transform.position = new Vector3(5, 5, -20);
            }
            // Five ice-cone scopes settle at the original animated cone, including its freeze result.
            foreach (var coneDirection in new[] { new[] { 1, 0, 3, 0 }, new[] { -1, 0, 1, 0 },
                new[] { 0, 1, 2, 0 }, new[] { 0, -1, 0, 0 }, new[] { 1, 0, 3, 1 } })
            {
                int dx = coneDirection[0], dy = coneDirection[1];
                var coneUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7) };
                coneUnits[0].Y = 7; coneUnits[0].Profession = 10; coneUnits[0].Direction = coneDirection[2];
                for (int distance = 1; distance <= 5; distance++)
                { var unit = LiveUnit(distance + 1, 2, 7 + dx * distance); unit.Y = 7 + dy * distance; coneUnits.Add(unit); }
                var coneSkill = liveCatalog.CreateSkill(10900, 0, 10035, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10900, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                coneSkill.LimitedTimes = 1; coneUnits[0].Skills.Add(coneSkill);
                coneUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(coneSkill, 0));
                var coneCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                coneCheckpoint.Units.AddRange(coneUnits);
                var coneWave = new FightBattleWaveState(); coneWave.UnitIds.AddRange(coneUnits.Skip(1).Select(value => value.Id));
                coneCheckpoint.Waves.Add(coneWave);
                FightBattleExecutor.Begin(coneCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                FightBattleCheckpoint coneBaseline = null;
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    var reference = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(coneCheckpoint));
                    reference.RandomSeed = 73 + attempt * 1234567;
                    FightBattleExecutor.Restore(reference, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
                    int frozen = reference.Units.Skip(1).Count(unit => unit.Statuses.Any(status => status.ClassId == 24215));
                    if (frozen == 0 || frozen == 5) continue;
                    coneCheckpoint.RandomSeed = 73 + attempt * 1234567; coneBaseline = reference; break;
                }
                Require(coneBaseline != null, "Could not select both original freeze outcomes for the ice cone.");
                string coneCommitted = FightBattleCheckpointSerializer.Save(coneCheckpoint);
                var coneDisplay = FightBattleCheckpointSerializer.Load(coneCommitted).Units.ToArray();
                var coneRoot = new GameObject("Isolated ice cone " + dx + "/" + dy).transform; coneRoot.SetParent(root, false);
                var coneBodies = new Dictionary<int, Transform>();
                foreach (var unit in coneUnits)
                {
                    var body = new GameObject("Ice cone unit " + unit.Id).transform; body.SetParent(coneRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    coneBodies.Add(unit.Id, body);
                }
                var coneAppearance = NativeJumpAppearanceFixture.Create(coneBodies[1].gameObject, 10);
                var coneTargetAppearance = NativeJumpAppearanceFixture.Create(coneBodies[2].gameObject);
                var coneFarAppearance = dx == 1 && coneDirection[3] == 0
                    ? NativeJumpAppearanceFixture.Create(coneBodies[6].gameObject) : null;
                var coneScope = new OfflineBattleNativePlayableScope(coneRoot, null);
                Animator coneAnimator = null; ParticleSystem coneParticle = null; AnimationClip coneClip = null;
                bool coneResumed = false, coneCaptured = false; int conePausedFrames = 0;
                coneScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10900" || leaf.name != "effect (3)" || !leaf.myApplyComponent) return;
                    coneAnimator = leaf.myApplyComponent.GetComponentsInChildren<Animator>(true).Single(value => value.name == "ani");
                    coneParticle = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "da");
                    coneClip = coneAnimator.runtimeAnimatorController.animationClips.Single(value => value.name == "Fx_Ani_Ice");
                    coneAnimator.speed = 0f;
                };
                var coneHits = new List<(int Target, int Frame)>();
                cameraView.transform.position = new Vector3(7, 7, -20);
                using (var coneViews = new OfficialBattleSkillEntityPlayback(coneRoot, new OfficialBattleGridMapper(coneRoot, grid),
                    coneScope, id => coneDisplay.FirstOrDefault(value => value.Id == id)))
                using (var coneGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), coneScope, coneViews))
                {
                    coneViews.BindDamageUI(coneRoot, 0);
                    foreach (var pair in coneBodies) coneViews.EnsureUnit(pair.Key, pair.Value);
                    Transform ConeBody(int id) => coneBodies.TryGetValue(id, out var body) ? body : null;
                    void ConeDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        int index = damage.TargetId - 2;
                        Require(damage.SkillEntityId == 10900 && damage.HitEntityId == 10901 && damage.DamageEntityId == 10902
                            && damage.ElementType == 101 && index == coneHits.Count && index >= 0 && index < 5
                            && coneAnimator && coneParticle && coneParticle.particleCount > 0 && coneClip
                            && coneAnimator.GetCurrentAnimatorClipInfo(0).Any(value => value.clip == coneClip)
                            && coneResumed, "Ice damage must follow its original visible cone through each scope.");
                        var floats = coneRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>(true);
                        Require(floats.Any(ui => ui.EntityId == new EcEntityIdentifier(1, damage.TargetId)
                            && ui.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == Math.Abs(damage.DamageValue).ToString())),
                            "Ice damage number must appear on its target in the impact frame.");
                        coneHits.Add((damage.TargetId, Time.frameCount));
                        if (dx == 1 && coneDirection[3] == 0 && !coneCaptured)
                        { Capture("ice-cone-native-impact.png", false); coneCaptured = true; }
                        if (dx == 1 && coneDirection[3] == 0 && index == 4)
                        {
                            cameraView.transform.position = new Vector3(10.5f, 7, -20);
                            Capture("ice-cone-native-far-impact.png", false);
                        }
                        coneDisplay[Array.FindIndex(coneDisplay, value => value.Id == damage.TargetId)] = projected;
                    }
                    using (var coneDriver = new OfficialBattleLiveGraphDriver(coneCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), coneViews,
                        id => id == 10900, ConeBody, null, ConeDamage))
                    {
                        coneDriver.SecondsStatusChanged += _ => { };
                        coneDisplay = coneCheckpoint.Units.Select(value => coneDriver.DisplayUnit(value.Id)).ToArray();
                        float coneDeadline = Time.realtimeSinceStartup + 15f;
                        while (!coneDriver.IsCompleted && Time.realtimeSinceStartup < coneDeadline)
                        {
                            coneDriver.Pump(Time.deltaTime); coneViews.Tick(Time.deltaTime); coneScope.LateTick();
                            if (coneAnimator && !coneResumed && ++conePausedFrames >= 8)
                            {
                                Require(coneAnimator.transform.localPosition.y == 0f && coneHits.Count == 0,
                                    "A paused ice cone cannot reach its targets.");
                                coneAnimator.speed = coneDirection[3] == 0 ? 1f : 10f; coneResumed = true;
                            }
                            yield return null;
                        }
                        Require(coneDriver.TryTakeCompleted(out var executor, out var result) && coneResumed && coneHits.Count == 5
                            && coneHits.Select(value => value.Target).Distinct().Count() == 5
                            && FightBattleCheckpointSerializer.Save(coneCheckpoint) == coneCommitted
                            && FightBattleCheckpointSerializer.Save(result.Checkpoint) == FightBattleCheckpointSerializer.Save(coneBaseline),
                            "The ice cone must preserve all five actual hits, freeze results and the original checkpoint.");
                        foreach (var frozen in executor.State.Units.Skip(1).SelectMany(unit => unit.Statuses).Where(status => status.ClassId == 24215))
                            Require(frozen.DurationRound == 1 && frozen.CreatorId == 1 && frozen.ApplyingSkillItemId == 10035,
                                "The ice cone must preserve the original freeze source and duration.");
                    }
                }
                coneScope.Dispose(); coneFarAppearance?.Dispose(); coneTargetAppearance.Dispose(); coneAppearance.Dispose(); Destroy(coneRoot.gameObject);
                cameraView.transform.position = new Vector3(5, 5, -20);
            }
            // The original iron thorns appear before the five simultaneous hits and their root results.
            foreach (var thornDirection in new[] { new[] { 1, 0, 3, 0 }, new[] { -1, 0, 1, 0 },
                new[] { 0, 1, 2, 0 }, new[] { 0, -1, 0, 0 }, new[] { 1, 0, 3, 1 } })
            {
                int dx = thornDirection[0], dy = thornDirection[1];
                var thornUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7 - 3 * dx) };
                thornUnits[0].Y = 7 - 3 * dy; thornUnits[0].Profession = 10; thornUnits[0].Direction = thornDirection[2];
                foreach (var offset in new[] { new[] { 0, 0 }, new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } })
                { var unit = LiveUnit(thornUnits.Count + 1, 2, 7 + offset[0]); unit.Y = 7 + offset[1]; thornUnits.Add(unit); }
                var thornSkill = liveCatalog.CreateSkill(10570, 0, 10005, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10570, 2, 20, 1).Select(value =>
                        new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
                thornSkill.LimitedTimes = 1; thornUnits[0].Skills.Add(thornSkill);
                thornUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(thornSkill, 0));
                var thornCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                thornCheckpoint.Units.AddRange(thornUnits);
                var thornWave = new FightBattleWaveState(); thornWave.UnitIds.AddRange(thornUnits.Skip(1).Select(value => value.Id));
                thornCheckpoint.Waves.Add(thornWave);
                FightBattleExecutor.Begin(thornCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                FightBattleCheckpoint thornBaseline = null;
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    var reference = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(thornCheckpoint));
                    reference.RandomSeed = 73 + attempt * 1234567;
                    FightBattleExecutor.Restore(reference, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
                    int rooted = reference.Units.Skip(1).Count(unit => unit.Statuses.Any(status => status.ClassId == 24020));
                    if (rooted == 0 || rooted == 5) continue;
                    thornCheckpoint.RandomSeed = 73 + attempt * 1234567; thornBaseline = reference; break;
                }
                Require(thornBaseline != null, "Could not select both original root outcomes for the iron thorns.");
                string thornCommitted = FightBattleCheckpointSerializer.Save(thornCheckpoint);
                var thornDisplay = FightBattleCheckpointSerializer.Load(thornCommitted).Units.ToArray();
                var thornRoot = new GameObject("Isolated iron thorns " + dx + "/" + dy).transform; thornRoot.SetParent(root, false);
                var thornBodies = new Dictionary<int, Transform>();
                foreach (var unit in thornUnits)
                {
                    var body = new GameObject("Iron thorn unit " + unit.Id).transform; body.SetParent(thornRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    thornBodies.Add(unit.Id, body);
                }
                var thornAppearance = NativeJumpAppearanceFixture.Create(thornBodies[1].gameObject, 10);
                var thornTargetAppearance = NativeJumpAppearanceFixture.Create(thornBodies[2].gameObject);
                var thornScope = new OfflineBattleNativePlayableScope(thornRoot, null);
                ParticleSystem thornParticle = null; bool thornResumed = false; int thornPausedFrames = 0;
                thornScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable_scope_10570" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    thornParticle = leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true)
                        .Single(value => value.name == "tieji (1)");
                    thornParticle.Pause(false);
                };
                var thornHits = new List<(int Target, int Frame)>();
                int thornStatusStarts = 0;
                cameraView.transform.position = new Vector3(7, 7, -20);
                using (var thornViews = new OfficialBattleSkillEntityPlayback(thornRoot, new OfficialBattleGridMapper(thornRoot, grid),
                    thornScope, id => thornDisplay.FirstOrDefault(value => value.Id == id)))
                using (var thornGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), thornScope, thornViews))
                {
                    thornViews.BindDamageUI(thornRoot, 0);
                    foreach (var pair in thornBodies) thornViews.EnsureUnit(pair.Key, pair.Value);
                    Transform ThornBody(int id) => thornBodies.TryGetValue(id, out var body) ? body : null;
                    thornGraph.BindGridEntities(thornRoot, new OfficialBattleGridMapper(thornRoot, grid), thornCheckpoint, ThornBody, _ => true);
                    var thornVisualExecutor = FightBattleExecutor.Restore(FightBattleCheckpointSerializer.Load(thornCommitted),
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                    thornGraph.BindAirCombo(thornVisualExecutor, thornRoot, ThornBody);
                    void ThornDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(damage.SkillEntityId == 10570 && damage.HitEntityId == 10571 && damage.DamageEntityId == 10572
                            && damage.ElementType == 100 && damage.TargetId >= 2 && damage.TargetId <= 6 && thornResumed && thornParticle
                            && (thornParticle.particleCount > 0 || thornDirection[3] == 1 && thornParticle.time > thornParticle.main.startDelay.constant),
                            "Iron thorn damage must wait for its original visible particle.");
                        var floats = thornRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>(true);
                        Require(floats.Any(ui => ui.EntityId == new EcEntityIdentifier(1, damage.TargetId)
                            && ui.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == Math.Abs(damage.DamageValue).ToString())),
                            "Iron thorn damage number must appear on its target in the impact frame.");
                        thornHits.Add((damage.TargetId, Time.frameCount));
                        if (dx == 1 && thornDirection[3] == 0 && thornHits.Count == 5) Capture("iron-thorn-native-impact.png", false);
                        thornDisplay[Array.FindIndex(thornDisplay, value => value.Id == damage.TargetId)] = projected;
                    }
                    using (var thornDriver = thornGraph.CreateLiveStep(thornCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), ThornBody, null, ThornDamage))
                    {
                        thornDriver.StatusViewChanged += transition => {
                            if (transition.Kind != FightBattleStatusViewEventKind.Start || transition.Status.ClassId != 24020) return;
                            Require(thornHits.Any(hit => hit.Target == transition.OwnerId && hit.Frame == Time.frameCount)
                                && thornRoot.GetComponentsInChildren<Zjcs.Official.BattleStatusPlayables.MyFightStatus>(true).Any(status => {
                                    var entity = status.GetComponent<MyEntity>();
                                    return entity && entity.Entity != null && entity.ClassId == 24020
                                        && status.Context != null && status.Context.Owner == thornBodies[transition.OwnerId].gameObject;
                                }), "The original root status view must start on the affected target in its actual impact frame.");
                            thornStatusStarts++;
                        };
                        thornDisplay = thornCheckpoint.Units.Select(value => thornDriver.DisplayUnit(value.Id)).ToArray();
                        float thornDeadline = Time.realtimeSinceStartup + 15f;
                        while (!thornDriver.IsCompleted && Time.realtimeSinceStartup < thornDeadline)
                        {
                            thornDriver.Pump(Time.deltaTime); thornGraph.Tick(Time.deltaTime); thornGraph.LateTick();
                            if (thornParticle && !thornResumed && ++thornPausedFrames >= 8)
                            {
                                Require(thornParticle.isPaused && thornHits.Count == 0 && thornStatusStarts == 0,
                                    "A paused iron thorn particle cannot settle damage or show its root state.");
                                if (thornDirection[3] == 1) thornParticle.Simulate(.8f, false, true, false);
                                thornParticle.Play(false); thornResumed = true;
                            }
                            yield return null;
                        }
                        Require(thornDriver.TryTakeCompleted(out var executor, out var result) && thornResumed && thornHits.Count == 5
                            && thornHits.Select(value => value.Target).Distinct().Count() == 5
                            && thornHits.Select(value => value.Frame).Distinct().Count() == 1
                            && thornStatusStarts == thornBaseline.Units.Skip(1).Count(unit => unit.Statuses.Any(status => status.ClassId == 24020))
                            && FightBattleCheckpointSerializer.Save(thornCheckpoint) == thornCommitted
                            && FightBattleCheckpointSerializer.Save(result.Checkpoint) == FightBattleCheckpointSerializer.Save(thornBaseline),
                            "The iron thorns must preserve their simultaneous hits, root results and original checkpoint.");
                        foreach (var rooted in executor.State.Units.Skip(1).SelectMany(unit => unit.Statuses).Where(status => status.ClassId == 24020))
                            Require(rooted.DurationRound == 1 && rooted.CreatorId == 1 && rooted.ApplyingSkillItemId == 10005,
                                "The iron thorns must preserve the original root source and duration.");
                    }
                }
                thornScope.Dispose(); thornTargetAppearance.Dispose(); thornAppearance.Dispose(); Destroy(thornRoot.gameObject);
                cameraView.transform.position = new Vector3(5, 5, -20);
            }
            var fireUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 5) };
            var fireRoot = new GameObject("Isolated fireball scene").transform;
            fireRoot.SetParent(root, false);
            var fireSource = new GameObject("Fireball source").transform; fireSource.SetParent(fireRoot, false);
            fireSource.localPosition = new Vector3(2, 5, 0); fireSource.gameObject.AddComponent<MyTransform>();
            var fireTarget = new GameObject("Fireball target").transform; fireTarget.SetParent(fireRoot, false);
            fireTarget.localPosition = new Vector3(5, 5, 0); fireTarget.gameObject.AddComponent<MyTransform>();
            foreach (var actorView in new[] { fireSource, fireTarget }) {
                var renderer = actorView.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                renderer.sharedMaterials = Array.Empty<Material>();
                actorView.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
            }
            var fireScope = new OfflineBattleNativePlayableScope(fireRoot, null);
            fireScope.BindNativePrefabAdapter(instance => {
                var playable = instance.GetComponent<MyPlayable>();
                if (playable) playable.OnPlay += value => played.Add(new Played { Name = instance.name.Replace("(Clone)", ""), At = Time.time, Frame = Time.frameCount });
            });
            FightBattleUnitState[] fireDisplayUnits = null;
            using var fireViews = new OfficialBattleSkillEntityPlayback(fireRoot, new OfficialBattleGridMapper(fireRoot, grid),
                fireScope, id => fireDisplayUnits?.FirstOrDefault(value => value.Id == id));
            var graphStarts = new List<(FightBattleSkillCastEvent Cast, EcEntityIdentifier Id, GameObject Prefab, MyPlayable Leaf, int Frame)>();
            fireViews.SkillGraphPlayableStarted += (cast, id, prefab, leaf) =>
                graphStarts.Add((cast, id, prefab, leaf, Time.frameCount));
            fireViews.BindDamageUI(fireRoot, 0);
            var fire = liveCatalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10800, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            fire.LimitedTimes = 1; fireUnits[0].Skills.Add(fire); fireUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(fire, 0));
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            cp.Units.AddRange(fireUnits); var fireWave = new FightBattleWaveState(); fireWave.UnitIds.Add(2); cp.Waves.Add(fireWave);
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            string committedFire = FightBattleCheckpointSerializer.Save(cp);
            fireDisplayUnits = FightBattleCheckpointSerializer.Load(committedFire).Units.ToArray();
            using var fireStep = new FightBattleIsolatedStep(cp, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), 0, 1);
            var fireLatch = fireStep.Latch;
            using var fireBridge = new OfficialBattleImpactViewBridge(fireViews, fireLatch);
            FightBattlePreparedCast firePrepared = null;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!fireLatch.TryTakePreparation(out firePrepared) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(firePrepared != null && firePrepared.Cast.SkillItemId == 10028, "Official fireball was not prepared.");
            var fireCast = firePrepared.Cast;
            float fireStartedAt = Time.time;
            var fireSkillId = fireViews.Start(fireCast, fireSource, fireTarget);
            Require(graphStarts.Any(value => ReferenceEquals(value.Cast, fireCast) && value.Id.Equals(fireSkillId)
                && value.Prefab.name == "playable10800" && value.Leaf && value.Frame == Time.frameCount),
                "An original zero-delay skill graph must report its actual cast and playback frame.");
            var floatRoot = fireRoot.GetComponentInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUIManager>().transform;
            int damageNotifications = 0;
            long observedDamage = 0;
            fireViews.EnsureUnit(2, fireTarget).GetComponent<MyFightRoleFight>().OnDamage += (role, damage) => {
                damageNotifications++; observedDamage = damage.Component.DamageResult.DamageValue;
            };
            fireLatch.CompletePreparation(firePrepared);
            FightBattleSelectedHit fireSelected = null;
            deadline = Time.realtimeSinceStartup + 10f;
            while (!fireLatch.TryTakeSelection(out fireSelected) && Time.realtimeSinceStartup < deadline) yield return null;
            Require(fireSelected != null && fireSelected.Selection.HitEntityId == 10801, "Official fireball hit was not selected.");
            bool fireArrival = false;
            Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot, int, Pos2d, int> fireObserver =
                (c, h, center, pos, cell) => {
                    if (ReferenceEquals(c, fireCast) && ReferenceEquals(h, fireSelected.Selection)
                        && cell == fireSelected.Selection.Targets[0].ScopeIndex) fireArrival = true;
                };
            fireViews.HitSelectionViewArrived += fireObserver;
            int beforeFire = played.Count;
            fireBridge.QueueSelection(fireSelected, fireSkillId, 0f);
            FightBattleSettledImpact fireDamage = null;
            deadline = Time.realtimeSinceStartup + 10f;
            while (!fireLatch.TryTakeSettlement(out fireDamage) && Time.realtimeSinceStartup < deadline) {
                if (!fireArrival) Require(fireUnits[1].Hp == 1000000
                    && floatRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>().Length == 0,
                    "Fireball HP and damage numbers must wait for the projectile impact.");
                fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                yield return null;
            }
            Require(fireDamage != null && fireArrival && fireDamage.Damage.BeforeHp == 1000000
                && fireDamage.Damage.AfterHp < 1000000 && fireUnits[1].Hp == 1000000
                && fireDisplayUnits[1].Hp == 1000000 && FightBattleCheckpointSerializer.Save(cp) == committedFire
                && fireDamage.Damage.HasDamageTriggerStatus && !fireDamage.Damage.IsDamageTriggerStatus
                && played.Skip(beforeFire).Any(x => x.Name == "playable10801"),
                "Official fireball did not settle its actual HP at the native explosion.");
            var incomplete = new FightBattleDamageEvent { DamageId = fireDamage.Damage.DamageId,
                SourceId = fireDamage.Damage.SourceId, SkillEntityId = fireDamage.Damage.SkillEntityId };
            Require(!fireViews.QueueSettledDamage(fireCast, fireSkillId, incomplete) && damageNotifications == 0,
                "An incomplete damage result cannot start the native damage view.");
            long fireDamageValue = fireDamage.Damage.DamageValue;
            bool hpRefreshAtImpact = false;
            Require(fireBridge.TryQueueSettlement(fireDamage, fireSkillId, damage => {
                fireDisplayUnits[1].Hp = damage.AfterHp;
                fireDisplayUnits[1].Turn.Hp = damage.AfterHp;
                hpRefreshAtImpact = !fireStep.IsCompleted && fireUnits[1].Hp == 1000000
                    && fireDisplayUnits[1].Hp == damage.AfterHp && damageNotifications == 1
                    && observedDamage == fireDamageValue;
            }) && hpRefreshAtImpact,
                "Actual fireball settlement must show damage and allow HP refresh at the same impact.");
            var floats = floatRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>();
            Require(floats.Any(ui => ui.EntityId == new EcEntityIdentifier(1, 2)
                && ui.GetComponentsInChildren<TextMesh>(true).Any(text =>
                    text.text == Math.Abs(fireDamageValue).ToString())),
                "The original damage UI must display a number on the actual target at impact.");
            Capture("fireball-damage-impact.png");
            FightBattleCompletedCast fireCompletion = null;
            deadline = Time.realtimeSinceStartup + 10f;
            while (!fireLatch.TryTakeCastCompletion(out fireCompletion) && Time.realtimeSinceStartup < deadline) {
                fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                yield return null;
            }
            Require(fireCompletion != null && ReferenceEquals(fireCompletion.Cast, fireCast)
                && fireCompletion.Cast.Presentation.Duration >= 0f && !fireStep.IsCompleted,
                "The original cast must report its final lifetime before the step completes.");
            float fireElapsedAtCompletion = Time.time - fireStartedAt;
            fireViews.CompleteLiveCast(fireCast, fireSkillId, fireElapsedAtCompletion);
            bool duplicateLifetimeRejected = false;
            try { fireViews.CompleteLiveCast(fireCast, fireSkillId, fireElapsedAtCompletion); }
            catch (InvalidOperationException) { duplicateLifetimeRejected = true; }
            Require(duplicateLifetimeRejected, "A live cast lifetime must be finalized only once.");
            fireLatch.CompleteCast(fireCompletion);
            deadline = Time.realtimeSinceStartup + 10f;
            while (!fireStep.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Require(fireStep.IsCompleted, "The official fireball action did not complete after impact.");
            Require(fireStep.TryTakeCompleted(out var completedExecutor, out var completedFire)
                && ReferenceEquals(completedExecutor.State, completedFire.Checkpoint)
                && FightBattleCheckpointSerializer.Save(cp) == committedFire,
                "The completed fireball must transfer its isolated checkpoint without changing the original.");
            bool duplicateDamageRejected = false;
            try { fireViews.QueueSettledDamage(fireCast, fireSkillId, fireDamage.Damage); }
            catch (InvalidOperationException) { duplicateDamageRejected = true; }
            Require(duplicateDamageRejected, "The same impact cannot create duplicate native damage entities.");
            float fireRemaining = Mathf.Max(0f, fireCompletion.Cast.Presentation.Duration - fireElapsedAtCompletion);
            Require(fireRemaining > .02f, "The fireball lifetime fixture must retain a visible remainder.");
            fireViews.Tick(fireRemaining - .01f);
            Require(fireViews.GetEntity(fireSkillId) != null,
                "The original live skill entity cannot end before its finalized lifetime.");
            fireViews.Tick(.02f);
            Require(fireViews.GetEntity(fireSkillId) == null,
                "The original live skill entity must end at its finalized cast lifetime.");
            var restoredFire = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(completedFire.Checkpoint));
            var restoredEvent = restoredFire.Receipts.Last().Event;
            var restoredCast = restoredEvent.SkillCasts.Single(c => c.SkillItemId == 10028);
            var restoredDamage = restoredEvent.DamageEvents.Single(d => d.DamageId == fireDamage.Damage.DamageId);
            var replayDamage = new Dictionary<int, FightBattleDamageEvent> {
                { restoredDamage.DamageId, restoredDamage } };
            var restoredSkillId = fireViews.Start(restoredCast, fireSource, fireTarget, replayDamage);
            Require(!restoredSkillId.Equals(fireSkillId) && graphStarts.Any(value => ReferenceEquals(value.Cast, restoredCast)
                && value.Id.Equals(restoredSkillId) && value.Prefab.name == "playable10800"),
                "A repeated skill graph must retain its own cast and entity identity.");
            Require(damageNotifications == 1, "A saved fireball receipt cannot display damage before its boundary.");
            deadline = Time.realtimeSinceStartup + 10f;
            while (damageNotifications < 2 && Time.realtimeSinceStartup < deadline) {
                fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                yield return null;
            }
            Require(damageNotifications == 2 && observedDamage == fireDamageValue,
                "The saved receipt must still play the same original damage once at its boundary.");
            fireViews.HitSelectionViewArrived -= fireObserver;
            fireBridge.Dispose(); fireStep.Dispose();
            fireViews.Tick(restoredCast.Presentation.Duration + .01f);
            yield return new WaitForSeconds(.6f);
            fireDisplayUnits[1].Hp = 1000000; fireDisplayUnits[1].Turn.Hp = 1000000;
            using var liveSession = new OfficialBattleLiveImpactSession(cp, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1);
            FightBattleSkillCastEvent sessionCast = null;
            FightBattleDamageEvent sessionDamage = null;
            EcEntityIdentifier sessionSkillId = default;
            var sessionGraphStarts = new List<(FightBattleSkillCastEvent Cast, EcEntityIdentifier Id, string Name)>();
            liveSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                if (prefab.name == "playable10800") sessionGraphStarts.Add((cast, id, leaf.name));
            };
            int startAttempts = 0;
            bool allowSessionStart = false, sessionArrival = false, sessionHpRefreshed = false, sessionCaptured = false;
            int beforeSessionFire = played.Count;
            bool StartSessionCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                startAttempts++;
                if (!allowSessionStart) { id = default; return false; }
                sessionCast = cast; id = fireViews.Start(cast, fireSource, fireTarget); sessionSkillId = id;
                return true;
            }
            void RefreshSessionHp(FightBattleDamageEvent damage) {
                sessionDamage = damage;
                fireDisplayUnits[1].Hp = damage.AfterHp; fireDisplayUnits[1].Turn.Hp = damage.AfterHp;
                sessionHpRefreshed = !liveSession.IsCompleted && fireUnits[1].Hp == 1000000
                    && damage.BeforeHp == 1000000 && damage.AfterHp < 1000000 && damage.HasDamageTriggerStatus
                    && !damage.IsDamageTriggerStatus && damageNotifications == 3 && observedDamage == damage.DamageValue
                    && played.Skip(beforeSessionFire).Any(value => value.Name == "playable10801" && value.Frame == Time.frameCount);
                Require(sessionHpRefreshed
                    && floatRoot.GetComponentsInChildren<Zjcs.Official.BattleDamageUI.CharacterDamageUI>().Any(ui =>
                        ui.EntityId == new EcEntityIdentifier(1, 2) && ui.GetComponentsInChildren<TextMesh>(true)
                            .Any(text => text.text == Math.Abs(observedDamage).ToString())),
                    "The live session must show actual HP and the original damage number in the impact frame.");
                Capture("session-fireball-damage.png"); sessionCaptured = true;
            }
            Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot, int, Pos2d, int> sessionObserver =
                (c, h, center, pos, cell) => { if (ReferenceEquals(c, sessionCast) && cell >= 0
                    && pos.Equals(new Pos2d(5, 5))) sessionArrival = true; };
            fireViews.HitSelectionViewArrived += sessionObserver;
            deadline = Time.realtimeSinceStartup + 10f;
            while (startAttempts == 0 && Time.realtimeSinceStartup < deadline) {
                liveSession.Pump(0f, StartSessionCast, RefreshSessionHp); yield return null;
            }
            Require(startAttempts > 0 && sessionCast == null && fireUnits[1].Hp == 1000000,
                "The live session must wait until the host actually starts the cast.");
            allowSessionStart = true;
            deadline = Time.realtimeSinceStartup + 10f;
            while (!liveSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                liveSession.Pump(Time.deltaTime, StartSessionCast, RefreshSessionHp);
                if (!sessionArrival) Require(fireUnits[1].Hp == 1000000 && damageNotifications == 2,
                    "The live fireball changed HP or displayed damage during flight.");
                fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                yield return null;
            }
            Require(liveSession.IsCompleted && sessionArrival && sessionHpRefreshed && sessionCaptured
                && sessionDamage != null && damageNotifications == 3
                && played.Skip(beforeSessionFire).Any(x => x.Name == "playable10801"),
                "The live session did not complete its original fireball impact.");
            Require(new[] { "anim", "effect" }.All(name => graphStarts.Any(value => ReferenceEquals(value.Cast, sessionCast)
                && value.Id.Equals(sessionSkillId) && value.Prefab.name == "playable10800" && value.Leaf.name == name)),
                "The original animation and delayed effect leaves must each retain their actual cast identity.");
            Require(new[] { "anim", "effect" }.All(name => sessionGraphStarts.Any(value => ReferenceEquals(value.Cast, sessionCast)
                && value.Id.Equals(sessionSkillId) && value.Name == name)),
                "The live session must forward the original action and effect callbacks for its exact cast.");
            yield return null;
            Capture("session-fireball-rendered.png");
            Require(liveSession.TryTakeCompleted(out var sessionExecutor, out var sessionResult)
                && ReferenceEquals(sessionExecutor.State, sessionResult.Checkpoint)
                && FightBattleCheckpointSerializer.Save(cp) == committedFire
                && FightBattleCheckpointSerializer.Save(sessionResult.Checkpoint)
                    == FightBattleCheckpointSerializer.Save(completedFire.Checkpoint),
                "The completed live session changed the committed checkpoint or deterministic result.");
            fireViews.Tick(sessionCast.Presentation.Duration + .01f);
            Require(fireViews.GetEntity(sessionSkillId) == null, "The live session did not end the original cast entity.");
            fireViews.HitSelectionViewArrived -= sessionObserver;
            liveSession.Dispose();

            // Keep the real fireball presentation, and attach an isolated status application
            // to exercise the incomplete-to-actual result handoff with Unity's native views.
            var statusCp = FightBattleCheckpointSerializer.Load(committedFire);
            var statusSkill = statusCp.Units.Single(unit => unit.Id == 1).Skills.Single(value => value.ItemId == 10028);
            statusSkill.HitGraphEntities.Single(value => value.EntityId == 10801).AppliedStatuses.Add(new FightBattleStatusApplication {
                StatusId = 24031, Chance = 1, AffectedByProp = false, LessThanHpPer = -1f });
            string statusCheckpoint = FightBattleCheckpointSerializer.Save(statusCp);
            var statusBaseline = FightBattleCheckpointSerializer.Load(statusCheckpoint);
            var statusBaselineResult = FightBattleExecutor.Restore(statusBaseline, formula, new FightDamageSettlement(),
                liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
            var expectedStatusDamage = statusBaselineResult.Event.DamageEvents.Single(value => value.SkillItemId == 10028);
            Require(expectedStatusDamage.IsDamageTriggerStatus, "The status handoff fixture must actually apply its status.");
            fireDisplayUnits[1].Hp = 1000000; fireDisplayUnits[1].Turn.Hp = 1000000;
            int beforeStatusFire = played.Count;
            bool statusDamageShown = false;
            FightBattleSkillCastEvent statusCast = null;
            using var statusSession = new OfficialBattleLiveImpactSession(statusCp, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1);
            bool StartStatusCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                statusCast = cast; id = fireViews.Start(cast, fireSource, fireTarget); return true;
            }
            void RefreshStatusHp(FightBattleDamageEvent damage) {
                fireDisplayUnits[1].Hp = damage.AfterHp; fireDisplayUnits[1].Turn.Hp = damage.AfterHp;
                statusDamageShown = !damage.HasDamageTriggerStatus
                    && damage.DamageValue == expectedStatusDamage.DamageValue && damageNotifications == 4
                    && played.Skip(beforeStatusFire).Any(value => value.Name == "playable10801" && value.Frame == Time.frameCount);
                Require(statusDamageShown, "The known native damage must reach the impact frame before status resolution.");
            }
            deadline = Time.realtimeSinceStartup + 10f;
            while (!statusSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                statusSession.Pump(Time.deltaTime, StartStatusCast, RefreshStatusHp);
                Require(!statusSession.StatusResolutionBlocked, "The simple status unexpectedly started a nested presentation.");
                fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                yield return null;
            }
            Require(statusDamageShown && statusSession.IsCompleted && statusCast != null
                && statusSession.TryTakeCompleted(out _, out var statusResult)
                && statusResult.Event.DamageEvents.Single(value => value.SkillItemId == 10028).IsDamageTriggerStatus
                && FightBattleCheckpointSerializer.Save(statusCp) == statusCheckpoint
                && FightBattleCheckpointSerializer.Save(statusResult.Checkpoint) == FightBattleCheckpointSerializer.Save(statusBaseline),
                "The live status result changed the original checkpoint or deterministic outcome.");
            statusSession.Dispose();
            fireViews.Tick(statusCast.Presentation.Duration + .01f);
            yield return new WaitForSeconds(.6f);
            var jumpUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 4) };
            var jumpSkill = liveCatalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10670, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            jumpSkill.LimitedTimes = 1; jumpUnits[0].Skills.Add(jumpSkill); jumpUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(jumpSkill, 0));
            var jumpCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            jumpCheckpoint.Units.AddRange(jumpUnits); var jumpWave = new FightBattleWaveState(); jumpWave.UnitIds.Add(2); jumpCheckpoint.Waves.Add(jumpWave);
            FightBattleExecutor.Begin(jumpCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            string jumpCommitted = FightBattleCheckpointSerializer.Save(jumpCheckpoint);
            fireDisplayUnits = FightBattleCheckpointSerializer.Load(jumpCommitted).Units.ToArray();
            var jumpBaseline = FightBattleCheckpointSerializer.Load(jumpCommitted);
            var jumpExpected = FightBattleExecutor.Restore(jumpBaseline, formula, new FightDamageSettlement(),
                liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
            var jumpExpectedDamage = jumpExpected.Event.DamageEvents.Single(value => value.SkillItemId == 10015);
            NativeJumpAppearanceFixture jumpAppearance = null;
            Matrix4x4[] jumpInitialPose = null;
            bool jumpActorCaptured = false, jumpPoseChanged = false, jumpClipObserved = false;
            if (OfficialBattleHitPlaybackAcceptance.PlayerJump)
            {
                DestroyImmediate(fireSource.GetComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>());
                DestroyImmediate(fireSource.GetComponent<MeshRenderer>());
                jumpAppearance = NativeJumpAppearanceFixture.Create(fireSource.gameObject);
                yield return null;
                Capture("session-jump-actor-idle.png", false);
                jumpInitialPose = Enumerable.Range(0, jumpAppearance.Native.State.Pose.Count)
                    .Select(i => jumpAppearance.Native.State.Pose.World(i)).ToArray();
            }
            fireSource.localPosition = new Vector3(2, 5, 0);
            fireTarget.localPosition = new Vector3(4, 5, 0);
            FightBattleSkillCastEvent jumpCast = null;
            int jumpImpactFrame = -1;
            bool jumpDamageShown = false, jumpMovementStarted = false;
            float jumpCastStarted = 0f;
            Vector3 jumpLanding = default;
            using var jumpSession = new OfficialBattleLiveImpactSession(jumpCheckpoint, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1, observeMovements: true);
            jumpSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                if (ReferenceEquals(cast, jumpCast) && prefab.name == "playable10670" && leaf.name == "effect (2)") {
                    jumpImpactFrame = Time.frameCount;
                    Require(jumpMovementStarted && Vector3.Distance(fireSource.localPosition, jumpLanding) < .05f,
                        "The original jump must reach its selected landing before the strike effect.");
                }
            };
            bool StartJumpCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                jumpCast = cast; jumpCastStarted = Time.time;
                id = fireViews.Start(cast, fireSource, fireTarget); return true;
            }
            bool StartJumpMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement) {
                Require(ReferenceEquals(cast, jumpCast) && movement.MoveBehaviourId == 10672 && movement.Path.Count == 2,
                    "The live jump must use its actual original movement action and direct path.");
                if (Time.time - jumpCastStarted < movement.Delay) return false;
                var landing = movement.Path[movement.Path.Count - 1];
                jumpLanding = fireViews.GridToPos(new Pos2d(landing.X, landing.Y));
                Require(fireViews.OwnsMovement(movement), "The original jump movement prefab is unavailable.");
                fireViews.StartMovement(movement, fireSource); jumpMovementStarted = true; return true;
            }
            void RefreshJumpHp(FightBattleDamageEvent damage) {
                fireDisplayUnits[1].Hp = damage.AfterHp; fireDisplayUnits[1].Turn.Hp = damage.AfterHp;
                jumpDamageShown = jumpImpactFrame == Time.frameCount && !damage.HasDamageTriggerStatus
                    && damage.DamageValue == jumpExpectedDamage.DamageValue && damageNotifications == 5
                    && observedDamage == damage.DamageValue;
                Require(jumpDamageShown, "The jump's actual damage must use its original landing-effect frame.");
            }
            deadline = Time.realtimeSinceStartup + 10f;
            while (!jumpSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                jumpSession.Pump(Time.deltaTime, StartJumpCast, RefreshJumpHp, StartJumpMovement);
                if (jumpImpactFrame < 0) Require(damageNotifications == 4 && fireDisplayUnits[1].Hp == 1000000,
                    "Jump damage must wait for the original landing effect.");
                fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                if (jumpAppearance != null && jumpMovementStarted)
                {
                    jumpClipObserved |= jumpAppearance.Animator.GetCurrentAnimatorClipInfo(0)
                        .Any(entry => entry.clip && entry.clip.name == "100/skill_02");
                    jumpPoseChanged |= Enumerable.Range(0, jumpInitialPose.Length)
                        .Any(i => jumpInitialPose[i] != jumpAppearance.Native.State.Pose.World(i));
                    if (!jumpActorCaptured && jumpClipObserved && jumpPoseChanged)
                    {
                        jumpActorCaptured = true;
                        Capture("session-jump-actor-motion.png", false);
                    }
                }
                yield return null;
            }
            if (jumpAppearance != null)
                Require(jumpActorCaptured && jumpClipObserved && jumpPoseChanged,
                    "The original jump clip must animate the actual player skeleton during movement.");
            Require(jumpDamageShown && jumpSession.IsCompleted && jumpSession.TryTakeCompleted(out _, out var jumpResult)
                && FightBattleCheckpointSerializer.Save(jumpCheckpoint) == jumpCommitted
                && FightBattleCheckpointSerializer.Save(jumpResult.Checkpoint) == FightBattleCheckpointSerializer.Save(jumpBaseline),
                "The original jump impact must complete without changing the committed checkpoint or rules result.");
            yield return null;
            Capture("session-jump-impact.png");
            // A fatal-prevention status starts a recovery skill while the jump's
            // own status result is still being resolved.
            fireViews.Tick(jumpCast.Presentation.Duration + .01f);
            yield return new WaitForSeconds(1.5f);
            var nestedUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 4) };
            nestedUnits[1].Hp = nestedUnits[1].Turn.Hp = 1;
            nestedUnits[1].Statuses.Add(liveCatalog.CreateStatus(25013, 2, 2, 1, 25013, 10518, 6, 17, 1));
            var nestedSkill = liveCatalog.CreateSkill(10670, 0, 10015, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10670, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            nestedSkill.LimitedTimes = 1; nestedUnits[0].Skills.Add(nestedSkill);
            nestedUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(nestedSkill, 0));
            var nestedCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            nestedCheckpoint.Units.AddRange(nestedUnits); var nestedWave = new FightBattleWaveState(); nestedWave.UnitIds.Add(2);
            nestedCheckpoint.Waves.Add(nestedWave);
            FightBattleExecutor.Begin(nestedCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            string nestedCommitted = FightBattleCheckpointSerializer.Save(nestedCheckpoint);
            var nestedBaseline = FightBattleCheckpointSerializer.Load(nestedCommitted);
            FightBattleExecutor.Restore(nestedBaseline, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
            fireDisplayUnits = FightBattleCheckpointSerializer.Load(nestedCommitted).Units.ToArray();
            fireSource.localPosition = new Vector3(2, 5, 0); fireTarget.localPosition = new Vector3(4, 5, 0);
            int nestedJumpFrame = -1, nestedCureFrame = -1; var nestedDamageSkills = new List<int>();
            Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot, int, Pos2d, int> nestedHitObserver =
                (cast, hit, center, position, scopeIndex) => { if (cast.SkillEntityId == 10990) nestedCureFrame = Time.frameCount; };
            fireViews.HitSelectionViewArrived += nestedHitObserver;
            using (var nestedSession = new OfficialBattleLiveImpactSession(nestedCheckpoint, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1, observeMovements: true))
            {
                nestedSession.SkillGraphPlayableStarted += (cast, id, prefab, playable) => {
                    if (cast.SkillEntityId == 10670 && prefab && prefab.name == "playable10670" && playable && playable.name == "effect (2)")
                        nestedJumpFrame = Time.frameCount;
                };
                bool StartNested(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                    var source = cast.ActorId == 1 ? fireSource : fireTarget;
                    var destination = cast.Presentation.TargetId == 1 ? fireSource : fireTarget;
                    id = fireViews.Start(cast, source, destination); return true;
                }
                bool MoveNested(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement) {
                    fireViews.StartMovement(movement, movement.UnitId == 1 ? fireSource : fireTarget); return true;
                }
                void ShowNested(FightBattleDamageEvent damage) {
                    nestedDamageSkills.Add(damage.SkillEntityId);
                    fireDisplayUnits[1].Hp = damage.AfterHp; fireDisplayUnits[1].Turn.Hp = damage.AfterHp;
                    Require(damage.SkillEntityId == 10670 ? nestedJumpFrame == Time.frameCount && !damage.HasDamageTriggerStatus
                        : damage.SkillEntityId == 10990 && nestedCureFrame == Time.frameCount,
                        "Parent damage and recovery must each appear at their own original impact.");
                    if (jumpAppearance != null)
                        Capture(damage.SkillEntityId == 10670 ? "nested-fatal-hit.png" : "nested-fatal-recovery.png", false);
                }
                deadline = Time.realtimeSinceStartup + 20f;
                while (!nestedSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    nestedSession.Pump(Time.deltaTime, StartNested, ShowNested, MoveNested);
                    fireViews.Tick(Time.deltaTime); fireScope.LateTick(); yield return null;
                }
                Require(nestedSession.IsCompleted && nestedSession.TryTakeCompleted(out _, out var nestedResult)
                    && nestedDamageSkills.SequenceEqual(new[] { 10670, 10990 })
                    && FightBattleCheckpointSerializer.Save(nestedResult.Checkpoint) == FightBattleCheckpointSerializer.Save(nestedBaseline)
                    && FightBattleCheckpointSerializer.Save(nestedCheckpoint) == nestedCommitted,
                    "Nested fatal recovery must finish in rule order without changing the committed checkpoint.");
            }
            fireViews.HitSelectionViewArrived -= nestedHitObserver;
            // The original status-only damage playable must still run once if
            // its result arrives after the enclosing skill entity has ended.
            fireDisplayUnits[1].Hp = fireDisplayUnits[1].Turn.Hp = 1000000;
            foreach (bool statusApplied in new[] { false, true })
            {
                var statusEffectSkill = liveCatalog.CreateSkill(13010, -1, 0, 1, 1, 0, null);
                var statusEffectCast = new FightBattleSkillCastEvent { ActorId = 1, Slot = -1, SkillEntityId = 13010,
                    Presentation = FightBattleSkillCastPresentation.Capture(fireDisplayUnits[0], statusEffectSkill,
                        new List<FightBattleUnitState> { fireDisplayUnits[1] }) };
                statusEffectCast.Presentation.Duration = statusEffectSkill.Duration;
                int damageId = statusApplied ? 98766 : 98765;
                var statusEffectDamage = new FightBattleDamageEvent { DamageId = damageId, DamageEntityId = 13012,
                    SourceId = 1, TargetId = 2, SkillEntityId = 13010, HasDamagePosition = true, DamageX = 4, DamageY = 5,
                    HasDamageValue = true, DamageValue = 10, HasAbsoluteDamage = true, AbsoluteDamage = 10,
                    BeforeHp = 1000000, AfterHp = 999990, MaxHp = 1000000 };
                int beforeEffect = played.Count, beforeDamage = damageNotifications;
                var statusEffectId = fireViews.Start(statusEffectCast, fireSource, fireTarget);
                statusEffectCast.DamageEventIds.Add(damageId);
                Require(fireViews.QueueUnresolvedDamage(statusEffectCast, statusEffectId, statusEffectDamage)
                    && damageNotifications == beforeDamage + 1 && !played.Skip(beforeEffect).Any(value => value.Name == "playable13012"),
                    "Unresolved status must show the known damage without inventing its conditional effect.");
                fireViews.Tick(1f); fireScope.LateTick();
                Require(fireViews.GetEntity(statusEffectId) == null, "The original skill lifetime must remain independent of status resolution.");
                fireViews.CompleteLiveCast(statusEffectCast, statusEffectId, 1f);
                statusEffectDamage.HasDamageTriggerStatus = true; statusEffectDamage.IsDamageTriggerStatus = statusApplied;
                Require(fireViews.ResolveDamageStatus(statusEffectDamage)
                    && !fireViews.ResolveDamageStatus(statusEffectDamage)
                    && played.Skip(beforeEffect).Count(value => value.Name == "playable13012") == (statusApplied ? 1 : 0)
                    && damageNotifications == beforeDamage + 1,
                    "The actual conditional status effect must play once without repeating damage.");
                fireViews.Tick(0f);
            }
            if (jumpAppearance != null)
            {
                // A blocked movement has a one-point original path at its starting
                // grid. Its actual blocked graph moves to the collision and returns.
                var blocked = new FightBattleMovementEvent { UnitId = 1, MoveBehaviourId = 11123,
                    HasDirection = true, Direction = 3, HasFacing = true, EndFlipX = false };
                blocked.Path.Add(new FightGridPoint(3, 5)); blocked.Path.Add(new FightGridPoint(3, 5));
                fireSource.localPosition = new Vector3(3, 5, 0);
                Require(fireViews.OwnsMovement(blocked), "Original blocked movement action is available.");
                using var collisionLatch = new FightBattleImpactLatch();
                typeof(FightBattleImpactLatch).GetProperty("ObserveMovements").GetSetMethod(true)
                    .Invoke(collisionLatch, new object[] { true });
                var bindWorker = typeof(FightBattleImpactLatch).GetMethod("BindWorker", BindingFlags.Instance | BindingFlags.NonPublic);
                var finishWorker = typeof(FightBattleImpactLatch).GetMethod("FinishWorker", BindingFlags.Instance | BindingFlags.NonPublic);
                var collisionHit = typeof(FightBattleHitExecutor).GetMethod("ApplyKnockbackCollision", BindingFlags.Instance | BindingFlags.NonPublic);
                var collisionExecutor = new FightBattleHitExecutor(formula, new FightDamageSettlement());
                var collisionSource = new FightBattleUnitState { Id = 1, Hp = 1000, MaxHp = 1000 };
                var collisionTarget = new FightBattleUnitState { Id = 2, Hp = 1000, MaxHp = 1000 };
                var collisionSkill = new FightBattleSkillDefinition { EntityId = 10100 };
                var collisionDefinition = new FightBattleHitDefinition {
                    EntityId = 10101, DamageId = 10006, DamageType = 8, ActionType = 4 };
                var collisionCast = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 10100 };
                collisionCast.TargetIds.Add(2);
                var collisionSelection = new FightBattleHitSelectionSnapshot { HitEntityId = 10101 };
                collisionSelection.EffectCenters.Add(new FightGridPoint(4, 5));
                collisionSelection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2 });
                collisionCast.HitSelections.Add(collisionSelection);
                var collisionImpact = new FightBattlePendingImpact { Cast = collisionCast,
                    HitSelection = collisionSelection, SourceId = 1, TargetId = 2, SkillEntityId = 10100,
                    HitEntityId = 10101, DamageEntityId = 10006 };
                Require(collisionHit != null, "The local collision settlement must be available.");
                int collisionSettled = 0;
                var collisionWorker = Task.Run(() => {
                    bindWorker.Invoke(collisionLatch, null);
                    try {
                        collisionLatch.BlockedMovementPrepared(null, blocked, new FightGridPoint(4, 5));
                        collisionLatch.AwaitBlockedContact(blocked);
                        collisionLatch.BindBlockedImpact(blocked, collisionCast, 2);
                        collisionLatch.AwaitImpact(collisionImpact);
                        collisionHit.Invoke(collisionExecutor, new object[] { new FightRandomStream(1), 1,
                            collisionSource, collisionTarget, collisionSkill, collisionDefinition, 500L, 1 });
                        Interlocked.Exchange(ref collisionSettled, 1);
                    }
                    finally { finishWorker.Invoke(collisionLatch, null); }
                });
                FightBattlePreparedMovement blockedPreparation = null;
                Require(SpinWait.SpinUntil(() => collisionLatch.TryTakeMovement(out blockedPreparation), 20000)
                    && blockedPreparation.HasBlockedPosition
                    && blockedPreparation.BlockedPosition.Equals(new FightGridPoint(4, 5)),
                    "The isolated rules worker must offer the original blocked position.");
                int blockedBefore = played.Count;
                int contacts = 0;
                float contactX = 0f;
                fireViews.StartMovement(blocked, fireSource, new FightGridPoint(4, 5), value => {
                    Require(ReferenceEquals(value, blocked), "Collision must retain the original movement identity.");
                    contacts++; contactX = fireSource.localPosition.x;
                    Require(collisionLatch.RecordBlockedContact(value, out long collisionRevision) && collisionRevision > 0,
                        "The actual collision must release its own waiting rules worker.");
                    collisionLatch.WaitForWorkerBoundary(collisionRevision);
                    Require(Volatile.Read(ref collisionSettled) == 1 && collisionTarget.Hp == 900,
                        "Collision damage must settle at the actual contact in the same frame.");
                });
                collisionLatch.CompleteMovement(blockedPreparation, out long startRevision);
                collisionLatch.WaitForWorkerBoundary(startRevision);
                Require(Volatile.Read(ref collisionSettled) == 0 && collisionTarget.Hp == 1000,
                    "Collision rules cannot run at movement start.");
                float farthest = fireSource.localPosition.x;
                bool captured = false;
                for (int frame = 0; frame < 100; frame++)
                {
                    fireViews.Tick(.01f); fireScope.LateTick();
                    if (contacts == 0) Require(Volatile.Read(ref collisionSettled) == 0 && collisionTarget.Hp == 1000,
                        "Collision damage must wait for the original contact frame.");
                    farthest = Mathf.Max(farthest, fireSource.localPosition.x);
                    if (!captured && farthest > 3.79f)
                    { Capture("session-collision.png", false); captured = true; }
                    yield return null;
                }
                Require(captured && contacts == 1 && contactX > 3.78f && contactX < 3.82f
                    && Mathf.Abs(fireSource.localPosition.x - 3f) < .01f
                    && played.Skip(blockedBefore).Any(value => value.Name == "playable_move_blocked")
                    && fireRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length == 0,
                    "Original blocked movement must signal its actual collision once and return before removal.");
                collisionWorker.GetAwaiter().GetResult();
                jumpSession.Dispose();

                var mountainUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 4), LiveUnit(3, 2, 5) };
                mountainUnits[0].Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
                var mountainSkill = liveCatalog.CreateSkill(11120, 0, 10052, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(11120, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                mountainSkill.LimitedTimes = 1; mountainUnits[0].Skills.Add(mountainSkill);
                mountainUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(mountainSkill, 0));
                var mountainCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                mountainCheckpoint.Units.AddRange(mountainUnits); var mountainWave = new FightBattleWaveState();
                mountainWave.UnitIds.Add(2); mountainWave.UnitIds.Add(3); mountainCheckpoint.Waves.Add(mountainWave);
                FightBattleExecutor.Begin(mountainCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                string mountainCommitted = FightBattleCheckpointSerializer.Save(mountainCheckpoint);
                fireDisplayUnits = FightBattleCheckpointSerializer.Load(mountainCommitted).Units.ToArray();
                fireSource.localPosition = new Vector3(2, 5, 0); fireTarget.localPosition = new Vector3(4, 5, 0);
                var blockerView = new GameObject("Mountain collision blocker").transform;
                blockerView.SetParent(fireRoot, false); blockerView.localPosition = new Vector3(5, 5, 0);
                blockerView.gameObject.AddComponent<MyTransform>();
                var blockerRenderer = blockerView.gameObject.AddComponent<MeshRenderer>(); blockerRenderer.enabled = false;
                blockerRenderer.sharedMaterials = Array.Empty<Material>();
                blockerView.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = blockerRenderer;
                int blockerDamageFrame = -1; long blockerDamageValue = 0;
                fireViews.EnsureUnit(3, blockerView).GetComponent<MyFightRoleFight>().OnDamage += (role, damage) => {
                    blockerDamageFrame = Time.frameCount; blockerDamageValue = damage.Component.DamageResult.DamageValue;
                };
                int mountainContactFrame = -1, mountainHitFrame = -1, mountainChildCount = 0, mountainPlayedBefore = played.Count;
                ParticleSystem mountainSecondWave = null;
                Transform MountainBody(int unitId) => unitId == 1 ? fireSource : unitId == 2 ? fireTarget : unitId == 3 ? blockerView : null;
                using var mountainDriver = new OfficialBattleLiveGraphDriver(mountainCheckpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews,
                    entity => entity == 11120 || entity == 10100, MountainBody, null, MountainDamage);
                int mountainAirStarts = 0;
                mountainDriver.SecondsStatusChanged += value => {
                    if (value.OwnerId == 2 && value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24269)
                        mountainAirStarts++;
                };
                fireDisplayUnits = mountainCheckpoint.Units.Select(value => mountainDriver.DisplayUnit(value.Id)).ToArray();
                mountainDriver.SkillGraphPlayableStarted += (mountainCast, id, prefab, leaf) => {
                    if (mountainCast.SkillEntityId != 11120 || prefab.name != "playable11120" || leaf.name != "effect") return;
                    mountainSecondWave = leaf.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "dierchong");
                };
                mountainDriver.BlockedContact += move => {
                    Require(move.UnitId == 2, "Wrong original mountain collision action.");
                    mountainContactFrame = Time.frameCount;
                };
                void MountainDamage(FightBattleDamageEvent damage, FightBattleUnitState projected) {
                    int displayedIndex = Array.FindIndex(fireDisplayUnits, value => value.Id == damage.TargetId);
                    fireDisplayUnits[displayedIndex] = projected;
                    if (damage.SkillEntityId == 11120) {
                        mountainHitFrame = Time.frameCount;
                        Require(mountainSecondWave && mountainSecondWave.particleCount > 0,
                            "The outer mountain hit must wait for its original visible particle wave.");
                    }
                    if (damage.SkillEntityId != 10100) return;
                    Require(mountainContactFrame == Time.frameCount && damage.TrueDamage && damage.Damage == -600
                        && (damage.TargetId == 2 ? observedDamage == -600
                            : damage.TargetId == 3 && blockerDamageFrame == Time.frameCount && blockerDamageValue == -600),
                        "The actual mountain collision child and damage display must reach the native contact frame.");
                    mountainChildCount++;
                    if (mountainChildCount == 2) Capture("mountain-collision-impact.png", false);
                }
                deadline = Time.realtimeSinceStartup + 20f;
                while (!mountainDriver.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    mountainDriver.Pump(Time.deltaTime);
                    if (mountainSecondWave && mountainSecondWave.particleCount == 0)
                        Require(mountainHitFrame < 0 && fireDisplayUnits[1].Hp == 1000000,
                            "Outer mountain damage cannot precede the visible wave.");
                    fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                    yield return null;
                }
                Require(mountainHitFrame >= 0 && mountainChildCount == 2 && mountainAirStarts == 1 && mountainDriver.TryTakeCompleted(out _, out var mountainResult)
                    && mountainResult.Event.DamageEvents.Count(value => value.SkillEntityId == 10100) == 2
                    && FightBattleCheckpointSerializer.Save(mountainCheckpoint) == mountainCommitted,
                    "The real mountain action must finish both collision child hits on its isolated checkpoint: completed="
                    + mountainDriver.IsCompleted + ", children=" + mountainChildCount + ", contact=" + mountainContactFrame
                    + ", pending movement=" + (mountainDriver.PendingMovement != null)
                    + ", pending settlement=" + (mountainDriver.PendingSettlement != null) + ", blocked status=" + mountainDriver.StatusResolutionBlocked
                    + ", played=" + string.Join(",", played.Skip(mountainPlayedBefore).Select(value => value.Name)));
                mountainDriver.Dispose();
                for (int frame = 0; frame < 200 && fireRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length != 0; frame++) {
                    fireViews.Tick(.01f); fireScope.LateTick(); yield return null;
                }
                Require(fireRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length == 0,
                    "The real blocked movement must finish before the interruption fixture starts.");

                var nearUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 3) };
                nearUnits[1].Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = 20000 });
                var nearSkill = liveCatalog.CreateSkill(11120, 0, 10052, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(11120, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                nearSkill.LimitedTimes = 1; nearUnits[0].Skills.Add(nearSkill);
                nearUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(nearSkill, 0));
                var nearCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                nearCheckpoint.Units.AddRange(nearUnits); var nearWave = new FightBattleWaveState();
                nearWave.UnitIds.Add(2); nearCheckpoint.Waves.Add(nearWave);
                FightBattleExecutor.Begin(nearCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                fireDisplayUnits = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(nearCheckpoint)).Units.ToArray();
                fireSource.localPosition = new Vector3(2, 5, 0); fireTarget.localPosition = new Vector3(3, 5, 0);
                ParticleSystem nearFirst = null, nearSecond = null; bool nearDamage = false, nearPaused = false, nearResumed = false;
                int pauseFrames = 0;
                using var nearSession = new OfficialBattleLiveImpactSession(nearCheckpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1, observeMovements: true);
                nearSession.SecondsStatusChanged += _ => { };
                nearSession.SkillGraphPlayableStarted += (nearCast, id, prefab, leaf) => {
                    if (nearCast.SkillEntityId != 11120 || prefab.name != "playable11120" || leaf.name != "effect") return;
                    var particles = leaf.GetComponentsInChildren<ParticleSystem>(true);
                    nearFirst = particles.Single(value => value.name == "diyichon");
                    nearSecond = particles.Single(value => value.name == "dierchong");
                };
                bool StartNearCast(FightBattleSkillCastEvent nearCast, out EcEntityIdentifier id) {
                    id = fireViews.Start(nearCast, fireSource, fireTarget); return true;
                }
                void NearDamage(FightBattleDamageEvent damage) {
                    nearDamage = nearFirst && nearFirst.particleCount > 0 && nearSecond && nearSecond.particleCount == 0;
                    Require(nearDamage, "The inner mountain hit must coincide with the first visible wave.");
                    fireDisplayUnits[1].Hp = damage.AfterHp; fireDisplayUnits[1].Turn.Hp = damage.AfterHp;
                }
                deadline = Time.realtimeSinceStartup + 20f;
                while (!nearSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    nearSession.Pump(Time.deltaTime, StartNearCast, NearDamage);
                    if (nearFirst && !nearPaused) {
                        Require(nearFirst.particleCount == 0 && nearSecond.particleCount == 0,
                            "The pause fixture must start before either original wave.");
                        nearFirst.Pause(); nearSecond.Pause(); nearPaused = true;
                    }
                    if (nearPaused && !nearResumed) {
                        Require(!nearDamage && nearFirst.particleCount == 0 && nearSecond.particleCount == 0,
                            "Paused original particle waves cannot trigger damage.");
                        if (++pauseFrames == 5) { nearFirst.Play(); nearSecond.Play(); nearResumed = true; }
                    }
                    if (nearFirst && nearFirst.particleCount == 0)
                        Require(!nearDamage && fireDisplayUnits[1].Hp == 1000000, "Inner damage preceded its visible wave.");
                    fireViews.Tick(Time.deltaTime); fireScope.LateTick(); yield return null;
                }
                Require(nearDamage && nearPaused && nearResumed && nearSession.TryTakeCompleted(out _, out _),
                    "The resumed inner wave did not complete its actual hit.");
                nearSession.Dispose();

                string canceledCheckpoint = FightBattleCheckpointSerializer.Save(nearCheckpoint);
                fireDisplayUnits = FightBattleCheckpointSerializer.Load(canceledCheckpoint).Units.ToArray();
                ParticleSystem canceledFirst = null; bool canceledDamage = false;
                using var canceledSession = new OfficialBattleLiveImpactSession(nearCheckpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1, observeMovements: true);
                canceledSession.SkillGraphPlayableStarted += (canceledCast, id, prefab, leaf) => {
                    if (canceledCast.SkillEntityId == 11120 && prefab.name == "playable11120" && leaf.name == "effect")
                        canceledFirst = leaf.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "diyichon");
                };
                deadline = Time.realtimeSinceStartup + 10f;
                while (!canceledFirst && Time.realtimeSinceStartup < deadline) {
                    canceledSession.Pump(Time.deltaTime, StartNearCast, _ => canceledDamage = true);
                    fireViews.Tick(Time.deltaTime); fireScope.LateTick(); yield return null;
                }
                Require(canceledFirst && canceledFirst.particleCount == 0 && !canceledDamage,
                    "The interruption fixture must stop before the first original wave.");
                canceledSession.Dispose();
                deadline = Time.realtimeSinceStartup + 10f;
                while (canceledFirst && canceledFirst.particleCount == 0 && Time.realtimeSinceStartup < deadline) {
                    fireViews.Tick(Time.deltaTime); fireScope.LateTick(); yield return null;
                }
                Require(canceledFirst && canceledFirst.particleCount > 0 && !canceledDamage
                    && FightBattleCheckpointSerializer.Save(nearCheckpoint) == canceledCheckpoint,
                    "An interrupted session cannot settle damage when its former wave later appears.");

                foreach (bool blockedDiagonal in new[] { false, true })
                {
                    var diagonalUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 3), LiveUnit(3, 2, blockedDiagonal ? 4 : 11),
                        LiveUnit(4, 2, 4), LiveUnit(5, 2, 3) };
                    diagonalUnits[1].Y = 6; diagonalUnits[2].Y = blockedDiagonal ? 7 : 11;
                    diagonalUnits[3].Y = 6; diagonalUnits[4].Y = 7;
                    diagonalUnits[0].Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
                    var diagonalSkill = liveCatalog.CreateSkill(11120, 0, 10052, 2, 20, 1,
                        liveCatalog.CalculateSkillProps(11120, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                    diagonalSkill.LimitedTimes = 1; diagonalUnits[0].Skills.Add(diagonalSkill);
                    diagonalUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(diagonalSkill, 0));
                    var diagonalCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                        Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                    diagonalCheckpoint.Units.AddRange(diagonalUnits); var diagonalWave = new FightBattleWaveState();
                    diagonalWave.UnitIds.AddRange(new[] { 2, 3, 4, 5 }); diagonalCheckpoint.Waves.Add(diagonalWave);
                    FightBattleExecutor.Begin(diagonalCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                    fireDisplayUnits = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(diagonalCheckpoint)).Units.ToArray();
                    fireSource.localPosition = new Vector3(2, 5, 0); fireTarget.localPosition = new Vector3(3, 6, 0);
                    blockerView.localPosition = new Vector3(blockedDiagonal ? 4 : 11, blockedDiagonal ? 7 : 11, 0);
                    int diagonalContactFrame = -1, diagonalChildren = 0; bool diagonalMovement = false, diagonalPoseChanged = false;
                    using var diagonalSession = new OfficialBattleLiveImpactSession(diagonalCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), fireViews, 0, 1, observeMovements: true);
                    diagonalSession.SecondsStatusChanged += _ => { };
                    bool StartDiagonalCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                        id = fireViews.Start(cast, fireSource, cast.TargetIds.Contains(3) ? blockerView : fireTarget); return true;
                    }
                    bool StartDiagonalMove(FightBattleSkillCastEvent cast, FightBattleMovementEvent move) {
                        Require(!blockedDiagonal && move.UnitId == 2 && move.Path.Count == 2
                            && move.Path[1].Equals(new FightGridPoint(4, 7)), "Open diagonal push must use the actual destination.");
                        fireViews.StartMovement(move, fireTarget); diagonalMovement = true; return true;
                    }
                    bool StartDiagonalBlocked(FightBattleSkillCastEvent cast, FightBattleMovementEvent move,
                        FightGridPoint position, Action contact) {
                        Require(blockedDiagonal && move.UnitId == 2 && position.Equals(new FightGridPoint(4, 7)),
                            "Blocked diagonal must retain its actual destination.");
                        fireViews.StartMovement(move, fireTarget, position, _ => {
                            diagonalContactFrame = Time.frameCount; contact();
                            Require(diagonalChildren == 2, "Both diagonal collision results must arrive at contact.");
                            Capture("mountain-diagonal-collision.png", false);
                        });
                        diagonalMovement = true; return true;
                    }
                    void DiagonalDamage(FightBattleDamageEvent damage) {
                        var displayed = fireDisplayUnits.Single(value => value.Id == damage.TargetId);
                        displayed.Hp = damage.AfterHp; displayed.Turn.Hp = damage.AfterHp;
                        if (damage.SkillEntityId != 10100) return;
                        Require(blockedDiagonal && diagonalContactFrame == Time.frameCount && damage.Damage == -600,
                            "Diagonal collision damage must settle at the original contact.");
                        diagonalChildren++;
                    }
                    deadline = Time.realtimeSinceStartup + 20f;
                    while (!diagonalSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                        diagonalSession.Pump(Time.deltaTime, StartDiagonalCast, DiagonalDamage, StartDiagonalMove, StartDiagonalBlocked);
                        fireViews.Tick(Time.deltaTime); fireScope.LateTick();
                        diagonalPoseChanged |= fireTarget.localPosition.x > 3.03f && fireTarget.localPosition.y > 6.03f;
                        yield return null;
                    }
                    Require(diagonalMovement && diagonalSession.TryTakeCompleted(out _, out var diagonalResult)
                        && diagonalChildren == (blockedDiagonal ? 2 : 0)
                        && diagonalResult.Checkpoint.Units.Single(value => value.Id == 2).X == (blockedDiagonal ? 3 : 4)
                        && diagonalResult.Checkpoint.Units.Single(value => value.Id == 2).Y == (blockedDiagonal ? 6 : 7),
                        "Original diagonal movement and collision did not complete correctly.");
                    diagonalSession.Dispose();
                    for (int frame = 0; frame < 200 && fireRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length != 0; frame++) {
                        fireViews.Tick(.01f); fireScope.LateTick();
                        diagonalPoseChanged |= fireTarget.localPosition.x > 3.03f && fireTarget.localPosition.y > 6.03f;
                        yield return null;
                    }
                    var expectedDiagonalPosition = blockedDiagonal ? new Vector3(3, 6, 0) : new Vector3(4, 7, 0);
                    Require(diagonalPoseChanged && Vector3.Distance(fireTarget.localPosition, expectedDiagonalPosition) < .05f,
                        "The native diagonal action must visibly travel and finish at its actual settled position.");
                }
                blockedBefore = played.Count;
                fireSource.localPosition = new Vector3(3, 5, 0);
                fireViews.StartMovement(blocked, fireSource, new FightGridPoint(4, 5), _ => contacts++);
                fireViews.Tick(0f); fireScope.LateTick();
                Require(played.Skip(blockedBefore).Any(value => value.Name == "playable_move_blocked"),
                    "The second original blocked movement starts before interruption: played="
                    + string.Join(",", played.Skip(blockedBefore).Select(value => value.Name))
                    + ", actions=" + fireRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length);
                fireViews.Dispose();
                Require(contacts == 1, "Interrupting the original blocked track must not signal contact.");
            }
            jumpAppearance?.Dispose();
            jumpSession.Dispose(); fireViews.Dispose(); Destroy(fireRoot.gameObject);

            // Super-heavy chop has no hit prefab effect. Its original second
            // slash contains the ground impact that coincides with the hit.
            foreach (var heavyDirection in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1) }) {
            string heavyDirectionName = heavyDirection.x > 0 ? "right" : heavyDirection.x < 0 ? "left" : heavyDirection.y > 0 ? "up" : "down";
            int heavyExpectedFacing = heavyDirection.x > 0 ? 3 : heavyDirection.x < 0 ? 1 : heavyDirection.y > 0 ? 2 : 0;
            var heavyUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + heavyDirection.x) };
            heavyUnits[1].Y += heavyDirection.y;
            var heavySkill = liveCatalog.CreateSkill(10610, 0, 10009, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10610, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            heavySkill.LimitedTimes = 1; heavyUnits[0].Skills.Add(heavySkill);
            heavyUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(heavySkill, 0));
            var heavyCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            heavyCheckpoint.Units.AddRange(heavyUnits); var heavyWave = new FightBattleWaveState(); heavyWave.UnitIds.Add(2); heavyCheckpoint.Waves.Add(heavyWave);
            var heavyExecutor = FightBattleExecutor.Begin(heavyCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            string heavyCommitted = FightBattleCheckpointSerializer.Save(heavyCheckpoint);
            var heavyDisplay = FightBattleCheckpointSerializer.Load(heavyCommitted).Units.ToArray();
            var heavyRoot = new GameObject("Live heavy chop").transform; heavyRoot.SetParent(root, false);
            var heavySource = new GameObject("Heavy source").transform; heavySource.SetParent(heavyRoot, false); heavySource.localPosition = new Vector3(5, 5, 0);
            var heavyTarget = new GameObject("Heavy target").transform; heavyTarget.SetParent(heavyRoot, false); heavyTarget.localPosition = new Vector3(5 + heavyDirection.x, 5 + heavyDirection.y, 0);
            foreach (var actorView in new[] { heavySource, heavyTarget }) {
                actorView.gameObject.AddComponent<MyTransform>();
                var renderer = actorView.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                actorView.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
            }
            var heavyAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump ? NativeJumpAppearanceFixture.Create(heavySource.gameObject) : null;
            var heavyScope = new OfflineBattleNativePlayableScope(heavyRoot, null);
            using (var heavyViews = new OfficialBattleSkillEntityPlayback(heavyRoot, new OfficialBattleGridMapper(heavyRoot, grid),
                heavyScope, id => heavyDisplay.FirstOrDefault(value => value.Id == id)))
            using (var heavyAir = new OfficialBattleAirComboPlayback(heavyExecutor, heavyRoot, heavyScope,
                id => id == 1 ? heavySource : id == 2 ? heavyTarget : null))
            using (var heavySession = new OfficialBattleLiveImpactSession(heavyCheckpoint, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), heavyViews, 0, 1)) {
                heavyViews.BindDamageUI(heavyRoot, 0);
                ParticleSystem groundImpact = null; bool pausedImpact = false, resumedImpact = false;
                int heavyDamageFrame = -1, heavyAirFrame = -1, pausedFrames = 0, heavyFacing = -1;
                var applyAir = typeof(OfficialBattleAirComboPlayback).GetMethod("ApplySecondsStatus", BindingFlags.Instance | BindingFlags.NonPublic);
                heavySession.SecondsStatusChanged += value => {
                    if (value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24269)
                        heavyAirFrame = Time.frameCount;
                    applyAir.Invoke(heavyAir, new object[] { value });
                };
                heavySession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                    if (cast.SkillEntityId != 10610 || prefab.name != "playable10610" || leaf.name != "effect (2)") return;
                    groundImpact = leaf.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "dimian_guangyun_an");
                    groundImpact.Pause(); pausedImpact = true;
                };
                bool StartHeavy(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                    heavyFacing = cast.Presentation.FacingIndex;
                    id = heavyViews.Start(cast, heavySource, heavyTarget); return true;
                }
                void HeavyDamage(FightBattleDamageEvent damage) {
                    Require(groundImpact && groundImpact.particleCount > 0 && resumedImpact,
                        "Heavy damage must wait for the original ground impact particle.");
                    heavyDamageFrame = Time.frameCount;
                    heavyDisplay[1].Hp = damage.AfterHp; heavyDisplay[1].Turn.Hp = damage.AfterHp;
                }
                deadline = Time.realtimeSinceStartup + 20f;
                while (!heavySession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    heavySession.Pump(Time.deltaTime, StartHeavy, HeavyDamage);
                    if (pausedImpact && !resumedImpact) {
                        Require(groundImpact.particleCount == 0 && heavyDamageFrame < 0 && heavyAirFrame < 0
                            && heavyDisplay[1].Hp == 1000000, "A paused ground impact cannot settle heavy damage or launch.");
                        if (++pausedFrames == 5) { resumedImpact = true; groundImpact.Play(); }
                    }
                    heavyViews.Tick(Time.deltaTime); heavyScope.LateTick(); yield return null;
                }
                Require(pausedImpact && resumedImpact && heavyDamageFrame >= 0 && heavyAirFrame == heavyDamageFrame && heavyFacing == heavyExpectedFacing
                    && heavySession.TryTakeCompleted(out _, out var heavyResult)
                    && heavyResult.Checkpoint.StatusSeconds.PendingHits.Count == 0
                    && FightBattleCheckpointSerializer.Save(heavyCheckpoint) == heavyCommitted,
                    "Heavy damage and airborne status must share the original visible impact frame: " + heavyDirectionName);
                if (heavyAppearance != null) { yield return null; Capture("heavy-chop-impact-" + heavyDirectionName + ".png", false); }
            }
            heavyAppearance?.Dispose(); Destroy(heavyRoot.gameObject);
            yield return null;
            }

            foreach (var comboDirection in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1) }) {
                string comboDirectionName = comboDirection.x > 0 ? "right" : comboDirection.x < 0 ? "left" : comboDirection.y > 0 ? "up" : "down";
                int comboExpectedFacing = comboDirection.x > 0 ? 3 : comboDirection.x < 0 ? 1 : comboDirection.y > 0 ? 2 : 0;
                var comboUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + comboDirection.x) };
                comboUnits[1].Y += comboDirection.y;
                var comboSkill = liveCatalog.CreateSkill(10630, 0, 10011, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10630, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                comboSkill.LimitedTimes = 1; comboUnits[0].Skills.Add(comboSkill);
                comboUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(comboSkill, 0));
                var comboCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                comboCheckpoint.Units.AddRange(comboUnits); var comboWave = new FightBattleWaveState(); comboWave.UnitIds.Add(2); comboCheckpoint.Waves.Add(comboWave);
                FightBattleExecutor.Begin(comboCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                string comboCommitted = FightBattleCheckpointSerializer.Save(comboCheckpoint);
                var comboDisplay = FightBattleCheckpointSerializer.Load(comboCommitted).Units.ToArray();
                var comboRoot = new GameObject("Live three-hit combo").transform; comboRoot.SetParent(root, false);
                var comboSource = new GameObject("Combo source").transform; comboSource.SetParent(comboRoot, false); comboSource.localPosition = new Vector3(5, 5, 0);
                var comboTarget = new GameObject("Combo target").transform; comboTarget.SetParent(comboRoot, false); comboTarget.localPosition = new Vector3(5 + comboDirection.x, 5 + comboDirection.y, 0);
                foreach (var actorView in new[] { comboSource, comboTarget }) {
                    actorView.gameObject.AddComponent<MyTransform>();
                    var renderer = actorView.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    actorView.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                }
                var comboAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump ? NativeJumpAppearanceFixture.Create(comboSource.gameObject) : null;
                var comboScope = new OfflineBattleNativePlayableScope(comboRoot, null);
                using (var comboViews = new OfficialBattleSkillEntityPlayback(comboRoot, new OfficialBattleGridMapper(comboRoot, grid),
                    comboScope, id => comboDisplay.FirstOrDefault(value => value.Id == id)))
                using (var comboSession = new OfficialBattleLiveImpactSession(comboCheckpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), comboViews, 0, 1)) {
                    comboViews.BindDamageUI(comboRoot, 0);
                    var comboParticles = new ParticleSystem[3]; var comboResumed = new bool[3]; var comboPauseFrames = new int[3];
                    var comboHitFrames = new int[3];
                    int comboHits = 0, comboFacing = -1, comboVisibleCaptured = 0, comboLaterCaptured = 0;
                    comboSession.SecondsStatusChanged += _ => { };
                    comboSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                        if (cast.SkillEntityId != 10630 || prefab.name != "playable10630") return;
                        int index = leaf.name == "effect (1)" ? 0 : leaf.name == "effect (2)" ? 1 : leaf.name == "effect (3)" ? 2 : -1;
                        if (index < 0) return;
                        comboParticles[index] = leaf.GetComponentsInChildren<ParticleSystem>(true)
                            .Single(value => value.name == (index == 0 ? "daoguang da (24)" : "daoguang da (28)"));
                        comboParticles[index].Pause(true);
                    };
                    bool StartCombo(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                        comboFacing = cast.Presentation.FacingIndex; id = comboViews.Start(cast, comboSource, comboTarget); return true;
                    }
                    void ComboDamage(FightBattleDamageEvent damage) {
                        int index = comboHits;
                        Require(index < 3 && comboResumed[index] && comboParticles[index] && comboParticles[index].particleCount > 0
                            && damage.BeforeHp == comboDisplay[1].Hp, "A combo hit preceded or repeated its original slash: " + comboDirectionName);
                        comboHitFrames[index] = Time.frameCount;
                        comboHits++; comboDisplay[1].Hp = damage.AfterHp; comboDisplay[1].Turn.Hp = damage.AfterHp;
                        if (comboAppearance != null) Capture("combo-" + comboDirectionName + "-" + comboHits + ".png", false);
                    }
                    deadline = Time.realtimeSinceStartup + 20f;
                    while (!comboSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                        comboSession.Pump(Time.deltaTime, StartCombo, ComboDamage);
                        if (comboAppearance != null && comboVisibleCaptured < comboHits
                            && Time.frameCount > comboHitFrames[comboVisibleCaptured]) {
                            comboVisibleCaptured++;
                            Capture("combo-visible-" + comboDirectionName + "-" + comboVisibleCaptured + ".png", false);
                        }
                        if (comboAppearance != null && comboLaterCaptured < comboHits
                            && Time.frameCount >= comboHitFrames[comboLaterCaptured] + 5) {
                            comboLaterCaptured++;
                            Capture("combo-later-" + comboDirectionName + "-" + comboLaterCaptured + ".png", false);
                        }
                        for (int index = 0; index < 3; index++) if (comboParticles[index] && !comboResumed[index]) {
                            Require(comboParticles[index].particleCount == 0 && comboHits == index,
                                "A paused combo slash cannot settle its damage: " + comboDirectionName);
                            if (++comboPauseFrames[index] == 5) { comboResumed[index] = true; comboParticles[index].Play(true); }
                        }
                        comboViews.Tick(Time.deltaTime); comboScope.LateTick(); yield return null;
                    }
                    Require(comboHits == 3 && comboFacing == comboExpectedFacing && comboResumed.All(value => value)
                        && comboSession.TryTakeCompleted(out _, out var comboResult) && comboResult.Event.DamageEvents.Count == 3
                        && FightBattleCheckpointSerializer.Save(comboCheckpoint) == comboCommitted,
                        "The original three combo impacts did not complete: " + comboDirectionName);
                    if (comboAppearance != null && comboVisibleCaptured < 3) {
                        yield return null; comboViews.Tick(Time.deltaTime); comboScope.LateTick();
                        Capture("combo-visible-" + comboDirectionName + "-3.png", false);
                    }
                    if (comboAppearance != null && comboLaterCaptured < 3) {
                        while (Time.frameCount < comboHitFrames[2] + 5) { yield return null; comboViews.Tick(Time.deltaTime); comboScope.LateTick(); }
                        Capture("combo-later-" + comboDirectionName + "-3.png", false);
                    }
                }
                comboAppearance?.Dispose(); Destroy(comboRoot.gameObject); yield return null;
            }

            // Four-way slash uses one simultaneous original cross effect for its four cells.
            var crossOffsets = new[] { new Vector2Int(-1, 0), new Vector2Int(0, -1),
                new Vector2Int(0, 1), new Vector2Int(1, 0) };
            var crossUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 5) };
            for (int index = 0; index < crossOffsets.Length; index++) {
                var unit = LiveUnit(index + 2, 2, 5 + crossOffsets[index].x); unit.Y += crossOffsets[index].y; crossUnits.Add(unit);
            }
            var crossSkill = liveCatalog.CreateSkill(10650, 0, 10013, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10650, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            crossSkill.LimitedTimes = 1; crossUnits[0].Skills.Add(crossSkill);
            crossUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(crossSkill, 0));
            var crossCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            crossCheckpoint.Units.AddRange(crossUnits); var crossWave = new FightBattleWaveState();
            crossWave.UnitIds.AddRange(new[] { 2, 3, 4, 5 }); crossCheckpoint.Waves.Add(crossWave);
            FightBattleExecutor.Begin(crossCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            string crossCommitted = FightBattleCheckpointSerializer.Save(crossCheckpoint);
            var crossDisplay = FightBattleCheckpointSerializer.Load(crossCommitted).Units.ToArray();
            var crossRoot = new GameObject("Live cross slash").transform; crossRoot.SetParent(root, false);
            var crossBodies = new Dictionary<int, Transform>();
            foreach (var unit in crossUnits) {
                var body = new GameObject("Cross unit " + unit.Id).transform; body.SetParent(crossRoot, false);
                body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                crossBodies.Add(unit.Id, body);
            }
            var crossAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump ? NativeJumpAppearanceFixture.Create(crossBodies[1].gameObject) : null;
            var crossScope = new OfflineBattleNativePlayableScope(crossRoot, null);
            using (var crossViews = new OfficialBattleSkillEntityPlayback(crossRoot, new OfficialBattleGridMapper(crossRoot, grid),
                crossScope, id => crossDisplay.FirstOrDefault(value => value.Id == id)))
            using (var crossSession = new OfficialBattleLiveImpactSession(crossCheckpoint, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), crossViews, 0, 1)) {
                crossViews.BindDamageUI(crossRoot, 0);
                foreach (var pair in crossBodies) crossViews.EnsureUnit(pair.Key, pair.Value);
                ParticleSystem crossParticle = null; bool crossPaused = false, crossResumed = false;
                int crossPauseFrames = 0, crossImpactFrame = -1; var damagedCrossUnits = new HashSet<int>();
                crossSession.SecondsStatusChanged += _ => { };
                crossSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                    if (cast.SkillEntityId != 10650 || prefab.name != "playable10650" || leaf.name != "effect") return;
                    crossParticle = leaf.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "shizi");
                    crossParticle.Pause(true); crossPaused = true;
                };
                bool StartCross(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                    id = crossViews.Start(cast, crossBodies[1], null); return true;
                }
                void CrossDamage(FightBattleDamageEvent damage) {
                    Require(crossResumed && crossParticle && crossParticle.particleCount > 0
                        && damagedCrossUnits.Add(damage.TargetId) && crossBodies.ContainsKey(damage.TargetId),
                        "A cross-scope target was hit before the original effect or more than once.");
                    if (crossImpactFrame < 0) crossImpactFrame = Time.frameCount;
                    Require(crossImpactFrame == Time.frameCount, "The simultaneous cross targets did not share one impact frame.");
                    var display = crossDisplay.Single(value => value.Id == damage.TargetId);
                    Require(display.Hp == damage.BeforeHp, "The cross target health changed before its own hit.");
                    display.Hp = damage.AfterHp; display.Turn.Hp = damage.AfterHp;
                }
                deadline = Time.realtimeSinceStartup + 20f;
                while (!crossSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    crossSession.Pump(Time.deltaTime, StartCross, CrossDamage);
                    if (crossPaused && !crossResumed) {
                        Require(crossParticle.particleCount == 0 && damagedCrossUnits.Count == 0,
                            "A paused cross effect cannot settle its targets.");
                        if (++crossPauseFrames == 5) { crossResumed = true; crossParticle.Play(true); }
                    }
                    crossViews.Tick(Time.deltaTime); crossScope.LateTick(); yield return null;
                }
                Require(crossPaused && crossResumed && damagedCrossUnits.SetEquals(new[] { 2, 3, 4, 5 })
                    && crossSession.TryTakeCompleted(out _, out var crossResult) && crossResult.Event.DamageEvents.Count == 4
                    && FightBattleCheckpointSerializer.Save(crossCheckpoint) == crossCommitted,
                    "The original cross effect did not reach all four targets exactly once.");
                if (crossAppearance != null) { yield return null; crossViews.Tick(Time.deltaTime); crossScope.LateTick(); Capture("cross-slash-impact.png", false); }
            }
            crossAppearance?.Dispose(); Destroy(crossRoot.gameObject); yield return null;

            // The original wind blade moves through four independently configured scope cells.
            foreach (var windDirection in new[] { new[] { 0, 1, 2 }, new[] { 0, -1, 0 },
                new[] { -1, 0, 1 }, new[] { 1, 0, 3 } })
            {
                int dx = windDirection[0], dy = windDirection[1];
                var windUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 5) };
                windUnits[0].Direction = windDirection[2];
                for (int index = 1; index <= 4; index++) {
                    var unit = LiveUnit(index + 1, 2, 5 + dx * index); unit.Y += dy * index;
                    unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectDodgeValue"), Value = 20000 });
                    windUnits.Add(unit);
                }
                var windSkill = liveCatalog.CreateSkill(10640, 0, 10012, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10640, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                windSkill.LimitedTimes = 1; windUnits[0].Skills.Add(windSkill);
                windUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(windSkill, 0));
                var windCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                windCheckpoint.Units.AddRange(windUnits); var windWave = new FightBattleWaveState();
                windWave.UnitIds.AddRange(new[] { 2, 3, 4, 5 }); windCheckpoint.Waves.Add(windWave);
                FightBattleExecutor.Begin(windCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                string windCommitted = FightBattleCheckpointSerializer.Save(windCheckpoint);
                var windDisplay = FightBattleCheckpointSerializer.Load(windCommitted).Units.ToArray();
                var windRoot = new GameObject("Live wind blade").transform; windRoot.SetParent(root, false);
                var windBodies = new Dictionary<int, Transform>();
                foreach (var unit in windUnits) {
                    var body = new GameObject("Wind unit " + unit.Id).transform; body.SetParent(windRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    windBodies.Add(unit.Id, body);
                }
                var windAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump ? NativeJumpAppearanceFixture.Create(windBodies[1].gameObject) : null;
                var windScope = new OfflineBattleNativePlayableScope(windRoot, null);
                using (var windViews = new OfficialBattleSkillEntityPlayback(windRoot, new OfficialBattleGridMapper(windRoot, grid),
                    windScope, id => windDisplay.FirstOrDefault(value => value.Id == id)))
                using (var windSession = new OfficialBattleLiveImpactSession(windCheckpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), windViews, 0, 1)) {
                    windViews.BindDamageUI(windRoot, 0);
                    foreach (var pair in windBodies) windViews.EnsureUnit(pair.Key, pair.Value);
                    Animator windAnimator = null; ParticleSystem windParticle = null;
                    bool windPaused = false, windResumed = false; int windPauseFrames = 0, windHits = 0;
                    float previousWindPosition = -1f, lastWindPosition = -1f;
                    windSession.SecondsStatusChanged += _ => { };
                    windSession.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                        if (cast.SkillEntityId != 10640 || prefab.name != "playable10640" || leaf.name != "effect (2)") return;
                        windAnimator = leaf.GetComponentsInChildren<Animator>(true).Single(value => value.name == "ani");
                        windParticle = leaf.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "fengren liang");
                        windAnimator.speed = 0f; windPaused = true;
                    };
                    bool StartWind(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) {
                        id = windViews.Start(cast, windBodies[1], null); return true;
                    }
                    void WindDamage(FightBattleDamageEvent damage) {
                        Require(windResumed && windAnimator && windParticle && windParticle.particleCount > 0
                            && damage.TargetId == windHits + 2 && windAnimator.transform.localPosition.y > 0f
                            && windAnimator.transform.localPosition.y >= previousWindPosition,
                            "Wind blade damage preceded its visible movement or reached the wrong cell.");
                        previousWindPosition = windAnimator.transform.localPosition.y; windHits++;
                        var display = windDisplay.Single(value => value.Id == damage.TargetId);
                        Require(display.Hp == damage.BeforeHp, "Wind target health changed before its hit.");
                        display.Hp = damage.AfterHp; display.Turn.Hp = damage.AfterHp;
                    }
                    deadline = Time.realtimeSinceStartup + 20f;
                    while (!windSession.IsCompleted && Time.realtimeSinceStartup < deadline) {
                        bool windGraphPaused = windPaused && !windResumed;
                        windSession.Pump(Time.deltaTime, StartWind, WindDamage);
                        if (windPaused && !windResumed) {
                            Require(windHits == 0 && Mathf.Approximately(windAnimator.transform.localPosition.y, 0f),
                                "A paused wind blade cannot settle a scope.");
                            if (++windPauseFrames == 5) { windResumed = true; windAnimator.speed = 1f; }
                        }
                        windViews.Tick(windGraphPaused ? 0f : Time.deltaTime); windScope.LateTick(); yield return null;
                        if (windAnimator) lastWindPosition = windAnimator.transform.localPosition.y;
                    }
                    bool windCompleted = windSession.TryTakeCompleted(out _, out var windResult);
                    Require(windPaused && windResumed && windHits == 4
                        && windCompleted && windResult.Event.DamageEvents.Count == 4
                        && FightBattleCheckpointSerializer.Save(windCheckpoint) == windCommitted,
                        "The original wind blade did not reach all four cells in order: direction=" + dx + "," + dy
                        + ", paused=" + windPaused + ", resumed=" + windResumed + ", hits=" + windHits
                        + ", completed=" + windCompleted + ", last position=" + lastWindPosition);
                    if (windAppearance != null) { yield return null; windViews.Tick(Time.deltaTime); windScope.LateTick();
                        Capture("wind-blade-" + dx + "-" + dy + ".png", false); }
                }
                windAppearance?.Dispose(); Destroy(windRoot.gameObject); yield return null;
            }

            // A blocked wind push can overlap another cell's original hit.
            foreach (bool overlappingWindHit in new[] { false, true })
            {
                var windCollisionUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, overlappingWindHit ? 3 : 6),
                    LiveUnit(3, 2, overlappingWindHit ? 5 : 7) };
                windCollisionUnits[0].Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
                var windCollisionSkill = liveCatalog.CreateSkill(10640, 0, 10012, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10640, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                windCollisionSkill.LimitedTimes = 1; windCollisionUnits[0].Skills.Add(windCollisionSkill);
                windCollisionUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(windCollisionSkill, 0));
                var windCollisionCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                if (overlappingWindHit) windCollisionCheckpoint.Grid.BlockedPositions.Add(new FightGridPoint(4, 5));
                windCollisionCheckpoint.Units.AddRange(windCollisionUnits); var windCollisionWave = new FightBattleWaveState();
                windCollisionWave.UnitIds.AddRange(new[] { 2, 3 }); windCollisionCheckpoint.Waves.Add(windCollisionWave);
                FightBattleExecutor.Begin(windCollisionCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                string windCollisionCommitted = FightBattleCheckpointSerializer.Save(windCollisionCheckpoint);
                var windCollisionDisplay = FightBattleCheckpointSerializer.Load(windCollisionCommitted).Units.ToArray();
                var windCollisionRoot = new GameObject("Live wind blade collision").transform; windCollisionRoot.SetParent(root, false);
                var windCollisionBodies = new Dictionary<int, Transform>();
                foreach (var unit in windCollisionUnits) {
                    var body = new GameObject("Wind collision unit " + unit.Id).transform; body.SetParent(windCollisionRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    windCollisionBodies.Add(unit.Id, body);
                }
                var windCollisionAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(windCollisionBodies[1].gameObject) : null;
                var windCollisionScope = new OfflineBattleNativePlayableScope(windCollisionRoot, null);
                using (var windCollisionViews = new OfficialBattleSkillEntityPlayback(windCollisionRoot,
                    new OfficialBattleGridMapper(windCollisionRoot, grid), windCollisionScope,
                    id => windCollisionDisplay.FirstOrDefault(value => value.Id == id))) {
                    windCollisionViews.BindDamageUI(windCollisionRoot, 0);
                    var nativeCollisionFrames = new Dictionary<int, int>();
                    foreach (var pair in windCollisionBodies) {
                        int unitId = pair.Key;
                        windCollisionViews.EnsureUnit(unitId, pair.Value).GetComponent<MyFightRoleFight>().OnDamage += (role, damage) => {
                            if (damage.Component.DamageResult.DamageValue == -600) nativeCollisionFrames[unitId] = Time.frameCount;
                        };
                    }
                    int windContactFrame = -1, windDirectHits = 0, windCollisionHits = 0;
                    var windCollisionTargets = new HashSet<int>();
                    Transform WindCollisionBody(int id) => windCollisionBodies.TryGetValue(id, out var body) ? body : null;
                    void WindCollisionDamage(FightBattleDamageEvent damage, FightBattleUnitState projected) {
                        int index = Array.FindIndex(windCollisionDisplay, value => value.Id == damage.TargetId);
                        windCollisionDisplay[index] = projected;
                        if (damage.SkillEntityId == 10640) {
                            windDirectHits++;
                            if (overlappingWindHit && damage.TargetId == 3)
                                Require(windContactFrame < 0, "A later wind hit waited for an earlier blocked contact.");
                            return;
                        }
                        Require(damage.SkillEntityId == 10100 && windContactFrame == Time.frameCount
                            && damage.TrueDamage && damage.Damage == -600 && windCollisionTargets.Add(damage.TargetId)
                            && nativeCollisionFrames.TryGetValue(damage.TargetId, out int frame) && frame == Time.frameCount,
                            "Wind collision and its native damage number must share the actual contact frame.");
                        windCollisionHits++;
                        if (windCollisionHits == (overlappingWindHit ? 1 : 2))
                            Capture(overlappingWindHit ? "wind-overlapping-collision.png" : "wind-collision-impact.png", false);
                    }
                    using (var windCollisionDriver = new OfficialBattleLiveGraphDriver(windCollisionCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        windCollisionViews, entity => entity == 10640 || entity == 10100, WindCollisionBody, null, WindCollisionDamage)) {
                        windCollisionDisplay = windCollisionCheckpoint.Units.Select(value => windCollisionDriver.DisplayUnit(value.Id)).ToArray();
                        windCollisionDriver.SecondsStatusChanged += _ => { };
                        windCollisionDriver.BlockedContact += move => {
                            Require(move.UnitId == 2 && move.MoveBehaviourId == 10643, "Wrong wind blade blocked action.");
                            windContactFrame = Time.frameCount;
                        };
                        deadline = Time.realtimeSinceStartup + 20f;
                        while (!windCollisionDriver.IsCompleted && Time.realtimeSinceStartup < deadline) {
                            windCollisionDriver.Pump(Time.deltaTime); windCollisionViews.Tick(Time.deltaTime);
                            windCollisionScope.LateTick(); yield return null;
                        }
                        Require(windDirectHits == (overlappingWindHit ? 2 : 1)
                            && windCollisionHits == (overlappingWindHit ? 1 : 2)
                            && windCollisionTargets.SetEquals(overlappingWindHit ? new[] { 2 } : new[] { 2, 3 })
                            && windCollisionDriver.TryTakeCompleted(out _, out var windCollisionResult)
                            && windCollisionResult.Event.DamageEvents.Count(value => value.SkillEntityId == 10100) == (overlappingWindHit ? 1 : 2)
                            && FightBattleCheckpointSerializer.Save(windCollisionCheckpoint) == windCollisionCommitted,
                            "Wind blade did not complete both original collision child hits.");
                    }
                }
                windCollisionAppearance?.Dispose(); Destroy(windCollisionRoot.gameObject); yield return null;
            }

            // Exercise the same live driver through the production graph owner's factory.
            var graphUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 5) };
            var graphSkill = liveCatalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10800, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            graphSkill.LimitedTimes = 1; graphUnits[0].Skills.Add(graphSkill);
            graphUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(graphSkill, 0));
            var graphCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            graphCheckpoint.Units.AddRange(graphUnits); var graphWave = new FightBattleWaveState(); graphWave.UnitIds.Add(2);
            graphCheckpoint.Waves.Add(graphWave);
            FightBattleExecutor.Begin(graphCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            string graphCommitted = FightBattleCheckpointSerializer.Save(graphCheckpoint);
            var graphDisplay = FightBattleCheckpointSerializer.Load(graphCommitted).Units.ToArray();
            var liveRoot = new GameObject("Live production graph scene").transform; liveRoot.SetParent(root, false);
            var liveSource = new GameObject("Live graph source").transform; liveSource.SetParent(liveRoot, false); liveSource.localPosition = new Vector3(2, 5, 0);
            var liveTarget = new GameObject("Live graph target").transform; liveTarget.SetParent(liveRoot, false); liveTarget.localPosition = new Vector3(5, 5, 0);
            foreach (var actorView in new[] { liveSource, liveTarget }) {
                actorView.gameObject.AddComponent<MyTransform>();
                var renderer = actorView.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                actorView.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
            }
            using (var liveScope = new OfflineBattleNativePlayableScope(liveRoot, null))
            using (var liveViews = new OfficialBattleSkillEntityPlayback(liveRoot, new OfficialBattleGridMapper(liveRoot, grid),
                liveScope, id => graphDisplay.FirstOrDefault(value => value.Id == id)))
            using (var liveGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), liveScope, liveViews))
            {
                liveGraph.BindDamageUI(liveRoot, 0);
                int arrivalFrame = -1; bool graphDamageShown = false;
                liveViews.HitSelectionViewArrived += (cast, hit, center, position, scopeIndex) => {
                    if (cast.SkillEntityId == 10800 && hit.HitEntityId == 10801) arrivalFrame = Time.frameCount;
                };
                Transform LiveBody(int unitId) => unitId == 1 ? liveSource : unitId == 2 ? liveTarget : null;
                void LiveDamage(FightBattleDamageEvent damage, FightBattleUnitState display) {
                    graphDamageShown = arrivalFrame == Time.frameCount && display.Hp == damage.AfterHp
                        && graphCheckpoint.Units[1].Hp == 1000000;
                    Require(graphDamageShown, "Production graph health must update at the original fireball impact.");
                    graphDisplay[1] = display;
                    Capture("live-graph-fireball-impact.png", false);
                }
                using var graphDriver = liveGraph.CreateLiveStep(graphCheckpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()), LiveBody, null, LiveDamage);
                deadline = Time.realtimeSinceStartup + 20f;
                while (!graphDriver.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    graphDriver.Pump(Time.deltaTime); liveGraph.Tick(Time.deltaTime); liveGraph.LateTick();
                    if (arrivalFrame < 0) Require(graphDisplay[1].Hp == 1000000, "Production graph health changed before impact.");
                    yield return null;
                }
                Require(graphDamageShown && graphDriver.TryTakeCompleted(out _, out _)
                    && FightBattleCheckpointSerializer.Save(graphCheckpoint) == graphCommitted,
                    "The production graph must complete the isolated fireball step without modifying its checkpoint.");
            }
            Destroy(liveRoot.gameObject);

            // Ordinary approach is an independent original movement skill, not an attack cast.
            var approachUnits = new[] { LiveUnit(1, 1, 2), LiveUnit(2, 2, 12) };
            approachUnits[0].Properties.Add(new FightPropertyValue { Id = 100, Value = 2 });
            var approachSkill = liveCatalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                liveCatalog.CalculateSkillProps(10600, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            approachSkill.LimitedTimes = 1; approachUnits[0].Skills.Add(approachSkill);
            approachUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(approachSkill, 0));
            var approachCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            approachCheckpoint.Units.AddRange(approachUnits); var approachWave = new FightBattleWaveState(); approachWave.UnitIds.Add(2);
            approachCheckpoint.Waves.Add(approachWave);
            FightBattleExecutor.Begin(approachCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            var approachRoot = new GameObject("Live ordinary approach scene").transform; approachRoot.SetParent(root, false);
            var approachSource = new GameObject("Approach source").transform; approachSource.SetParent(approachRoot, false);
            approachSource.localPosition = new Vector3(2, 5, 0); approachSource.gameObject.AddComponent<MyTransform>();
            var approachTarget = new GameObject("Approach target").transform; approachTarget.SetParent(approachRoot, false);
            approachTarget.localPosition = new Vector3(12, 5, 0); approachTarget.gameObject.AddComponent<MyTransform>();
            NativeJumpAppearanceFixture approachAppearance = null;
            if (OfficialBattleHitPlaybackAcceptance.PlayerJump)
            {
                approachAppearance = NativeJumpAppearanceFixture.Create(approachSource.gameObject);
                yield return null;
            }
            using (var approachScope = new OfflineBattleNativePlayableScope(approachRoot, null))
            using (var approachViews = new OfficialBattleSkillEntityPlayback(approachRoot,
                new OfficialBattleGridMapper(approachRoot, grid), approachScope, id => approachUnits.FirstOrDefault(value => value.Id == id)))
            using (var approachGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), approachScope, approachViews))
            using (var approachDriver = approachGraph.CreateLiveStep(approachCheckpoint, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                id => id == 1 ? approachSource : id == 2 ? approachTarget : null, null,
                (damage, display) => throw new InvalidOperationException("A distant approach must not cause damage.")))
            {
                bool sawAction = false, sawSkill = false;
                approachViews.CastStarted += cast => {
                    if (cast.SkillEntityId == 15300) sawSkill = true;
                };
                deadline = Time.realtimeSinceStartup + 20f;
                while (Time.realtimeSinceStartup < deadline && (!approachDriver.IsCompleted || !sawAction)) {
                    approachDriver.Pump(Time.deltaTime); approachGraph.Tick(Time.deltaTime); approachGraph.LateTick();
                    sawAction |= approachRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length > 0;
                    if (sawAction && approachAppearance != null) Capture("live-ordinary-approach.png", false);
                    yield return null;
                }
                bool approachCompleted = approachDriver.TryTakeCompleted(out _, out var approachResult);
                bool recordedApproach = approachCompleted && approachResult.Event.Movements.Any(value => value.UnitId == 1 && value.MoveSkillEntityId == 15300);
                Require(approachCompleted && recordedApproach && sawAction && sawSkill && approachCheckpoint.Units[0].X == 2,
                    $"Original approach: completed={approachCompleted}, recorded={recordedApproach}, action={sawAction}, skill={sawSkill}, x={approachCheckpoint.Units[0].X}, pending={approachDriver.PendingMovement != null}, "
                    + $"resultMove={approachResult?.Checkpoint.Units[0].MoveDist}, targetable={approachResult?.Checkpoint.Units[1].Targetable}, range={approachSkill.Range.Count}, casts={approachResult?.Event.SkillCasts.Count}, moves={approachResult?.Event.Movements.Count}, "
                    + $"actionDuration={approachDriver.PendingMovement?.Movement.OrdinarySkillPresentation?.ActionDuration}, delay={approachDriver.PendingMovement?.Movement.Delay}, moveDuration={approachDriver.PendingMovement?.Movement.MoveDuration}, pathCount={approachDriver.PendingMovement?.Movement.Path.Count}.");
            }
            approachAppearance?.Dispose();
            Destroy(approachRoot.gameObject);

            // An ordinary attack must not start while its approach action is still playing.
            float previousFollowCameraSize = cameraView.orthographicSize;
            cameraView.orthographicSize = 8f;
            foreach (var direction in new[] { new Vector2Int(3, 0), new Vector2Int(-3, 0),
                new Vector2Int(0, 3), new Vector2Int(0, -3) })
            {
                string directionName = direction.x > 0 ? "right" : direction.x < 0 ? "left" : direction.y > 0 ? "up" : "down";
                int expectedFacing = direction.x > 0 ? 3 : direction.x < 0 ? 1 : direction.y > 0 ? 2 : 0;
                var followUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + direction.x) };
                followUnits[1].Y += direction.y;
                followUnits[0].Properties.Add(new FightPropertyValue { Id = 100, Value = 2 });
                var followSkill = liveCatalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10600, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                followSkill.LimitedTimes = 1; followUnits[0].Skills.Add(followSkill);
                followUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(followSkill, 0));
                var followCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                followCheckpoint.Units.AddRange(followUnits); var followWave = new FightBattleWaveState(); followWave.UnitIds.Add(2);
                followCheckpoint.Waves.Add(followWave);
                FightBattleExecutor.Begin(followCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var followRoot = new GameObject("Live approach and attack scene").transform; followRoot.SetParent(root, false);
                var followSource = new GameObject("Follow source").transform; followSource.SetParent(followRoot, false); followSource.localPosition = new Vector3(5, 5, 0);
                var followTarget = new GameObject("Follow target").transform; followTarget.SetParent(followRoot, false); followTarget.localPosition = new Vector3(5 + direction.x, 5 + direction.y, 0);
                foreach (var body in new[] { followSource, followTarget }) {
                    body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                }
                NativeJumpAppearanceFixture followAppearance = null;
                if (OfficialBattleHitPlaybackAcceptance.PlayerJump)
                {
                    DestroyImmediate(followSource.GetComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>());
                    DestroyImmediate(followSource.GetComponent<MeshRenderer>());
                    followAppearance = NativeJumpAppearanceFixture.Create(followSource.gameObject);
                    yield return null;
                }
                using (var followScope = new OfflineBattleNativePlayableScope(followRoot, null))
                using (var followViews = new OfficialBattleSkillEntityPlayback(followRoot, new OfficialBattleGridMapper(followRoot, grid),
                    followScope, id => followUnits.FirstOrDefault(value => value.Id == id)))
                using (var followGraph = new OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog.TryLoadDefault(), followScope, followViews))
                {
                    followGraph.BindDamageUI(followRoot, 0);
                    bool sawFollowAttack = false, movedBeforeAttack = false, followDamage = false, afterActionEnd = false, resumedSlash = false;
                    float followMoveStarted = -1f, followGraphElapsed = 0f, slashPaused = 0f;
                    ParticleSystem followSlash = null;
                    followViews.CastStarted += cast => {
                        if (cast.SkillEntityId == 15300) followMoveStarted = Time.time;
                        if (cast.SkillEntityId == 10600) {
                            sawFollowAttack = true;
                            movedBeforeAttack = followRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length == 0;
                            afterActionEnd = followMoveStarted >= 0f && followGraphElapsed >= .5f - .0001f;
                        }
                    };
                    using var followDriver = followGraph.CreateLiveStep(followCheckpoint, copy => FightBattleExecutor.Restore(copy,
                        formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        id => id == 1 ? followSource : id == 2 ? followTarget : null, null, (damage, display) => {
                            followDamage = resumedSlash && followSlash && followSlash.particleCount > 0 && display.Hp == damage.AfterHp
                                && followCheckpoint.Units[1].Hp == 1000000;
                            if (followDamage && followAppearance != null) Capture("live-slash-impact-" + directionName + ".png", false);
                        });
                    followDriver.SkillGraphPlayableStarted += (cast, id, prefab, playable) => {
                        if (cast.SkillEntityId != 10600 || !prefab || prefab.name != "playable10600" || !playable || playable.name != "effect") return;
                        followSlash = playable.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "daoguang da (23)");
                        followSlash.Pause(true);
                    };
                    deadline = Time.realtimeSinceStartup + 20f;
                    while (!followDriver.IsCompleted && Time.realtimeSinceStartup < deadline) {
                        followDriver.Pump(Time.deltaTime);
                        if (followMoveStarted >= 0f && !sawFollowAttack) followGraphElapsed += Time.deltaTime;
                        followGraph.Tick(Time.deltaTime); followGraph.LateTick();
                        if (followSlash && !resumedSlash) {
                            Require(!followDamage && followDriver.DisplayUnit(2).Hp == 1000000,
                                "A paused original slash must not settle damage.");
                            slashPaused += Time.deltaTime;
                            if (slashPaused >= .2f) { resumedSlash = true; followSlash.Play(true); }
                        }
                        yield return null;
                    }
                    bool completedFollow = followDriver.TryTakeCompleted(out _, out var followResult);
                    Require(completedFollow && sawFollowAttack && movedBeforeAttack && afterActionEnd && followDamage
                        && followCheckpoint.Units[1].Hp == 1000000 && followResult.Event.DamageEvents.Count == 1
                        && followResult.Event.SkillCasts.Single(value => value.SkillEntityId == 10600).Presentation.FacingIndex == expectedFacing,
                        $"Approach and attack {directionName}: completed={completedFollow}, started={sawFollowAttack}, afterMove={movedBeforeAttack}, afterAction={afterActionEnd}, elapsed={followGraphElapsed}, slash={followSlash != null}, resumed={resumedSlash}, damage={followDamage}, pendingMove={followDriver.PendingMovement != null}.");
                    if (followAppearance != null) {
                        yield return null;
                        followGraph.Tick(Time.deltaTime); followGraph.LateTick();
                        yield return null;
                        Capture("live-slash-visible-" + directionName + ".png", false);
                    }
                }
                followAppearance?.Dispose();
                Destroy(followRoot.gameObject);
            }
            cameraView.orthographicSize = previousFollowCameraSize;

            // A completed isolated step replaces its executor. Existing airborne
            // views must retain their entity and follow the replacement clock only.
            var airCheckpoint = FightBattleCheckpointSerializer.Load(committedFire);
            var launch = liveCatalog.CreateStatus(24269, 2, 1, 1, 10580, 10006, 2, 20, 1);
            launch.Components.Seconds.Started = true;
            launch.Components.Seconds.RemainingSeconds = launch.Components.Seconds.DurationSeconds;
            airCheckpoint.Units[1].Statuses.Add(launch);
            airCheckpoint.StatusInstanceSerial = 1;
            airCheckpoint.StatusSeconds = new FightBattleStatusSecondsClock();
            var oldAirExecutor = FightBattleExecutor.Restore(airCheckpoint, formula,
                new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            var adoptedAirCheckpoint = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(airCheckpoint));
            var adoptedAirExecutor = FightBattleExecutor.Restore(adoptedAirCheckpoint, formula,
                new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            var airRoot = new GameObject("Airborne executor transfer").transform;
            airRoot.SetParent(root, false);
            var airTarget = new GameObject("Airborne target").transform;
            airTarget.SetParent(airRoot, false);
            airTarget.localPosition = new Vector3(5, 5, 0);
            airTarget.gameObject.AddComponent<MyTransform>();
            var airAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                ? NativeJumpAppearanceFixture.Create(airTarget.gameObject) : null;
            var airScope = new OfflineBattleNativePlayableScope(airRoot, null);
            using (var airViews = new OfficialBattleAirComboPlayback(oldAirExecutor, airRoot, airScope,
                id => id == 2 ? airTarget : null)) {
                airViews.Synchronize();
                var leases = (IDictionary)typeof(OfficialBattleAirComboPlayback)
                    .GetField("leases", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(airViews);
                long launchKey = ((long)2 << 32) | 1u;
                Require(leases.Count == 1 && leases.Contains(launchKey), "Original airborne state did not restore.");
                object originalLease = leases[launchKey];
                var rebind = typeof(OfficialBattleAirComboPlayback).GetMethod("Rebind", BindingFlags.Instance | BindingFlags.NonPublic);
                rebind.Invoke(airViews, new object[] { adoptedAirExecutor });
                rebind.Invoke(airViews, new object[] { adoptedAirExecutor });
                Require(leases.Count == 1 && ReferenceEquals(originalLease, leases[launchKey]),
                    "Adopting an equivalent executor restarted its native airborne state.");
                oldAirExecutor.AdvanceStatusSeconds(10d);
                Require(leases.Count == 1 && ReferenceEquals(originalLease, leases[launchKey]),
                    "The discarded executor still controlled the adopted airborne view.");
                adoptedAirExecutor.AdvanceStatusSeconds(launch.Components.Seconds.DurationSeconds + .001d);
                Require(leases.Count == 1 && !leases.Contains(launchKey)
                    && adoptedAirCheckpoint.Units[1].Statuses.Any(value => value.ClassId == 24270),
                    "The adopted executor did not deliver the original recovery transition.");
                adoptedAirExecutor.AdvanceStatusSeconds(10d);
                Require(leases.Count == 0, "The adopted recovery status did not remove its native view.");
            }
            airScope.Dispose();
            var activeAirCheckpoint = FightBattleCheckpointSerializer.Load(committedFire);
            var activeAirClock = new FightBattleStatusSeconds(activeAirCheckpoint, liveCatalog,
                new FightRandomStream(activeAirCheckpoint.RandomSeed));
            activeAirClock.Enqueue(0, 2, true, false, false,
                liveCatalog.CreateStatus(24269, 2, 1, 1, 10580, 10006, 2, 20, 1));
            var activeAirExecutor = FightBattleExecutor.Restore(activeAirCheckpoint, formula,
                new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            var activeAirScope = new OfflineBattleNativePlayableScope(airRoot, null);
            using (var activeAirViews = new OfficialBattleAirComboPlayback(activeAirExecutor, airRoot, activeAirScope,
                id => id == 2 ? airTarget : null)) {
                var leases = (IDictionary)typeof(OfficialBattleAirComboPlayback)
                    .GetField("leases", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(activeAirViews);
                var gate = new StatusClockDuringCast();
                gate.Start = () => {
                    activeAirExecutor.AdvanceStatusSeconds(0);
                    Require(leases.Count == 1 && activeAirCheckpoint.Units[1].Statuses.Any(value => value.ClassId == 24269),
                        "A native airborne state did not start during the active skill.");
                    activeAirScope.Tick(.05f); activeAirScope.LateTick();
                    if (airAppearance != null) Capture("active-cast-airborne.png", false);
                    activeAirExecutor.AdvanceStatusSeconds(.7);
                    Require(leases.Count == 1 && activeAirCheckpoint.Units[1].Statuses.Any(value => value.ClassId == 24270),
                        "A native airborne state did not recover during the active skill.");
                };
                activeAirExecutor.ImpactGate = gate;
                activeAirExecutor.Step(0, 1);
                Require(gate.Start == null, "The active status clock was not exercised during the cast.");
            }
            activeAirScope.Dispose();
            var threadedCheckpoint = FightBattleCheckpointSerializer.Load(committedFire);
            var threadedClock = new FightBattleStatusSeconds(threadedCheckpoint, liveCatalog,
                new FightRandomStream(threadedCheckpoint.RandomSeed));
            threadedClock.Enqueue(0, 2, true, false, false,
                liveCatalog.CreateStatus(24269, 2, 1, 1, 10580, 10006, 2, 20, 1));
            string threadedCommitted = FightBattleCheckpointSerializer.Save(threadedCheckpoint);
            var threadedOriginal = FightBattleExecutor.Restore(threadedCheckpoint, formula,
                new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
            var threadedScope = new OfflineBattleNativePlayableScope(airRoot, null);
            using (var threadedViews = new OfficialBattleSkillEntityPlayback(airRoot,
                new OfficialBattleGridMapper(airRoot, grid), threadedScope,
                id => threadedCheckpoint.Units.FirstOrDefault(value => value.Id == id)))
            using (var threadedAir = new OfficialBattleAirComboPlayback(threadedOriginal, airRoot, threadedScope,
                id => id == 2 ? airTarget : null))
            using (var threadedSession = new OfficialBattleLiveImpactSession(threadedCheckpoint, copy =>
                FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                threadedViews, 0, 1)) {
                int mainThread = Thread.CurrentThread.ManagedThreadId;
                var delivered = new List<string>();
                var apply = typeof(OfficialBattleAirComboPlayback).GetMethod("ApplySecondsStatus", BindingFlags.Instance | BindingFlags.NonPublic);
                threadedSession.SecondsStatusChanged += value => {
                    Require(Thread.CurrentThread.ManagedThreadId == mainThread, "Native status notification left Unity's main thread.");
                    delivered.Add(value.Kind + ":" + value.Status.ClassId);
                    apply.Invoke(threadedAir, new object[] { value });
                };
                bool HoldThreadedCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier id) { id = default; return false; }
                Task<bool> timedLaunch = null;
                deadline = Time.realtimeSinceStartup + 10f;
                while (!threadedSession.TryAdvanceStatusSeconds(0, out timedLaunch) && Time.realtimeSinceStartup < deadline) {
                    threadedSession.Pump(0, HoldThreadedCast); yield return null;
                }
                Require(timedLaunch != null, "The isolated status clock never reached a held cast.");
                while (!timedLaunch.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    threadedSession.Pump(0, HoldThreadedCast); yield return null;
                }
                Require(timedLaunch.IsCompleted && timedLaunch.GetAwaiter().GetResult()
                    && delivered.SequenceEqual(new[] { "Start:24269" }), "The isolated airborne start was not presented once.");
                threadedScope.Tick(.05f); threadedScope.LateTick();
                if (airAppearance != null) Capture("threaded-airborne.png", false);
                Task<bool> timedRecovery = null;
                while (!threadedSession.TryAdvanceStatusSeconds(.7, out timedRecovery) && Time.realtimeSinceStartup < deadline) {
                    threadedSession.Pump(0, HoldThreadedCast); yield return null;
                }
                Require(timedRecovery != null, "The isolated recovery clock never reached a held cast.");
                while (!timedRecovery.IsCompleted && Time.realtimeSinceStartup < deadline) {
                    threadedSession.Pump(0, HoldThreadedCast); yield return null;
                }
                Require(timedRecovery.IsCompleted && timedRecovery.GetAwaiter().GetResult()
                    && delivered.SequenceEqual(new[] { "Start:24269", "End:24269", "Start:24270" })
                    && FightBattleCheckpointSerializer.Save(threadedCheckpoint) == threadedCommitted,
                    "The isolated recovery changed order, repeated a view or mutated the committed checkpoint.");
            }
            threadedScope.Dispose();
            airAppearance?.Dispose();
            Destroy(airRoot.gameObject);

            // Phantom strike uses the original hit playable and source movement action.
            foreach (var direction in new[] { new[] { 1, 0, 3 }, new[] { -1, 0, 1 },
                new[] { 0, 1, 2 }, new[] { 0, -1, 0 } })
            {
                int dx = direction[0], dy = direction[1];
                var phantomUnits = new[] { LiveUnit(1, 1, 7), LiveUnit(2, 2, 7 + dx) };
                phantomUnits[0].Y = 7; phantomUnits[0].Direction = direction[2];
                phantomUnits[1].Y = 7 + dy;
                var phantomSkill = liveCatalog.CreateSkill(11270, 0, 10054, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(11270, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                phantomSkill.LimitedTimes = 1;
                phantomUnits[0].Skills.Add(phantomSkill);
                phantomUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(phantomSkill, 0));
                var phantomCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                phantomCheckpoint.Units.AddRange(phantomUnits);
                var phantomWave = new FightBattleWaveState(); phantomWave.UnitIds.Add(2);
                phantomCheckpoint.Waves.Add(phantomWave);
                FightBattleExecutor.Begin(phantomCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var phantomDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(phantomCheckpoint)).Units.ToArray();
                var phantomRoot = new GameObject("Phantom strike acceptance").transform; phantomRoot.SetParent(root, false);
                var phantomBodies = new Dictionary<int, Transform>();
                foreach (var unit in phantomUnits)
                {
                    var body = new GameObject("Phantom unit " + unit.Id).transform; body.SetParent(phantomRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    phantomBodies.Add(unit.Id, body);
                }
                var phantomAppearance = NativeJumpAppearanceFixture.Create(phantomBodies[1].gameObject);
                var phantomScope = new OfflineBattleNativePlayableScope(phantomRoot, null);
                int hitFrame = -1, damageCount = 0;
                phantomScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name == "playable11271" && leaf.name == "effect (1)") hitFrame = Time.frameCount;
                };
                using (var phantomViews = new OfficialBattleSkillEntityPlayback(phantomRoot,
                    new OfficialBattleGridMapper(phantomRoot, grid), phantomScope,
                    id => phantomDisplay.FirstOrDefault(value => value.Id == id)))
                using (var phantomGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), phantomScope, phantomViews))
                {
                    phantomViews.BindDamageUI(phantomRoot, 0);
                    foreach (var pair in phantomBodies) phantomViews.EnsureUnit(pair.Key, pair.Value);
                    Transform PhantomBody(int id) => phantomBodies.TryGetValue(id, out var value) ? value : null;
                    void PhantomDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(hitFrame == Time.frameCount && damage.SkillEntityId == 11270
                            && damage.HitEntityId == 11271 && damage.DamageEntityId == 11273
                            && damage.TargetId == 2 && damage.ElementType == 0 && damage.Damage < 0,
                            "Phantom strike damage did not arrive with its native hit effect.");
                        damageCount++;
                        phantomDisplay[Array.FindIndex(phantomDisplay, unit => unit.Id == damage.TargetId)] = projected;
                        Capture("phantom-hit-" + dx + "-" + dy + ".png", false);
                    }
                    using (var phantomDriver = new OfficialBattleLiveGraphDriver(phantomCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        phantomViews, id => id == 11270, PhantomBody, null, PhantomDamage))
                    {
                        phantomDriver.SecondsStatusChanged += _ => { };
                        phantomDisplay = phantomCheckpoint.Units.Select(unit => phantomDriver.DisplayUnit(unit.Id)).ToArray();
                        float phantomDeadline = Time.realtimeSinceStartup + 15f;
                        while (!phantomDriver.IsCompleted && Time.realtimeSinceStartup < phantomDeadline)
                        {
                            phantomDriver.Pump(Time.deltaTime); phantomViews.Tick(Time.deltaTime); phantomScope.LateTick();
                            yield return null;
                        }
                        Require(phantomDriver.TryTakeCompleted(out _, out var result) && damageCount == 1,
                            "Phantom strike did not settle one native hit.");
                        var phantomMove = result.Event.Movements.Single(value => value.UnitId == 1);
                        Require(phantomMove.MoveBehaviourId == 11272 && phantomMove.Path.Count == 4
                            && phantomMove.Path.Last().X == 7 - 3 * dx && phantomMove.Path.Last().Y == 7 - 3 * dy,
                            "Phantom strike did not retreat three cells away from its target.");
                        phantomDeadline = Time.realtimeSinceStartup + 20f;
                        while (phantomRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length > 0
                            && Time.realtimeSinceStartup < phantomDeadline)
                        {
                            phantomViews.Tick(Time.deltaTime); phantomScope.LateTick(); yield return null;
                        }
                        Require(phantomRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length == 0
                            && Vector3.Distance(phantomBodies[1].localPosition, new Vector3(7 - 3 * dx, 7 - 3 * dy, 0)) < .01f,
                            "Phantom strike native movement did not reach its settled destination.");
                    }
                }
                phantomScope.Dispose(); phantomAppearance.Dispose(); Destroy(phantomRoot.gameObject);
            }

            // The traction hit prefab starts its line first and its target attack effect later.
            foreach (var direction in new[] { new[] { 1, 0, 3 }, new[] { -1, 0, 1 },
                new[] { 0, 1, 2 }, new[] { 0, -1, 0 } })
            {
                int dx = direction[0], dy = direction[1], centerX = 7 + 3 * dx, centerY = 7 + 3 * dy;
                var pullUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7) };
                pullUnits[0].Y = 7; pullUnits[0].Direction = direction[2];
                var pullTargetUnit = LiveUnit(2, 2, centerX); pullTargetUnit.Y = centerY;
                pullUnits.Add(pullTargetUnit);
                var pullSkill = liveCatalog.CreateSkill(10700, 0, 10018, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10700, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                pullSkill.LimitedTimes = 1; pullUnits[0].Skills.Add(pullSkill);
                pullUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(pullSkill, 0));
                var pullCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                pullCheckpoint.Units.AddRange(pullUnits);
                var pullWave = new FightBattleWaveState(); pullWave.UnitIds.AddRange(pullUnits.Skip(1).Select(unit => unit.Id));
                pullCheckpoint.Waves.Add(pullWave);
                FightBattleExecutor.Begin(pullCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var pullDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(pullCheckpoint)).Units.ToArray();
                var pullRoot = new GameObject("Traction acceptance").transform; pullRoot.SetParent(root, false);
                var pullBodies = new Dictionary<int, Transform>();
                foreach (var unit in pullUnits)
                {
                    var body = new GameObject("Traction unit " + unit.Id).transform; body.SetParent(pullRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    pullBodies.Add(unit.Id, body);
                }
                var pullAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(pullBodies[1].gameObject) : null;
                var targetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(pullBodies[2].gameObject) : null;
                var pullScope = new OfflineBattleNativePlayableScope(pullRoot, null);
                int lineFrame = -1, attackFrame = -1;
                var pullDamaged = new HashSet<int>();
                pullScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10701") return;
                    if (leaf.name == "LineEffect") lineFrame = Time.frameCount;
                    if (leaf.name == "AtkEffect") attackFrame = Time.frameCount;
                };
                using (var pullViews = new OfficialBattleSkillEntityPlayback(pullRoot,
                    new OfficialBattleGridMapper(pullRoot, grid), pullScope,
                    id => pullDisplay.FirstOrDefault(value => value.Id == id)))
                using (var pullGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), pullScope, pullViews))
                {
                    pullViews.BindDamageUI(pullRoot, 0);
                    foreach (var pair in pullBodies) pullViews.EnsureUnit(pair.Key, pair.Value);
                    Transform PullBody(int id) => pullBodies.TryGetValue(id, out var value) ? value : null;
                    void PullDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(lineFrame >= 0 && attackFrame == Time.frameCount && attackFrame > lineFrame
                            && damage.SkillEntityId == 10700 && damage.HitEntityId == 10701
                            && damage.DamageEntityId == 10702 && damage.ElementType == 100 && damage.Damage < 0
                            && damage.TargetId > 1 && pullDamaged.Add(damage.TargetId),
                            "Traction damage did not match its original target attack effect: line=" + lineFrame
                                + ", attack=" + attackFrame + ", damage=" + Time.frameCount + ", target=" + damage.TargetId
                                + ", skill=" + damage.SkillEntityId + ", hit=" + damage.HitEntityId
                                + ", entity=" + damage.DamageEntityId + ", element=" + damage.ElementType
                                + ", amount=" + damage.Damage + ", alreadyHit=" + pullDamaged.Contains(damage.TargetId));
                        pullDisplay[Array.FindIndex(pullDisplay, unit => unit.Id == damage.TargetId)] = projected;
                        if (pullAppearance != null) Capture("pull-damage-" + dx + "-" + dy + ".png", false);
                    }
                    using (var pullDriver = new OfficialBattleLiveGraphDriver(pullCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        pullViews, id => id == 10700, PullBody, null, PullDamage))
                    {
                        pullDriver.SecondsStatusChanged += _ => { };
                        pullDisplay = pullCheckpoint.Units.Select(unit => pullDriver.DisplayUnit(unit.Id)).ToArray();
                        float pullDeadline = Time.realtimeSinceStartup + 15f;
                        while (!pullDriver.IsCompleted && Time.realtimeSinceStartup < pullDeadline)
                        {
                            pullDriver.Pump(Time.deltaTime); pullViews.Tick(Time.deltaTime); pullScope.LateTick(); yield return null;
                        }
                        Require(pullDriver.TryTakeCompleted(out _, out var result) && pullDamaged.Count == 1,
                            "Traction did not settle its original scope target.");
                        Require(result.Event.Movements.Any(value => value.MoveBehaviourId == 10703)
                            && result.Event.Movements.Where(value => value.MoveBehaviourId == 10703)
                                .All(value => pullDamaged.Contains(value.UnitId)),
                            "Traction did not start its original movement for the hit targets.");
                        pullDeadline = Time.realtimeSinceStartup + 20f;
                        while (pullRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length > 0
                            && Time.realtimeSinceStartup < pullDeadline)
                        {
                            pullViews.Tick(Time.deltaTime); pullScope.LateTick(); yield return null;
                        }
                        Require(pullRoot.GetComponentsInChildren<MyFightActionSkillMove>(false).Length == 0,
                            "Traction native movement did not finish.");
                    }
                }
                pullScope.Dispose(); pullAppearance?.Dispose(); targetAppearance?.Dispose(); Destroy(pullRoot.gameObject);
            }

            // Falling wind maps centre and adjacent cells to the actual sword and ground particles.
            foreach (var direction in new[] { new[] { 1, 0, 3 }, new[] { -1, 0, 1 },
                new[] { 0, 1, 2 }, new[] { 0, -1, 0 } })
            {
                int dx = direction[0], dy = direction[1], centerX = 7 + 3 * dx, centerY = 7 + 3 * dy;
                var fallUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7) };
                fallUnits[0].Y = 7; fallUnits[0].Direction = direction[2];
                foreach (var offset in new[] { new[] { 0, 0 }, new[] { -1, 0 }, new[] { 1, 0 },
                    new[] { 0, -1 }, new[] { 0, 1 } })
                {
                    var fallTarget = LiveUnit(fallUnits.Count + 1, 2, centerX + offset[0]);
                    fallTarget.Y = centerY + offset[1]; fallUnits.Add(fallTarget);
                }
                var fallSkill = liveCatalog.CreateSkill(10590, 0, 10043, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10590, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                fallSkill.LimitedTimes = 1; fallUnits[0].Skills.Add(fallSkill);
                fallUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(fallSkill, 0));
                var fallCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                fallCheckpoint.Units.AddRange(fallUnits);
                var fallWave = new FightBattleWaveState(); fallWave.UnitIds.AddRange(fallUnits.Skip(1).Select(unit => unit.Id));
                fallCheckpoint.Waves.Add(fallWave);
                FightBattleExecutor.Begin(fallCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var fallDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(fallCheckpoint)).Units.ToArray();
                var fallRoot = new GameObject("Falling wind acceptance").transform; fallRoot.SetParent(root, false);
                var fallBodies = new Dictionary<int, Transform>();
                foreach (var unit in fallUnits)
                {
                    var body = new GameObject("Falling wind unit " + unit.Id).transform; body.SetParent(fallRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    fallBodies.Add(unit.Id, body);
                }
                var fallAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(fallBodies[1].gameObject) : null;
                var fallTargetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(fallBodies[2].gameObject) : null;
                var fallScope = new OfflineBattleNativePlayableScope(fallRoot, null);
                MyPlayable fallEffect = null; Animator fallAnimator = null; ParticleSystem fallOuter = null;
                var pausedFallParticles = new List<ParticleSystem>();
                var fallDamaged = new HashSet<int>(); int centreFrame = -1, outerFrame = -1;
                bool fallResumed = false, outerResumed = false; int frozenFrames = 0, outerFrozenFrames = 0;
                fallScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10590" || leaf.name != "effect") return;
                    fallEffect = leaf;
                    if (!leaf.myApplyComponent) return;
                    fallAnimator = leaf.myApplyComponent.GetComponent<Animator>();
                    if (fallAnimator) fallAnimator.speed = 0f;
                    foreach (var particle in leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        if (particle.name == "tuci") fallOuter = particle;
                        if (particle.isPlaying) { particle.Pause(); pausedFallParticles.Add(particle); }
                    }
                };
                using (var fallViews = new OfficialBattleSkillEntityPlayback(fallRoot,
                    new OfficialBattleGridMapper(fallRoot, grid), fallScope,
                    id => fallDisplay.FirstOrDefault(value => value.Id == id)))
                using (var fallGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), fallScope, fallViews))
                {
                    fallViews.BindDamageUI(fallRoot, 0);
                    foreach (var pair in fallBodies) fallViews.EnsureUnit(pair.Key, pair.Value);
                    Transform FallBody(int id) => fallBodies.TryGetValue(id, out var value) ? value : null;
                    void FallDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(damage.SkillEntityId == 10590 && damage.HitEntityId == 10591
                            && damage.DamageEntityId == 10592 && damage.ElementType == 100 && damage.Damage < 0
                            && damage.TargetId > 1 && fallDamaged.Add(damage.TargetId) && fallEffect && fallAnimator,
                            "Falling wind produced an unexpected damage receipt.");
                        if (damage.TargetId == 2)
                        {
                            var sword = fallEffect.myApplyComponent.transform.Find("jian");
                            Require(fallResumed && sword && sword.localPosition.y <= .376f && fallOuter,
                                "Falling wind hit its centre before the original sword landed.");
                            centreFrame = Time.frameCount; fallOuter.Pause();
                        }
                        else
                        {
                            Require(centreFrame >= 0 && outerResumed && fallOuter && fallOuter.particleCount > 0
                                && Time.frameCount > centreFrame && (outerFrame < 0 || outerFrame == Time.frameCount),
                                "Falling wind did not hit adjacent cells with its original ground effect.");
                            outerFrame = Time.frameCount;
                        }
                        fallDisplay[Array.FindIndex(fallDisplay, unit => unit.Id == damage.TargetId)] = projected;
                        if (fallAppearance != null) Capture("falling-wind-damage-" + dx + "-" + dy + ".png", false);
                    }
                    using (var fallDriver = new OfficialBattleLiveGraphDriver(fallCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        fallViews, id => id == 10590, FallBody, null, FallDamage))
                    {
                        fallDriver.SecondsStatusChanged += _ => { };
                        fallDisplay = fallCheckpoint.Units.Select(unit => fallDriver.DisplayUnit(unit.Id)).ToArray();
                        float fallDeadline = Time.realtimeSinceStartup + 15f;
                        while (!fallDriver.IsCompleted && Time.realtimeSinceStartup < fallDeadline)
                        {
                            fallDriver.Pump(Time.deltaTime); fallViews.Tick(Time.deltaTime); fallScope.LateTick();
                            if (fallEffect && !fallResumed && ++frozenFrames >= 8)
                            {
                                Require(fallAnimator && fallOuter && fallDamaged.Count == 0,
                                    "A frozen falling sword settled damage before moving.");
                                fallAnimator.speed = 1f; foreach (var particle in pausedFallParticles) if (particle) particle.Play();
                                fallResumed = true;
                            }
                            if (centreFrame >= 0 && !outerResumed && ++outerFrozenFrames >= 8)
                            {
                                Require(fallDamaged.Count == 1 && fallOuter && fallOuter.isPaused,
                                    "Frozen ground particles settled an adjacent target.");
                                fallOuter.Play(); outerResumed = true;
                            }
                            yield return null;
                        }
                        Require(fallDriver.TryTakeCompleted(out _, out _) && fallDamaged.Count == 5
                            && centreFrame >= 0 && outerFrame > centreFrame && fallResumed && outerResumed,
                            "Falling wind did not settle its five original scope targets.");
                    }
                }
                fallScope.Dispose(); fallAppearance?.Dispose(); fallTargetAppearance?.Dispose(); Destroy(fallRoot.gameObject);
            }

            // A hit-triggered passive creates its own zero-time native effect and later wind hit.
            foreach (var direction in new[] { new[] { 1, 0, 3, 0 }, new[] { -1, 0, 1, 0 },
                new[] { 0, 1, 2, 0 }, new[] { 0, -1, 0, 0 }, new[] { 1, 0, 3, 1 } })
            {
                int dx = direction[0], dy = direction[1]; bool shielded = direction[3] != 0;
                var swordWindUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + dx) };
                swordWindUnits[0].Direction = direction[2]; swordWindUnits[1].Y = 5 + dy;
                var swordWindSkill = liveCatalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10600, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                swordWindSkill.LimitedTimes = 1; swordWindUnits[0].Skills.Add(swordWindSkill);
                swordWindUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(swordWindSkill, 0));
                var swordWindStatus = liveCatalog.CreateStatus(25025, 1, 1, 1, 25025, 10562, 2, 20, 1);
                swordWindStatus.Components.HitSkill.Rate = 1f; // Deterministic trigger; its original rate is tested separately.
                swordWindUnits[0].Statuses.Add(swordWindStatus);
                if (shielded) swordWindUnits[1].Statuses.Add(new FightBattleStatusState {
                    ClassId = 90003, InstanceId = 1, OwnerId = 2, CreatorId = 2,
                    ShieldKind = FightBattleShieldKind.Persistent, ShieldInitialized = true,
                    ShieldValue = 100000000, ShieldRemainValue = 100000000 });
                var swordWindCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                swordWindCheckpoint.Units.AddRange(swordWindUnits);
                var swordWindWave = new FightBattleWaveState(); swordWindWave.UnitIds.Add(2);
                swordWindCheckpoint.Waves.Add(swordWindWave);
                FightBattleExecutor.Begin(swordWindCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var swordWindDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(swordWindCheckpoint)).Units.ToArray();
                var swordWindRoot = new GameObject("Sword wind acceptance").transform; swordWindRoot.SetParent(root, false);
                var swordWindBodies = new Dictionary<int, Transform>();
                foreach (var unit in swordWindUnits)
                {
                    var body = new GameObject("Sword wind unit " + unit.Id).transform; body.SetParent(swordWindRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    swordWindBodies.Add(unit.Id, body);
                }
                var swordWindAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(swordWindBodies[1].gameObject) : null;
                var swordWindTargetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(swordWindBodies[2].gameObject) : null;
                var swordWindScope = new OfflineBattleNativePlayableScope(swordWindRoot, null);
                ParticleSystem swordWindRing = null; int baseHits = 0, passiveHits = 0, frozenFrames = 0, graphLeafCount = 0;
                bool swordWindResumed = false;
                swordWindScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable11290" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    foreach (var particle in leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                        if (particle.name == "yuanquan (1)") { swordWindRing = particle; particle.Pause(); }
                };
                using (var swordWindViews = new OfficialBattleSkillEntityPlayback(swordWindRoot,
                    new OfficialBattleGridMapper(swordWindRoot, grid), swordWindScope,
                    id => swordWindDisplay.FirstOrDefault(value => value.Id == id)))
                using (var swordWindGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), swordWindScope, swordWindViews))
                {
                    swordWindViews.BindDamageUI(swordWindRoot, 0);
                    foreach (var pair in swordWindBodies) swordWindViews.EnsureUnit(pair.Key, pair.Value);
                    Transform SwordWindBody(int id) => swordWindBodies.TryGetValue(id, out var value) ? value : null;
                    void SwordWindDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(damage.TargetId == 2 && (!shielded || damage.BeforeHp == damage.AfterHp),
                            "Sword wind hit the wrong target or bypassed its shield.");
                        if (damage.SkillEntityId == 10600) baseHits++;
                        else
                        {
                            Require(damage.SkillEntityId == 11290 && damage.HitEntityId == 11291
                                && damage.DamageEntityId == 11292 && damage.ElementType == 100 && baseHits == 1
                                && swordWindResumed && swordWindRing && swordWindRing.particleCount > 0,
                                "Sword wind did not settle with its original target effect.");
                            passiveHits++;
                            if (swordWindAppearance != null) Capture("sword-wind-damage-" + dx + "-" + dy + "-" + direction[3] + ".png", false);
                        }
                        swordWindDisplay[Array.FindIndex(swordWindDisplay, unit => unit.Id == damage.TargetId)] = projected;
                    }
                    using (var swordWindDriver = new OfficialBattleLiveGraphDriver(swordWindCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        swordWindViews, id => id == 10600 || id == 11290, SwordWindBody, null, SwordWindDamage))
                    {
                        swordWindDriver.SecondsStatusChanged += _ => { };
                        swordWindDriver.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                            if (cast.SkillEntityId == 11290 && prefab.name == "playable11290" && leaf.name == "effect") graphLeafCount++;
                        };
                        swordWindDisplay = swordWindCheckpoint.Units.Select(unit => swordWindDriver.DisplayUnit(unit.Id)).ToArray();
                        float swordWindDeadline = Time.realtimeSinceStartup + 15f;
                        while (!swordWindDriver.IsCompleted && Time.realtimeSinceStartup < swordWindDeadline)
                        {
                            swordWindDriver.Pump(Time.deltaTime); swordWindViews.Tick(Time.deltaTime); swordWindScope.LateTick();
                            if (swordWindRing && !swordWindResumed && ++frozenFrames >= 8)
                            {
                                Require(swordWindRing.isPaused && baseHits == 1 && passiveHits == 0,
                                    "A frozen sword-wind effect settled its passive hit.");
                                swordWindRing.Play(); swordWindResumed = true;
                            }
                            yield return null;
                        }
                        Require(swordWindDriver.TryTakeCompleted(out _, out var result) && baseHits == 1 && passiveHits == 1 && graphLeafCount == 1
                            && result.Event.SkillCasts.Count(value => value.SkillEntityId == 11290) == 1,
                            "The hit passive did not settle its original child exactly once.");
                    }
                }
                swordWindScope.Dispose(); swordWindAppearance?.Dispose(); swordWindTargetAppearance?.Dispose(); Destroy(swordWindRoot.gameObject);
            }

            // Water's native track must reach its target before the hit is released.
            foreach (var direction in new[] { new[] { 1, 0, 3, 3, 0 }, new[] { -1, 0, 1, 1, 0 },
                new[] { 0, 1, 2, 4, 0 }, new[] { 0, -1, 0, 2, 0 }, new[] { 1, 0, 3, 3, 1 },
                new[] { 1, 0, 3, 3, 2 } })
            {
                int dx = direction[0], dy = direction[1], distance = direction[3]; bool cancel = direction[4] != 0;
                var waterUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + dx * distance) };
                waterUnits[0].Direction = direction[2]; waterUnits[1].Y = 5 + dy * distance;
                var waterSkill = liveCatalog.CreateSkill(10500, 0, 10000, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10500, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                waterSkill.LimitedTimes = 1; waterUnits[0].Skills.Add(waterSkill);
                waterUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(waterSkill, 0));
                var waterCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                waterCheckpoint.Units.AddRange(waterUnits);
                var waterWave = new FightBattleWaveState(); waterWave.UnitIds.Add(2); waterCheckpoint.Waves.Add(waterWave);
                FightBattleExecutor.Begin(waterCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var waterDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(waterCheckpoint)).Units.ToArray();
                var waterRoot = new GameObject("Water projectile acceptance").transform; waterRoot.SetParent(root, false);
                var waterBodies = new Dictionary<int, Transform>();
                foreach (var unit in waterUnits)
                {
                    var body = new GameObject("Water unit " + unit.Id).transform; body.SetParent(waterRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    waterBodies.Add(unit.Id, body);
                }
                var waterAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(waterBodies[1].gameObject) : null;
                var waterTargetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(waterBodies[2].gameObject) : null;
                var waterScope = new OfflineBattleNativePlayableScope(waterRoot, null);
                Zjcs.Official.BattleTrackPlayables.MyPlayableTrack waterTrack = null;
                int waterHits = 0, waterPausedFrames = 0; bool waterResumed = false, waterStopped = false;
                waterScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name == "playable10501" && leaf.name == "effect")
                    {
                        waterTrack = leaf as Zjcs.Official.BattleTrackPlayables.MyPlayableTrack;
                        if (!cancel) Time.timeScale = 0f;
                    }
                };
                using (var waterViews = new OfficialBattleSkillEntityPlayback(waterRoot,
                    new OfficialBattleGridMapper(waterRoot, grid), waterScope,
                    id => waterDisplay.FirstOrDefault(value => value.Id == id)))
                using (var waterGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), waterScope, waterViews))
                {
                    waterViews.BindDamageUI(waterRoot, 0);
                    foreach (var pair in waterBodies) waterViews.EnsureUnit(pair.Key, pair.Value);
                    Transform WaterBody(int id) => waterBodies.TryGetValue(id, out var value) ? value : null;
                    void WaterDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(!cancel && waterResumed && waterTrack && waterTrack.myApplyComponent && waterTrack.myTarget
                            && Vector3.Distance(waterTrack.myApplyComponent.transform.position, waterTrack.myTarget.transform.position) < .01f
                            && damage.SkillEntityId == 10500 && damage.HitEntityId == 10501 && damage.DamageEntityId == 10502
                            && damage.TargetId == 2 && damage.ElementType == 101,
                            "Water damage did not coincide with the native projectile reaching its target.");
                        waterHits++;
                        if (waterAppearance != null) Capture("water-impact-" + dx + "-" + dy + ".png", false);
                        waterDisplay[Array.FindIndex(waterDisplay, unit => unit.Id == damage.TargetId)] = projected;
                    }
                    using (var waterDriver = new OfficialBattleLiveGraphDriver(waterCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        waterViews, id => id == 10500, WaterBody, null, WaterDamage))
                    {
                        waterDriver.SecondsStatusChanged += _ => { };
                        waterDisplay = waterCheckpoint.Units.Select(unit => waterDriver.DisplayUnit(unit.Id)).ToArray();
                        float waterDeadline = Time.realtimeSinceStartup + 15f;
                        while (!waterDriver.IsCompleted && Time.realtimeSinceStartup < waterDeadline)
                        {
                            waterDriver.Pump(Time.deltaTime); waterViews.Tick(Time.deltaTime); waterScope.LateTick();
                            if (waterTrack && !cancel && !waterResumed && ++waterPausedFrames >= 8)
                            {
                                Require(waterHits == 0 && waterTrack.Time == 0f, "Paused water projectile dealt damage or moved.");
                                Time.timeScale = 1f; waterResumed = true;
                            }
                            if (waterTrack && cancel && !waterStopped && waterTrack.Time > 0f)
                            {
                                Require(waterTrack.Time < waterTrack.GetDuration(), "Water projectile already reached its target.");
                                if (direction[4] == 2)
                                {
                                    Require(Vector3.Distance(waterTrack.myApplyComponent.transform.position,
                                        waterTrack.myTarget.transform.position) > .01f, "Water projectile already reached its target.");
                                    typeof(MyPlayable).GetProperty("Time").SetValue(waterTrack, waterTrack.GetDuration());
                                }
                                waterTrack.StopBeforeResetData(); waterTrack.Stop(true); waterStopped = true;
                            }
                            if (waterStopped && ++waterPausedFrames >= 8) break;
                            yield return null;
                        }
                        if (cancel) Require(waterStopped && waterHits == 0 && !waterDriver.IsCompleted,
                            "Cancelled water projectile settled its hit.");
                        else Require(waterDriver.TryTakeCompleted(out _, out _) && waterHits == 1 && waterResumed,
                            "Water projectile did not settle exactly once.");
                    }
                    Require(!waterTrack || waterTrack.myTrackTriggerList.Count == 0,
                        "Water projectile retained its temporary hit observer.");
                }
                Time.timeScale = 1f; waterScope.Dispose(); waterAppearance?.Dispose(); waterTargetAppearance?.Dispose(); Destroy(waterRoot.gameObject);
            }

            // The water passive's zero-time child appears on the struck target after the projectile lands.
            foreach (var direction in new[] { new[] { 1, 0, 3, 0 }, new[] { -1, 0, 1, 0 },
                new[] { 0, 1, 2, 0 }, new[] { 0, -1, 0, 0 }, new[] { 1, 0, 3, 1 }, new[] { 1, 0, 3, 2 } })
            {
                int dx = direction[0], dy = direction[1]; bool shielded = direction[3] == 1, fastForward = direction[3] == 2;
                bool expectFrozen = dx == 1 && dy == 0 && !shielded;
                var iceUnits = new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + dx * 2) };
                iceUnits[0].Direction = direction[2]; iceUnits[1].Y = 5 + dy * 2;
                var iceSkill = liveCatalog.CreateSkill(10500, 0, 10000, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10500, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                iceSkill.LimitedTimes = 1; iceUnits[0].Skills.Add(iceSkill);
                iceUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(iceSkill, 0));
                var iceStatus = liveCatalog.CreateStatus(25026, 1, 1, 1, 25026, 10560, 2, 20, 1);
                iceStatus.Components.HitSkill.Rate = 1f; // Original trigger probability is verified by the rule test.
                iceUnits[0].Statuses.Add(iceStatus);
                if (shielded) iceUnits[1].Statuses.Add(new FightBattleStatusState {
                    ClassId = 90003, InstanceId = 1, OwnerId = 2, CreatorId = 2,
                    ShieldKind = FightBattleShieldKind.Persistent, ShieldInitialized = true,
                    ShieldValue = 100000000, ShieldRemainValue = 100000000 });
                var iceCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                iceCheckpoint.Units.AddRange(iceUnits);
                var iceWave = new FightBattleWaveState(); iceWave.UnitIds.Add(2); iceCheckpoint.Waves.Add(iceWave);
                FightBattleExecutor.Begin(iceCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                bool foundSeed = false;
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    var reference = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(iceCheckpoint));
                    reference.RandomSeed = 73 + attempt * 1234567;
                    FightBattleExecutor.Restore(reference, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
                    if (reference.Units.Single(unit => unit.Id == 2).Statuses.Any(status => status.ClassId == 24215) != expectFrozen) continue;
                    iceCheckpoint.RandomSeed = 73 + attempt * 1234567; foundSeed = true; break;
                }
                Require(foundSeed, "Could not select the original freeze outcome for the ice passive.");
                var iceDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(iceCheckpoint)).Units.ToArray();
                var iceRoot = new GameObject("Ice passive acceptance").transform; iceRoot.SetParent(root, false);
                var iceBodies = new Dictionary<int, Transform>();
                foreach (var unit in iceUnits)
                {
                    var body = new GameObject("Ice unit " + unit.Id).transform; body.SetParent(iceRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    iceBodies.Add(unit.Id, body);
                }
                var iceAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(iceBodies[1].gameObject) : null;
                var iceTargetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(iceBodies[2].gameObject) : null;
                var iceScope = new OfflineBattleNativePlayableScope(iceRoot, null);
                ParticleSystem iceParticle = null; int waterHits = 0, iceHits = 0, icePausedFrames = 0, iceGraphLeaves = 0;
                bool iceResumed = false;
                iceScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable11300" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    foreach (var particle in leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                        if (particle.name == "hit_da") { iceParticle = particle; particle.Pause(); }
                };
                using (var iceViews = new OfficialBattleSkillEntityPlayback(iceRoot,
                    new OfficialBattleGridMapper(iceRoot, grid), iceScope,
                    id => iceDisplay.FirstOrDefault(value => value.Id == id)))
                using (var iceGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), iceScope, iceViews))
                {
                    iceViews.BindDamageUI(iceRoot, 0);
                    foreach (var pair in iceBodies) iceViews.EnsureUnit(pair.Key, pair.Value);
                    Transform IceBody(int id) => iceBodies.TryGetValue(id, out var value) ? value : null;
                    void IceDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(damage.TargetId == 2 && (!shielded || damage.BeforeHp == damage.AfterHp),
                            "Ice passive hit the wrong target or bypassed its shield.");
                        if (damage.SkillEntityId == 10500) waterHits++;
                        else
                        {
                            Require(damage.SkillEntityId == 11300 && damage.HitEntityId == 11301
                                && damage.DamageEntityId == 11302 && damage.SkillItemId == 10560 && damage.ElementType == 101
                                && waterHits == 1 && iceResumed && iceParticle
                                && (iceParticle.particleCount > 0 || fastForward && iceParticle.time >= .2f),
                                "Ice passive damage did not coincide with its original target effect.");
                            iceHits++;
                            if (iceAppearance != null) Capture("ice-passive-" + dx + "-" + dy + "-" + direction[3] + ".png", false);
                        }
                        iceDisplay[Array.FindIndex(iceDisplay, unit => unit.Id == damage.TargetId)] = projected;
                    }
                    using (var iceDriver = new OfficialBattleLiveGraphDriver(iceCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        iceViews, id => id == 10500 || id == 11300, IceBody, null, IceDamage))
                    {
                        iceDriver.SecondsStatusChanged += _ => { };
                        iceDriver.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                            if (cast.SkillEntityId == 11300 && prefab.name == "playable11300" && leaf.name == "effect") iceGraphLeaves++;
                        };
                        iceDisplay = iceCheckpoint.Units.Select(unit => iceDriver.DisplayUnit(unit.Id)).ToArray();
                        float iceDeadline = Time.realtimeSinceStartup + 15f;
                        while (!iceDriver.IsCompleted && Time.realtimeSinceStartup < iceDeadline)
                        {
                            iceDriver.Pump(Time.deltaTime); iceViews.Tick(Time.deltaTime); iceScope.LateTick();
                            if (iceParticle && !iceResumed && ++icePausedFrames >= 8)
                            {
                                Require(iceParticle.isPaused && waterHits == 1 && iceHits == 0,
                                    "A paused ice effect settled its child hit.");
                                if (fastForward)
                                {
                                    iceParticle.Simulate(.2f, true, true, false);
                                    Require(iceParticle.time >= .2f && iceParticle.particleCount == 0,
                                        "The skipped ice burst fixture did not advance past the particle.");
                                }
                                iceParticle.Play(); iceResumed = true;
                            }
                            yield return null;
                        }
                        Require(iceDriver.TryTakeCompleted(out var executor, out var result)
                            && waterHits == 1 && iceHits == 1 && iceGraphLeaves == 1
                            && result.Event.SkillCasts.Count(value => value.SkillEntityId == 11300) == 1,
                            "Ice passive did not settle its original child exactly once.");
                        var frozen = executor.State.Units.Single(unit => unit.Id == 2).Statuses
                            .Where(status => status.ClassId == 24215).ToArray();
                        Require(expectFrozen ? frozen.Length == 1 && frozen[0].DurationRound == 1
                            && frozen[0].CreatorId == 1 && frozen[0].ApplyingSkillItemId == 10560 : frozen.Length == 0,
                            "Ice passive did not preserve its original freezing result.");
                    }
                }
                iceScope.Dispose(); iceAppearance?.Dispose(); iceTargetAppearance?.Dispose(); Destroy(iceRoot.gameObject);
            }

            // Unstable aura applies its wind hit before the native knockback action.
            foreach (var scenario in new[] { new[] { 1, 0, 3, 0 }, new[] { -1, 0, 1, 0 },
                new[] { 0, 1, 2, 0 }, new[] { 0, -1, 0, 0 }, new[] { 1, 0, 3, 1 },
                new[] { 1, 0, 3, 2 }, new[] { 1, 0, 3, 3 } })
            {
                int dx = scenario[0], dy = scenario[1], mode = scenario[3];
                var auraUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 5), LiveUnit(2, 2, 5 + dx * 2) };
                auraUnits[0].Direction = scenario[2]; auraUnits[1].Y = 5 + dy * 2;
                auraUnits[0].Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 20000 });
                if (mode == 2)
                {
                    var blocker = LiveUnit(3, 2, 5 + dx * 3); blocker.Y = 5 + dy * 3; auraUnits.Add(blocker);
                }
                if (mode == 3) auraUnits[1].Statuses.Add(new FightBattleStatusState {
                    ClassId = 90003, InstanceId = 1, OwnerId = 2, CreatorId = 2,
                    ShieldKind = FightBattleShieldKind.Persistent, ShieldInitialized = true,
                    ShieldValue = 100000000, ShieldRemainValue = 100000000 });
                var auraSkill = liveCatalog.CreateSkill(10500, 0, 10000, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10500, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                auraSkill.LimitedTimes = 1; auraUnits[0].Skills.Add(auraSkill);
                auraUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(auraSkill, 0));
                var auraStatus = liveCatalog.CreateStatus(25005, 1, 1, 1, 25005, 10512, 2, 20, 1);
                auraStatus.Components.HitSkill.Rate = 1f; // Original trigger probability is verified by the rule test.
                auraUnits[0].Statuses.Add(auraStatus);
                var auraCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                if (mode == 1) auraCheckpoint.Grid.BlockedPositions.Add(new FightGridPoint(5 + dx * 3, 5 + dy * 3));
                auraCheckpoint.Units.AddRange(auraUnits); var auraWave = new FightBattleWaveState();
                auraWave.UnitIds.AddRange(auraUnits.Where(unit => unit.Side == 2).Select(unit => unit.Id)); auraCheckpoint.Waves.Add(auraWave);
                FightBattleExecutor.Begin(auraCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                bool auraSeedFound = false;
                for (int attempt = 0; attempt < 64; attempt++)
                {
                    var reference = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(auraCheckpoint));
                    reference.RandomSeed = 73 + attempt * 1234567;
                    var baseline = FightBattleExecutor.Restore(reference, formula, new FightDamageSettlement(),
                        liveCatalog, combatAssets.LoadSkillItems()).Step(0, 1);
                    var expectedCollisions = mode == 1 ? new[] { 2 } : mode == 2 ? new[] { 2, 3 } : Array.Empty<int>();
                    if (!baseline.Event.Movements.Any(value => value.UnitId == 2 && value.MoveBehaviourId == 10853)
                        || !new HashSet<int>(baseline.Event.DamageEvents.Where(value => value.SkillEntityId == 10100)
                            .Select(value => value.TargetId)).SetEquals(expectedCollisions)) continue;
                    auraCheckpoint.RandomSeed = 73 + attempt * 1234567; auraSeedFound = true; break;
                }
                Require(auraSeedFound, "Could not select the original aura knockback outcome for mode " + mode);
                var auraDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(auraCheckpoint)).Units.ToArray();
                var auraRoot = new GameObject("Unstable aura acceptance").transform; auraRoot.SetParent(root, false);
                var auraBodies = new Dictionary<int, Transform>();
                foreach (var unit in auraUnits)
                {
                    var body = new GameObject("Aura unit " + unit.Id).transform; body.SetParent(auraRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    auraBodies.Add(unit.Id, body);
                }
                var auraAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(auraBodies[1].gameObject) : null;
                var auraTargetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(auraBodies[2].gameObject) : null;
                var auraScope = new OfflineBattleNativePlayableScope(auraRoot, null);
                ParticleSystem auraParticle = null; int waterHits = 0, auraHits = 0, auraPausedFrames = 0, contactFrame = -1;
                bool auraResumed = false; var collisionTargets = new HashSet<int>();
                auraScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name != "playable10850" || leaf.name != "effect" || !leaf.myApplyComponent) return;
                    foreach (var particle in leaf.myApplyComponent.GetComponentsInChildren<ParticleSystem>(true))
                        if (particle.name == "hit (4)") { auraParticle = particle; particle.Pause(); }
                };
                using (var auraViews = new OfficialBattleSkillEntityPlayback(auraRoot,
                    new OfficialBattleGridMapper(auraRoot, grid), auraScope,
                    id => auraDisplay.FirstOrDefault(value => value.Id == id)))
                using (var auraGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), auraScope, auraViews))
                {
                    auraViews.BindDamageUI(auraRoot, 0);
                    foreach (var pair in auraBodies) auraViews.EnsureUnit(pair.Key, pair.Value);
                    Transform AuraBody(int id) => auraBodies.TryGetValue(id, out var value) ? value : null;
                    void AuraDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        if (damage.SkillEntityId == 10500) { Require(damage.TargetId == 2, "Water hit the wrong aura target."); waterHits++; }
                        else if (damage.SkillEntityId == 10850)
                        {
                            Require(damage.HitEntityId == 10851 && damage.DamageEntityId == 10852
                                && damage.SkillItemId == 10512 && damage.ElementType == 100 && damage.TargetId == 2
                                && (mode != 3 || damage.BeforeHp == damage.AfterHp) && waterHits == 1
                                && auraResumed && auraParticle && auraParticle.particleCount > 0,
                                "Unstable aura did not settle with its original target effect.");
                            auraHits++;
                            if (auraAppearance != null) Capture("unstable-aura-" + dx + "-" + dy + "-" + mode + ".png", false);
                        }
                        else
                            Require(damage.SkillEntityId == 10100 && damage.TrueDamage && damage.Damage == -600
                                && contactFrame == Time.frameCount && collisionTargets.Add(damage.TargetId),
                                "Aura collision damage did not match the native contact.");
                        auraDisplay[Array.FindIndex(auraDisplay, unit => unit.Id == damage.TargetId)] = projected;
                    }
                    using (var auraDriver = new OfficialBattleLiveGraphDriver(auraCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        auraViews, id => id == 10500 || id == 10850 || id == 10100, AuraBody, null, AuraDamage))
                    {
                        auraDriver.SecondsStatusChanged += _ => { };
                        auraDriver.BlockedContact += action => {
                            Require(action.UnitId == 2 && action.MoveBehaviourId == 10853 && auraHits == 1,
                                "The aura collision used the wrong original movement.");
                            contactFrame = Time.frameCount;
                        };
                        auraDisplay = auraCheckpoint.Units.Select(unit => auraDriver.DisplayUnit(unit.Id)).ToArray();
                        float auraDeadline = Time.realtimeSinceStartup + 20f;
                        while (!auraDriver.IsCompleted && Time.realtimeSinceStartup < auraDeadline)
                        {
                            auraDriver.Pump(Time.deltaTime); auraViews.Tick(Time.deltaTime); auraScope.LateTick();
                            if (auraParticle && !auraResumed && ++auraPausedFrames >= 8)
                            {
                                Require(auraParticle.isPaused && waterHits == 1 && auraHits == 0,
                                    "A paused aura effect settled its child hit.");
                                auraParticle.Play(); auraResumed = true;
                            }
                            yield return null;
                        }
                        Require(auraDriver.TryTakeCompleted(out var executor, out var result) && waterHits == 1 && auraHits == 1
                            && result.Event.SkillCasts.Count(value => value.SkillEntityId == 10850) == 1,
                            "Unstable aura did not settle its original child exactly once.");
                        var moved = executor.State.Units.Single(unit => unit.Id == 2);
                        Require(moved.X == 5 + dx * (mode == 1 || mode == 2 ? 2 : 3)
                            && moved.Y == 5 + dy * (mode == 1 || mode == 2 ? 2 : 3)
                            && result.Event.Movements.Any(value => value.UnitId == 2 && value.MoveBehaviourId == 10853)
                            && collisionTargets.SetEquals(mode == 1 ? new[] { 2 } : mode == 2 ? new[] { 2, 3 } : Array.Empty<int>()),
                            "Unstable aura did not preserve its original knockback or collision targets: direction="
                            + dx + "," + dy + " mode=" + mode + " position=" + moved.X + "," + moved.Y
                            + " collision=" + string.Join(",", collisionTargets)
                            + " moves=" + string.Join(",", result.Event.Movements.Select(value => value.UnitId + ":" + value.MoveBehaviourId)));
                    }
                }
                auraScope.Dispose(); auraAppearance?.Dispose(); auraTargetAppearance?.Dispose(); Destroy(auraRoot.gameObject);
            }

            // Fireball's shared explosion must wait for its own original projectile.
            Vector3 fireballPreviousCamera = cameraView.transform.position;
            foreach (var scenario in new[] { new[] { 1, 0, 3, 0 }, new[] { -1, 0, 1, 0 },
                new[] { 0, 1, 2, 0 }, new[] { 0, -1, 0, 0 }, new[] { 1, 0, 3, 1 } })
            {
                int dx = scenario[0], dy = scenario[1]; bool cancel = scenario[3] != 0;
                int centerX = 7 + 3 * dx, centerY = 7 + 3 * dy;
                cameraView.transform.position = new Vector3(7 + 1.5f * dx, 7 + 1.5f * dy, fireballPreviousCamera.z);
                var fireballUnits = new List<FightBattleUnitState> { LiveUnit(1, 1, 7) };
                fireballUnits[0].Y = 7; fireballUnits[0].Direction = scenario[2];
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                {
                    var unit = LiveUnit(fireballUnits.Count + 1, 2, centerX + x); unit.Y = centerY + y; fireballUnits.Add(unit);
                }
                var fireballSkill = liveCatalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                    liveCatalog.CalculateSkillProps(10800, 2, 20, 1)
                        .Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                fireballSkill.LimitedTimes = 1; fireballUnits[0].Skills.Add(fireballSkill);
                fireballUnits[0].SkillState.Slots.Add(liveCatalog.CreateSkillSlot(fireballSkill, 0));
                var fireballCheckpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                fireballCheckpoint.Units.AddRange(fireballUnits);
                var fireballWave = new FightBattleWaveState(); fireballWave.UnitIds.AddRange(fireballUnits.Skip(1).Select(unit => unit.Id));
                fireballCheckpoint.Waves.Add(fireballWave);
                FightBattleExecutor.Begin(fireballCheckpoint, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems());
                var fireballDisplay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(fireballCheckpoint)).Units.ToArray();
                var fireballRoot = new GameObject("Fireball arrival acceptance").transform; fireballRoot.SetParent(root, false);
                var fireballBodies = new Dictionary<int, Transform>();
                foreach (var unit in fireballUnits)
                {
                    var body = new GameObject("Fireball unit " + unit.Id).transform; body.SetParent(fireballRoot, false);
                    body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false;
                    renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                    fireballBodies.Add(unit.Id, body);
                }
                var fireballAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(fireballBodies[1].gameObject) : null;
                var fireballTargetAppearance = OfficialBattleHitPlaybackAcceptance.PlayerJump
                    ? NativeJumpAppearanceFixture.Create(fireballBodies[6].gameObject) : null;
                var fireballScope = new OfflineBattleNativePlayableScope(fireballRoot, null);
                Zjcs.Official.BattleTrackPlayables.MyPlayableTrack fireballTrack = null;
                var fireballTargets = new HashSet<int>(); int explosionFrame = -1, fireballPausedFrames = 0;
                bool fireballResumed = false, fireballStopped = false; float fireballPausedTime = 0f, trackSpeed = 0f;
                fireballScope.NativePlayableStarted += (prefab, leaf) => {
                    if (prefab.name == "playable_10801_SourceToHit" && leaf.name == "track")
                    {
                        fireballTrack = leaf as Zjcs.Official.BattleTrackPlayables.MyPlayableTrack;
                        if (fireballTrack) { trackSpeed = fireballTrack.Speed; typeof(MyPlayable).GetProperty("Speed").SetValue(fireballTrack, 0f); }
                    }
                    if (prefab.name == "playable10801") explosionFrame = Time.frameCount;
                };
                using (var fireballViews = new OfficialBattleSkillEntityPlayback(fireballRoot,
                    new OfficialBattleGridMapper(fireballRoot, grid), fireballScope,
                    id => fireballDisplay.FirstOrDefault(value => value.Id == id)))
                using (var fireballGraph = new OfficialBattleSkillGraphPlayback(
                    OfficialBattleSkillGraphCatalog.TryLoadDefault(), fireballScope, fireballViews))
                {
                    fireballViews.BindDamageUI(fireballRoot, 0);
                    foreach (var pair in fireballBodies) fireballViews.EnsureUnit(pair.Key, pair.Value);
                    Transform FireballBody(int id) => fireballBodies.TryGetValue(id, out var value) ? value : null;
                    void FireballDamage(FightBattleDamageEvent damage, FightBattleUnitState projected)
                    {
                        Require(!cancel && fireballResumed && fireballTrack && fireballTrack.myApplyComponent && fireballTrack.myTarget
                            && Vector3.Distance(fireballTrack.myApplyComponent.transform.position, fireballTrack.myTarget.transform.position) < .01f
                            && explosionFrame == Time.frameCount && damage.SkillEntityId == 10800 && damage.HitEntityId == 10801
                            && damage.DamageEntityId == 10023 && damage.ElementType == 102 && fireballTargets.Add(damage.TargetId),
                            "Fireball damage did not coincide with its original projectile and explosion.");
                        fireballDisplay[Array.FindIndex(fireballDisplay, unit => unit.Id == damage.TargetId)] = projected;
                        if (fireballTargets.Count == 1 && fireballAppearance != null)
                            Capture("fireball-arrival-" + dx + "-" + dy + ".png", false);
                    }
                    using (var fireballDriver = new OfficialBattleLiveGraphDriver(fireballCheckpoint,
                        copy => FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), liveCatalog, combatAssets.LoadSkillItems()),
                        fireballViews, id => id == 10800, FireballBody, null, FireballDamage))
                    {
                        fireballDriver.SecondsStatusChanged += _ => { };
                        fireballDisplay = fireballCheckpoint.Units.Select(unit => fireballDriver.DisplayUnit(unit.Id)).ToArray();
                        float fireballDeadline = Time.realtimeSinceStartup + 20f;
                        while (!fireballDriver.IsCompleted && Time.realtimeSinceStartup < fireballDeadline)
                        {
                            fireballDriver.Pump(Time.deltaTime); fireballViews.Tick(Time.deltaTime); fireballScope.LateTick();
                            if (fireballTrack && !fireballResumed && !fireballStopped)
                            {
                                fireballPausedFrames++; fireballPausedTime += Time.deltaTime;
                                Require(fireballTargets.Count == 0 && explosionFrame < 0 && fireballTrack.Time == 0f,
                                    "A paused fireball spawned its explosion or settled damage.");
                                if (fireballPausedTime >= fireballTrack.GetDuration() + .05f)
                                {
                                    if (cancel) { fireballTrack.StopBeforeResetData(); fireballTrack.Stop(true); fireballStopped = true; }
                                    else { typeof(MyPlayable).GetProperty("Speed").SetValue(fireballTrack, trackSpeed); fireballResumed = true; }
                                }
                            }
                            if (fireballStopped && ++fireballPausedFrames >= 120) break;
                            yield return null;
                        }
                        if (cancel) Require(fireballStopped && fireballTargets.Count == 0 && explosionFrame < 0 && !fireballDriver.IsCompleted,
                            "Cancelled fireball spawned its explosion or settled damage.");
                        else Require(fireballDriver.TryTakeCompleted(out _, out _) && fireballResumed && fireballTargets.Count == 9,
                            "Fireball did not hit all nine original scope cells exactly once.");
                        if (!cancel && fireballAppearance != null)
                        {
                            yield return null; fireballViews.Tick(Time.deltaTime); fireballScope.LateTick();
                            Capture("fireball-explosion-" + dx + "-" + dy + ".png", false);
                        }
                    }
                    Require(!fireballTrack || fireballTrack.myTrackTriggerList.Count == 0,
                        "Fireball retained its temporary projectile observer.");
                }
                fireballScope.Dispose(); fireballAppearance?.Dispose(); fireballTargetAppearance?.Dispose(); Destroy(fireballRoot.gameObject);
            }
            cameraView.transform.position = fireballPreviousCamera;

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

            // Consume the production executor's eight independent random draws,
            // including empty cells; no damage callback creates these views.
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            for (int id = 1; id <= 2; id++)
            {
                var unit = Unit(id, 2 + 3 * (id - 1), 5);
                unit.Side = id; unit.Level = 1; unit.Hp = unit.MaxHp = 5000;
                unit.Turn = new FightTurnUnit { Id = id, Hp = 5000, Speed = 100 - id };
                unit.Properties.Add(new FightPropertyValue { Id = 3, Value = 5000 });
                unit.Properties.Add(new FightPropertyValue { Id = 4, Value = 100 - id });
                if (id == 1)
                {
                    unit.Skills.Add(combatCatalog.CreateSkill(13520, 0, 0, 1, 1, 1, null));
                    unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
                }
                checkpoint.Units.Add(unit);
            }
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            var execution = assets.BeginBattle(checkpoint).Step(0, 1);
            var settledCast = execution.Event.SkillCasts.Single(value => value.SkillEntityId == 13520);
            var triggerReceipts = settledCast.HitSelections.Where(value => value.HitEntityId == 13522).ToArray();
            Require(triggerReceipts.Length == 8
                && triggerReceipts.All(value => value.EffectCenters.Count == 1)
                && triggerReceipts.Any(value => value.Targets.Count == 0),
                "Original 13520 did not retain its eight trigger occurrences and empty draws.");
            var saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Time.timeScale = 0f;
            before = played.Count;
            var actualSkillId = views.Start(settledCast, source, target);
            views.Tick(.79f);
            Require(root.GetComponentsInChildren<MyFightHitTriggerSkill>().Length == 0,
                "Receipt trigger hit started before its configured delay.");
            views.Tick(.011f);
            Require(root.GetComponentsInChildren<MyFightHitTriggerSkill>().Length == 1,
                "Repeated trigger class IDs lost their independent creation times.");
            var actualSkill = views.GetEntity(actualSkillId).GetComponent<MyFightSkill>();
            Require(played.Skip(before).Any(value => value.Name == "playable13522"
                && value.Source == actualSkill.GetPlayableTargetGo()),
                "Receipt trigger trajectory lost the original skill playable target anchor.");
            checkpoint.Units[1].X = 13;
            Time.timeScale = 1f;
            yield return new WaitForSeconds(.2f);
            var triggerViews = root.GetComponentsInChildren<MyFightHitTriggerSkill>();
            Require(triggerViews.Length == 8 && triggerViews.Select(value => value.myEntity.Id).Distinct().Count() == 8
                && triggerViews.Select(value => value.Component.GridTransform.Pos).Distinct().Count() == 8,
                "Repeated trigger receipts did not create eight independent native identities.");
            foreach (var value in triggerViews)
                Require(value.Component.SkillId.Equals(actualSkillId)
                    && settledCast.HitSelections.Any(receipt => receipt.EffectCenters.Any(center =>
                        value.Component.GridTransform.Pos.Equals(new Pos2d(center.X, center.Y)))),
                    "Trigger hit lost its actual owning skill or frozen random cell.");
            Capture("hit-trigger-production-receipts.png");
            yield return new WaitForSeconds(1.1f);
            Require(root.GetComponentsInChildren<MyFightHitTriggerSkill>().Length == 0,
                "Trigger hit did not remove after original DestroyDelay.");
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var legacy = restored.Receipts[0].Event.SkillCasts.Single(value => value.SkillEntityId == 13520);
            foreach (var receipt in legacy.HitSelections) receipt.EffectDelays.Clear();
            var legacySaved = FightBattleCheckpointSerializer.Save(restored);
            views.Start(legacy, source, target);
            yield return new WaitForSeconds(.95f);
            Require(root.GetComponentsInChildren<MyFightHitTriggerSkill>().Length == 8,
                "Legacy trigger receipts failed to resolve flight from their original view binding.");
            yield return new WaitForSeconds(1.1f);
            Require(root.GetComponentsInChildren<MyFightHitTriggerSkill>().Length == 0,
                "Receipt playback leaked pooled trigger entities.");
            Require(FightBattleCheckpointSerializer.Save(restored) == legacySaved,
                "Receipt playback repeated settlement or changed its checkpoint.");

            units[0].X = 5; units[1].X = 8;
            source.localPosition = new Vector3(5, 5, 0);
            target.localPosition = new Vector3(8, 5, 0);
            foreach (int summonSkill in new[] { 11540, 12140, 12200, 57780 })
            {
                var summonBattle = SummonBattle(summonSkill, out var summonCast);
                var receipt = summonCast.HitSelections.Single(value =>
                    combatCatalog.CreateSkill(summonSkill, 0, 0, 5, 20, 1, null).HitGraphEntities
                        .Any(definition => definition.EntityId == value.HitEntityId && definition.IsSummon));
                if (summonSkill == 12140) receipt.EffectDelays.Clear();
                var beforePlayback = FightBattleCheckpointSerializer.Save(summonBattle);
                Time.timeScale = 0f;
                before = played.Count;
                var summonSkillId = views.Start(summonCast, source, target);
                views.Tick(receipt.Delay + .001f);
                var nativeSummon = root.GetComponentsInChildren<MyFightHitSummon>().Single();
                Require(nativeSummon.Component.SkillId.Equals(summonSkillId),
                    "Summon hit lost its actual owning cast identity.");
                var originalCells = nativeSummon.Component.GetScopes().Select(cell =>
                    nativeSummon.Component.GridTransform.LocalToWorldPos(cell.Pos)).ToArray();
                var spawned = summonBattle.Units.Where(unit => unit.MasterId == 1).OrderBy(unit => unit.Id)
                    .Select(unit => new Pos2d(unit.X, unit.Y)).ToArray();
                if (summonSkill == 11540)
                    Require(nativeSummon.myPlayable && originalCells.Length == 9 && summonBattle.GridItems.Count > 0
                        && summonBattle.GridItems.All(item => originalCells.Contains(new Pos2d(item.X, item.Y))),
                        "Original nine-cell summon scope lost its settled grid-item cells.");
                else
                    Require(spawned.SequenceEqual(originalCells) && !nativeSummon.myPlayable
                        && !nativeSummon.myScopePlayable && !nativeSummon.SourceToMePlayable,
                        "Summon position/order or original empty hit graph changed: " + summonSkill);
                Time.timeScale = 1f;
                yield return new WaitForSeconds(summonSkill == 11540 ? .6f : .03f);
                if (summonSkill == 11540)
                    Require(played.Skip(before).Any(value => value.Name == "playable11541"
                        && value.Source == nativeSummon.gameObject && value.Target == nativeSummon.gameObject),
                        "Production summon receipt did not play its own original graph.");
                Capture("hit-summon-production-" + summonSkill + ".png");
                yield return new WaitForSeconds(summonSkill == 11540 ? 1.2f : .8f);
                Require(root.GetComponentsInChildren<MyFightHitSummon>().Length == 0
                    && FightBattleCheckpointSerializer.Save(summonBattle) == beforePlayback,
                    "Summon receipt playback leaked its view or repeated summon creation.");
            }
            yield return ValidateRandomReceipts(root, source, target);
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

        private IEnumerator CheckStunStrike(Transform root, FightBattleGrid grid)
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load(); var formula = assets.LoadDamageFormula(); var items = assets.LoadSkillItems();
            foreach (var direction in new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1) })
            {
                FightBattleUnitState LiveUnit(int id, int side, int x, int y)
                {
                    var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
                        Level = 50, SubRank = 5, X = x, Y = y, Direction = 3, Hp = 1000000, MaxHp = 1000000,
                        LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
                    unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
                    foreach (var prop in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
                        unit.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
                    foreach (string name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                        "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
                    if (id == 1) unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("EffectRateValue"), Value = 10000 });
                    return unit;
                }
                var units = new[] { LiveUnit(1, 1, 5, 5), LiveUnit(2, 2, 5 + direction.x, 5 + direction.y) };
                var skill = catalog.CreateSkill(10620, 0, 10010, 2, 20, 1,
                    catalog.CalculateSkillProps(10620, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
                skill.LimitedTimes = 1; units[0].Skills.Add(skill); units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                    Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
                checkpoint.Units.AddRange(units); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
                FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
                string committed = FightBattleCheckpointSerializer.Save(checkpoint);
                var display = FightBattleCheckpointSerializer.Load(committed).Units.ToArray();
                var holder = new GameObject("Live stun strike").transform; holder.SetParent(root, false);
                var source = new GameObject("Stun source").transform; source.SetParent(holder, false); source.localPosition = new Vector3(5, 5, 0);
                var target = new GameObject("Stun target").transform; target.SetParent(holder, false); target.localPosition = new Vector3(5 + direction.x, 5 + direction.y, 0);
                foreach (var body in new[] { source, target })
                {
                    body.gameObject.AddComponent<MyTransform>();
                    var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                    body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                }
                var nativeScope = new OfflineBattleNativePlayableScope(holder, null);
                using (var nativeViews = new OfficialBattleSkillEntityPlayback(holder, new OfficialBattleGridMapper(holder, grid),
                    nativeScope, id => display.FirstOrDefault(value => value.Id == id)))
                using (var session = new OfficialBattleLiveImpactSession(checkpoint, copy => FightBattleExecutor.Restore(copy,
                    formula, new FightDamageSettlement(), catalog, items), nativeViews, 0, 1))
                {
                    nativeViews.BindDamageUI(holder, 0);
                    ParticleSystem impact = null; bool paused = false, resumed = false, captured = false; int frames = 0, damageFrame = -1;
                    session.SkillGraphPlayableStarted += (cast, id, prefab, leaf) => {
                        if (cast.SkillEntityId != 10620 || prefab.name != "playable10620" || leaf.name != "effect") return;
                        impact = leaf.GetComponentsInChildren<ParticleSystem>(true).Single(value => value.name == "glow (5)");
                        impact.Pause(); paused = true;
                    };
                    bool Start(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                    { id = nativeViews.Start(cast, source, target); return true; }
                    void Damage(FightBattleDamageEvent damage)
                    {
                        Require(impact && impact.particleCount > 0 && resumed, "Stun strike settled before its original impact particle.");
                        damageFrame = Time.frameCount;
                    }
                    float deadline = Time.realtimeSinceStartup + 20f;
                    while (!session.IsCompleted && Time.realtimeSinceStartup < deadline)
                    {
                        session.Pump(Time.deltaTime, Start, Damage);
                        if (paused && !resumed)
                        {
                            Require(impact.particleCount == 0 && damageFrame < 0 && FightBattleCheckpointSerializer.Save(checkpoint) == committed,
                                "A paused original impact cannot settle damage or mutate the committed checkpoint.");
                            if (++frames == 5) { resumed = true; impact.Play(); }
                        }
                        if (!captured && damageFrame >= 0)
                        {
                            Capture("stun-strike-impact-" + direction.x + "-" + direction.y + ".png", false);
                            captured = true;
                        }
                        nativeViews.Tick(Time.deltaTime); nativeScope.LateTick(); yield return null;
                    }
                    if (!captured && damageFrame >= 0)
                    {
                        Capture("stun-strike-impact-" + direction.x + "-" + direction.y + ".png", false);
                        captured = true;
                    }
                    Require(paused && resumed && captured && damageFrame >= 0,
                        "Stun impact did not reach damage: paused=" + paused + " resumed=" + resumed + " captured=" + captured
                        + " frame=" + damageFrame + " particles=" + (impact ? impact.particleCount : -1)
                        + " active=" + (impact && impact.gameObject.activeInHierarchy) + " playing=" + (impact && impact.isPlaying)
                        + " completed=" + session.IsCompleted);
                    Require(session.TryTakeCompleted(out _, out var result)
                        && result.Checkpoint.Units.Single(value => value.Id == 2).Statuses.Any(value => value.ClassId == 24010)
                        && FightBattleCheckpointSerializer.Save(checkpoint) == committed,
                        "Stun damage and status must follow the visible original impact.");
                }
                Destroy(holder.gameObject); yield return null;
            }
        }

        private IEnumerator CheckBouncingShieldImpact(Transform root, FightBattleGrid grid, bool multipleTargets)
        {
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load(); var formula = assets.LoadDamageFormula(); var items = assets.LoadSkillItems();
            FightBattleUnitState LiveUnit(int id, int side, int x)
            {
                var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 2,
                    Level = 50, SubRank = 5, X = x, Y = 5, Direction = 3, Hp = 1000000, MaxHp = 1000000,
                    LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
                unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
                foreach (var prop in new Dictionary<int, long> { { 1, 1000 }, { 2, 10 }, { 3, unit.MaxHp }, { 4, unit.Turn.Speed } })
                    unit.Properties.Add(new FightPropertyValue { Id = prop.Key, Value = prop.Value });
                foreach (string name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
                    "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
                    unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
                return unit;
            }
            var units = multipleTargets
                ? new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 7), LiveUnit(3, 2, 9) }
                : new[] { LiveUnit(1, 1, 5), LiveUnit(2, 2, 7) };
            var skill = catalog.CreateSkill(11090, 0, 10048, 2, 20, 1,
                catalog.CalculateSkillProps(11090, 2, 20, 1).Select(x => new FightPropertyValue { Id = x.Key, Value = x.Value }).ToList());
            skill.LimitedTimes = 1; units[0].Skills.Add(skill); units[0].SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 17,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            checkpoint.Units.AddRange(units); var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(units.Skip(1).Select(value => value.Id)); checkpoint.Waves.Add(wave);
            FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            string committed = FightBattleCheckpointSerializer.Save(checkpoint);
            var display = FightBattleCheckpointSerializer.Load(committed).Units.ToArray();
            var holder = new GameObject("Live bouncing shield").transform; holder.SetParent(root, false);
            var bodies = new Dictionary<int, Transform>();
            foreach (var unit in units)
            {
                var body = new GameObject("Shield unit " + unit.Id).transform; body.SetParent(holder, false);
                body.localPosition = new Vector3(unit.X, unit.Y, 0); body.gameObject.AddComponent<MyTransform>();
                var renderer = body.gameObject.AddComponent<MeshRenderer>(); renderer.enabled = false; renderer.sharedMaterials = Array.Empty<Material>();
                body.gameObject.AddComponent<Zjcs.Official.BattleMaterialPlayables.MeshMaterialProperty>().MeshRenderer = renderer;
                bodies.Add(unit.Id, body);
            }
            var attachAgent = bodies[1].gameObject.AddComponent<Zjcs.Official.StoryAttachment.AttachAgent>();
            foreach (string bone in new[] { "body", "shenti_1", "sWeapon_01" })
            {
                var attachment = new GameObject(bone).transform; attachment.SetParent(bodies[1], false);
                attachAgent.AddBoneNode(bone, attachment);
            }
            var nativeScope = new OfflineBattleNativePlayableScope(holder, null);
            using (var nativeViews = new OfficialBattleSkillEntityPlayback(holder, new OfficialBattleGridMapper(holder, grid),
                nativeScope, id => display.FirstOrDefault(value => value.Id == id)))
            using (var session = new OfficialBattleLiveImpactSession(checkpoint, copy => FightBattleExecutor.Restore(copy,
                formula, new FightDamageSettlement(), catalog, items), nativeViews, 0, 1))
            {
                nativeViews.BindDamageUI(holder, 0);
                var speedSetter = typeof(MyPlayable).GetProperty("Speed").GetSetMethod(true);
                MyPlayableTrack track = null; int pausedFrames = 0, pausedHits = 0; bool paused = false, resumed = false, captured = false;
                var targets = new List<int>(); var tracks = new List<string>();
                nativeViews.HitGraphPlayableStarted += (cast, hit, center, prefab, leaf) => {
                    if (cast.SkillEntityId != 11090 || prefab.name != "playable11090_hit_bone" || leaf.name != "track") return;
                    track = (MyPlayableTrack)leaf; paused = true; resumed = false; pausedFrames = 0; pausedHits = targets.Count;
                    tracks.Add(hit.HitEntityId + ":triggers=" + track.myTrackTriggerList.Count + ":duration=" + track.GetDuration()
                        + ":target=" + (track.myTarget ? track.myTarget.name : "null"));
                    speedSetter.Invoke(track, new object[] { 0f });
                };
                bool Start(FightBattleSkillCastEvent cast, out EcEntityIdentifier id)
                { id = nativeViews.Start(cast, bodies[cast.ActorId], bodies[cast.Presentation.TargetId]); return true; }
                void Damage(FightBattleDamageEvent damage)
                {
                    Require(track && resumed && track.myApplyComponent && track.myTarget
                        && Vector3.Distance(track.myApplyComponent.transform.position, track.myTarget.transform.position) <= .01f,
                        "Shield damage did not coincide with the original projectile endpoint.");
                    targets.Add(damage.TargetId);
                }
                float deadline = Time.realtimeSinceStartup + 30f;
                while (!session.IsCompleted && Time.realtimeSinceStartup < deadline)
                {
                    session.Pump(Time.deltaTime, Start, Damage);
                    if (paused && !resumed)
                    {
                        Require(track && targets.Count == pausedHits && FightBattleCheckpointSerializer.Save(checkpoint) == committed,
                            "A paused shield flight cannot settle damage or mutate the committed checkpoint.");
                        if (++pausedFrames == 5) { resumed = true; speedSetter.Invoke(track, new object[] { 1f }); }
                    }
                    if (!captured && targets.Count > 0) { Capture("bouncing-shield-impact-" + multipleTargets + ".png", false); captured = true; }
                    nativeViews.Tick(Time.deltaTime); nativeScope.LateTick(); yield return null;
                }
                if (!captured && targets.Count > 0) { Capture("bouncing-shield-impact-" + multipleTargets + ".png", false); captured = true; }
                int[] expectedTargets = multipleTargets ? new[] { 2, 3, 2, 3 } : new[] { 2 };
                Require(captured && tracks.Count == expectedTargets.Length && targets.SequenceEqual(expectedTargets)
                    && session.TryTakeCompleted(out _, out _)
                    && FightBattleCheckpointSerializer.Save(checkpoint) == committed,
                    "Shield impacts must follow their original projectile arrivals: targets=" + string.Join(",", targets)
                    + " tracks=" + string.Join(",", tracks) + " completed=" + session.IsCompleted
                    + " paused=" + paused + " resumed=" + resumed + " time=" + (track ? track.Time : -1)
                    + " status=" + (track ? track.myStatus.ToString() : "missing") + " speed=" + (track ? track.Speed : -1));
            }
            Destroy(holder.gameObject); yield return null;
        }

        private IEnumerator ValidateRandomReceipts(Transform root, Transform source, Transform target)
        {
            // The previous summon skill owns a longer graph than its hit entity.
            // Let that original graph end before recording the next skill's pixels.
            yield return new WaitForSeconds(3f);
            foreach (int skillId in new[] { 10920, 68480 })
            {
                var battle = SummonBattle(skillId, out var cast);
                var definition = combatCatalog.CreateSkill(skillId, 0, 0, 5, 20, 1, null)
                    .HitGraphEntities.Single(hit => hit.HitComponentType == "FightHitRandomTargetComponent");
                var receipt = cast.HitSelections.Single(hit => hit.HitEntityId == definition.EntityId);
                Require(receipt.EffectCenters.Count == 1 && receipt.Targets.Count == 0,
                    "Random parent view must retain its anchor without drawing another target.");
                foreach (bool legacy in new[] { false, true })
                {
                    var replay = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(battle));
                    var replayCast = replay.Receipts[0].Event.SkillCasts.Single(hit => hit.SkillEntityId == skillId);
                    if (legacy) foreach (var hit in replayCast.HitSelections) hit.EffectDelays.Clear();
                    string saved = FightBattleCheckpointSerializer.Save(replay);
                    int before = played.Count;
                    Time.timeScale = 0f;
                    var skill = views.Start(replayCast, source, target);
                    views.Tick(receipt.Delay + .001f);
                    var native = root.GetComponentsInChildren<MyFightHitRandomTarget>().Single();
                    var center = receipt.EffectCenters.Single();
                    Require(native.Component.SkillId.Equals(skill)
                        && native.Component.GridTransform.Pos.Equals(new Pos2d(center.X, center.Y))
                        && native.myPlayable && native.Component.GetScopes().Count == definition.ScopeEntries.Count,
                        "Random parent lost its original graph, scope, identity or settled anchor.");
                    Time.timeScale = 1f;
                    yield return new WaitForSeconds(.025f);
                    Require(played.Skip(before).Any(value => value.Name == native.myPlayable.name
                        && value.Source == native.gameObject && value.Target == native.gameObject),
                        "Random parent receipt did not play its original graph.");
                    Capture("hit-random-production-" + skillId + (legacy ? "-legacy" : "") + ".png");
                    yield return new WaitForSeconds(.375f);
                    Capture("hit-random-production-" + skillId + (legacy ? "-legacy" : "") + "-sustain.png");
                    yield return new WaitForSeconds(3f);
                    Require(root.GetComponentsInChildren<MyFightHitRandomTarget>().Length == 0
                        && FightBattleCheckpointSerializer.Save(replay) == saved,
                        "Random parent playback leaked its entity or repeated combat settlement.");
                }
            }
        }

        private FightBattleCheckpoint SummonBattle(int skillId, out FightBattleSkillCastEvent cast, Action<FightBattleCheckpoint> arrange = null)
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 0 } };
            for (int id = 1; id <= 2; id++)
            {
                var unit = Unit(id, 5 + 3 * (id - 1), 5);
                unit.Side = id; unit.Level = 150; unit.SubRank = 1;
                unit.Hp = unit.MaxHp = 1000000; unit.MoveMask = 5;
                unit.Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 - id };
                unit.Properties.Add(new FightPropertyValue { Id = 1, Value = 10001 });
                unit.Properties.Add(new FightPropertyValue { Id = 2, Value = 10003 });
                unit.Properties.Add(new FightPropertyValue { Id = 3, Value = 1000000 });
                unit.Properties.Add(new FightPropertyValue { Id = 4, Value = 100 - id });
                if (id == 1)
                {
                    unit.Skills.Add(combatCatalog.CreateSkill(skillId, 0, 0, 5, 20, 1, null));
                    unit.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
                }
                checkpoint.Units.Add(unit);
            }
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            arrange?.Invoke(checkpoint);
            var result = OfficialCombatAssetCatalog.LoadDefault().BeginBattle(checkpoint).Step(0, 1);
            cast = result.Event.SkillCasts.Single(value => value.SkillEntityId == skillId);
            return checkpoint;
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
        private void Capture(string name, bool expectEffect = true)
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
                Require(!expectEffect || hasEffect, "Native hit capture contains only its background: " + name);
            }
            finally { cameraView.targetTexture = previousTarget; RenderTexture.active = previous; Destroy(render); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
