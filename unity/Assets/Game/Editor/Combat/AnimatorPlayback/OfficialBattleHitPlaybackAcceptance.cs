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
