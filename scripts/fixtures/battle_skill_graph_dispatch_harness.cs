using System;
using System.Collections.Generic;
using Zjcs.Presentation.Combat;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using UnityEngine;
using System.Linq;

// Records the native playback seam; compiles the actual catalog and dispatcher below it.
namespace UnityEngine
{
    public class GameObject
    {
        public string name; public int PartnerAdapters, EmbeddedAdapters, MecanimAdapters;
        public readonly Zjcs.Official.StoryGroundEffects.ApplyByGroundType GroundApply = new Zjcs.Official.StoryGroundEffects.ApplyByGroundType();
        public GameObject(string value) { name = value; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new object[] { GroundApply }.OfType<T>().ToArray();
    }
    public class Transform { public GameObject gameObject; public Transform(string name) { gameObject = new GameObject(name); } }
    public class TextAsset { public string text = "catalog"; }
    public static class Resources
    {
        public static readonly Dictionary<string, object> Assets = new Dictionary<string, object>();
        public static T Load<T>(string path) where T : class { return Assets.TryGetValue(path, out var value) ? value as T : null; }
    }
    public static class JsonUtility { public static object Data; public static T FromJson<T>(string text) { return (T)Data; } }
    public static class Mathf { public static float Max(float a, float b) { return Math.Max(a, b); } public static float Min(float a, float b) => Math.Min(a, b); public static float Min(params float[] values) => values.Min(); public static float Sqrt(float value) => (float)Math.Sqrt(value); }
}
namespace Zjcs.Combat.Official
{
    public class FightBattleUnitState { public int Id, Direction, CharacterType; public long Hp; }
    public class GridItem { public int Id; public bool HasRuntime; public FightBattleUnitState Runtime; }
    public class FightBattleCheckpoint { public List<FightBattleUnitState> Units = new List<FightBattleUnitState>(); public List<GridItem> GridItems = new List<GridItem>(); }
    public class FightBattleSkillCastPresentation { public int FacingIndex, TargetId; }
    public class FightBattleSkillCastEvent { public int ActorId, SkillEntityId, Slot; public bool IsPetSupport; public float Delay; public FightBattleSkillCastPresentation Presentation; public List<int> TargetIds = new List<int>(); }
    public class FightGridPoint { public int X,Y; }
    public class FightBattleMovementEvent { public int UnitId,MoveBehaviourId,MoveSkillEntityId; public FightBattleSkillCastPresentation OrdinarySkillPresentation; public float OrdinaryMoveDelay; public bool HasDirection,HasFacing; public float Delay,MoveDuration,MoveSpeed,NativeDuration; public List<FightGridPoint> Path = new List<FightGridPoint>(); }
    public sealed class FightBattleSkillMove { public float MoveDuration, MoveSpeed, Delay; }
    public sealed class FightBattleSkillDefinition
    {
        public int EntityId, SkillType;
        public bool HasReplay, HasLogicComponent;
        public float Duration, AllEndedDuration, AllRelevantEndedDuration,
            ActionDuration, AllEndedActionDuration, AllRelevantEndedActionDuration;
        public List<FightBattleSkillMove> SourceMoves = new List<FightBattleSkillMove>();
        public List<int> HitGraphEntities = new List<int>(), Hits = new List<int>(), ExtraSkills = new List<int>();
    }
    public class FightBattleStepEvent { public List<FightBattleSkillCastEvent> SkillCasts = new List<FightBattleSkillCastEvent>(); public List<FightBattleMovementEvent> Movements = new List<FightBattleMovementEvent>(); }
}
namespace Zjcs.Official.StoryPlayableManager
{
    public interface IBattleCameraOffsetHost {}
    public interface IBattleCameraPerformanceHost {}
    public interface IBattleCharacterPartVisibilityHost {}
    public interface IBattleMasterEntityResolver {}
}
namespace Zjcs.Official.StoryPlayables
{
    public interface IBattleSourceDeathView {}
}
namespace Zjcs.Official.StoryGroundEffects
{
    public interface IPlayableGroundEffectHost {}
    public sealed class GroundHost : IPlayableGroundEffectHost {}
    public sealed class ApplyByGroundType { public IPlayableGroundEffectHost Host; }
}
namespace Zjcs.Presentation.Combat
{
    // This fixture leaves the separately tested grid lifecycle seam unbound.
    public class OfficialBattleGridMapper {}
    public class OfficialBattleGridEntityPlayback : IDisposable
    {
        public OfficialBattleGridEntityPlayback(Transform root, OfficialBattleGridMapper mapper,
            OfflineBattleNativePlayableScope scope, OfficialBattleSkillEntityPlayback views,
            FightBattleCheckpoint checkpoint, Func<int, Transform> resolve, Func<int, bool> icons) {}
        public float NextDelay => float.PositiveInfinity;
        public Transform TryGetTransform(int id) => null;
        public void RefreshSnapshot(FightBattleCheckpoint checkpoint) {}
        public void Restore() {}
        public void Enqueue(FightBattleStepEvent step, FightBattleCheckpoint checkpoint) {}
        public void DispatchDue() {}
        public void AdvanceTimers(float delta) {}
        public void Dispose() {}
    }
    // Exercise dispatch through its real entity-lifecycle seam. Native entity
    // playback is verified in Unity with the actual original MyFightSkill.
    public class OfficialBattleSkillEntityPlayback : IDisposable
    {
        private readonly OfflineBattleNativePlayableScope scope;
        public Zjcs.Official.StoryGroundEffects.IPlayableGroundEffectHost GroundEffects { get; } = new Zjcs.Official.StoryGroundEffects.GroundHost();
        public OfficialBattleSkillEntityPlayback(OfflineBattleNativePlayableScope scope) { this.scope = scope; scope.GroundEffectsHost = GroundEffects; }
        public void Start(FightBattleSkillCastEvent cast, Transform source, Transform target)
            { scope.EntitiesStarted.Add(cast.SkillEntityId); scope.CastSlots.Add(cast.Slot); scope.CastTimes.Add(scope.Clock);
              // Native entity playback owns directional prefab selection now.
              // This seam supplies a prefab; the actual entity is checked in Unity.
              int facing = cast.Presentation?.FacingIndex ?? 2;
              string field = facing == 2 ? "UpPlayablePrefab" : facing == 0 ? "DownPlayablePrefab"
                  : facing == 1 ? "LeftPlayablePrefab" : "RightPlayablePrefab";
              var prefab = OfficialBattleSkillGraphCatalog.TryLoadDefault().Find(cast.SkillEntityId, field);
              if (prefab != null) scope.Play(prefab, source.gameObject, target?.gameObject ?? source.gameObject, 1,
                scope.PlayableSlot.gameObject, new Dictionary<string, object>()); }
        public void Tick(float delta) => scope.Tick(delta);
        public bool OwnsMovement(FightBattleMovementEvent movement) => movement.HasDirection && movement.HasFacing && movement.MoveBehaviourId == 777;
        public float MovementDuration(FightBattleMovementEvent movement) => movement.NativeDuration;
        public void StartMovement(FightBattleMovementEvent movement, Transform source)
        { scope.Moves.Add(movement.UnitId); scope.MoveTimes.Add(scope.Clock); }
        public void Dispose() => scope.Dispose();
    }
}
namespace Zjcs.Offline.BattleNativePlayables
{
    public class OfflineBattleNativePlayableScope : IDisposable
    {
        public readonly Transform PlayableSlot = new Transform("slot");
        public readonly List<int> EntitiesStarted = new List<int>();
        public readonly List<int> CastSlots = new List<int>();
        public readonly List<float> CastTimes = new List<float>();
        public readonly List<int> Moves = new List<int>();
        public readonly List<float> MoveTimes = new List<float>();
        public Zjcs.Official.StoryGroundEffects.IPlayableGroundEffectHost GroundEffectsHost;
        private Action<GameObject, GameObject> nativeAdapter;
        public void BindNativePrefabAdapter(Action<GameObject, GameObject> adapter) { nativeAdapter = adapter; }
        public readonly List<string> Plays = new List<string>();
        public readonly List<string> Sources = new List<string>();
        public readonly List<float> Times = new List<float>();
        public float Clock;
        public int LateTicks;
        public bool Disposed;
        public void BindSourceDeathView(Zjcs.Official.StoryPlayables.IBattleSourceDeathView view) {}
        public void BindHosts(Zjcs.Official.StoryPlayableManager.IBattleCameraOffsetHost host,
            Zjcs.Official.StoryPlayableManager.IBattleCharacterPartVisibilityHost visibilityHost = null,
            Zjcs.Official.StoryPlayableManager.IBattleCameraPerformanceHost cameraPerformanceHost = null,
            Zjcs.Official.StoryPlayableManager.IBattleMasterEntityResolver masterResolver = null) {}
        public void Tick(float delta) { Clock += delta; }
        public void LateTick() { LateTicks++; }
        public void Play(GameObject prefab, GameObject source, GameObject target, float speed,
            GameObject parent, Dictionary<string, object> parameters)
        { var instance = new GameObject(prefab.name); nativeAdapter?.Invoke(prefab, instance);
          if (!ReferenceEquals(instance.GroundApply.Host, GroundEffectsHost)) throw new Exception("Native graph lost its ground-effect host");
          Plays.Add(prefab.name + ":" + target.name); Sources.Add(source.name); Times.Add(Clock); }
        public void Dispose() { Disposed = true; }
    }
}
namespace Zjcs.Offline.ProfessionUI
{
    public sealed class EmbeddedMonsterMecanimResources : IDisposable
    {
        public void BindOriginalPrefab(GameObject source, GameObject instance)
        { if (source == null) throw new Exception("Original asset identity was lost"); instance.MecanimAdapters++; }
        public void Update() {}
        public void Dispose() {}
    }
    public static class PartnerNativeSkillSkeleton { public static void BindOriginalPrefab(GameObject value) { value.PartnerAdapters++; } }
    public static class EmbeddedMonsterNativeSkeleton { public static void BindOriginalPrefab(GameObject value) { value.EmbeddedAdapters++; } }
}
public static class BattleSkillGraphDispatchHarness
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static OfficialBattleSkillGraphBinding Binding(string field, string resource)
        { return new OfficialBattleSkillGraphBinding { field = field, resource = resource }; }
    static FightBattleSkillCastEvent Cast(int skill, float delay, int facing = 2)
        { return new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = skill, Delay = delay, Presentation = new FightBattleSkillCastPresentation { FacingIndex = facing, TargetId = 2 }, TargetIds = new List<int> { 2 } }; }
    public static void Run()
    {
        Resources.Assets.Clear();
        Resources.Assets["Combat/skill-native-graphs"] = new TextAsset();
        Resources.Assets["fallback"] = new GameObject("fallback");
        var bindings = new List<OfficialBattleSkillGraphBinding> { Binding("myPlayablePrefab", "fallback") };
        string[] fields = { "UpPlayablePrefab", "DownPlayablePrefab", "LeftPlayablePrefab", "RightPlayablePrefab" };
        foreach (string field in fields) { Resources.Assets[field] = new GameObject(field); bindings.Add(Binding(field, field)); }
        JsonUtility.Data = new OfficialBattleSkillGraphCatalogData { manifest = "884_156692", skills = new[] {
            new OfficialBattleSkillGraphRecord { skill_entity_id = 10, playables = bindings.ToArray() },
            new OfficialBattleSkillGraphRecord { skill_entity_id = 20, playables = new[] { Binding("myPlayablePrefab", "fallback") } },
            new OfficialBattleSkillGraphRecord { skill_entity_id = 15300, playables = new[] { Binding("myPlayablePrefab", "fallback") } },
            new OfficialBattleSkillGraphRecord { skill_entity_id = 30, playables = Array.Empty<OfficialBattleSkillGraphBinding>() }
        } };
        var catalog = OfficialBattleSkillGraphCatalog.TryLoadDefault();
        var checkpoint = new FightBattleCheckpoint();
        var actor = new FightBattleUnitState { Id = 1, Hp = 0 };
        checkpoint.Units.Add(actor);
        var transforms = new Dictionary<int, Transform> { { 1, new Transform("actor") }, { 2, new Transform("target") } };
        Func<int, Transform> resolve = id => transforms.TryGetValue(id, out var value) ? value : null;
        int[] facingIndices = { 2, 0, 1, 3 }; // Pos2d.FourDirs -> Common.Direction.
        for (int direction = 0; direction < 4; direction++)
        {
            actor.Direction = facingIndices[direction];
            var scope = new OfflineBattleNativePlayableScope();
            using (var playback = new OfficialBattleSkillGraphPlayback(catalog, scope, new OfficialBattleSkillEntityPlayback(scope)))
            {
                playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { Cast(10, 0, actor.Direction) } }, checkpoint, resolve);
                Require(scope.Plays.Count == 1 && scope.Plays[0] == fields[direction] + ":target", "Official direction or settled-dead cast was lost");
            }
        }
        Resources.Assets.Remove(fields[0]);
        bool missingDirection = false;
        try { catalog.Find(10, fields[0]); }
        catch (InvalidOperationException exception)
        {
            missingDirection = exception.Message.Contains("skill=10")
                && exception.Message.Contains("field=" + fields[0]) && exception.Message.Contains("resource=" + fields[0]);
        }
        Require(missingDirection, "A declared directional asset must not silently select the default");
        Require(catalog.Find(20, fields[0]).name == "fallback", "An originally absent direction must use the original default");
        Resources.Assets[fields[0]] = new GameObject(fields[0]);
        Resources.Assets.Remove("fallback");
        bool missingDefault = false;
        try { catalog.Find(20, fields[0]); }
        catch (InvalidOperationException exception)
        {
            missingDefault = exception.Message.Contains("skill=20")
                && exception.Message.Contains("field=myPlayablePrefab") && exception.Message.Contains("resource=fallback");
        }
        Require(missingDefault, "A declared default asset must not silently drop the cast");
        Resources.Assets["fallback"] = new GameObject("fallback");
        // Settlement may already have turned the actor again before presentation starts.
        actor.Direction = 3;
        var captured = Cast(10, .2f);
        captured.Presentation = new FightBattleSkillCastPresentation { FacingIndex = 1, TargetId = 2 };
        var capturedScope = new OfflineBattleNativePlayableScope();
        using (var playback = new OfficialBattleSkillGraphPlayback(catalog, capturedScope, new OfficialBattleSkillEntityPlayback(capturedScope)))
        {
            playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { captured } }, checkpoint, resolve);
            playback.Tick(.2f);
            Require(capturedScope.Plays[0] == "LeftPlayablePrefab:target", "Settled actor direction replaced the skill creation snapshot");
        }
        var petScope = new OfflineBattleNativePlayableScope();
        using (var playback = new OfficialBattleSkillGraphPlayback(catalog, petScope, new OfficialBattleSkillEntityPlayback(petScope)))
        {
            var immediate = Cast(20, 0); immediate.IsPetSupport = true;
            var delayed = Cast(20, .25f); delayed.IsPetSupport = true;
            var pet = new Transform("pet");
            playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> {
                immediate, Cast(20, 0), delayed } }, checkpoint, resolve, id => id == 1 ? pet : null);
            Require(string.Join(",", petScope.Sources) == "pet,actor", "Support and ordinary casts did not retain separate sources");
            playback.Tick(.25f);
            Require(string.Join(",", petScope.Sources) == "pet,actor,pet" && petScope.Plays[2] == "fallback:target",
                "Delayed support lost its pet source or unit target");
            playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { immediate } }, checkpoint, resolve);
            Require(petScope.Sources.Count == 3, "Missing pet source incorrectly cast through the owner");
        }
        var empty = new OfflineBattleNativePlayableScope();
        using (var playback = new OfficialBattleSkillGraphPlayback(catalog, empty, new OfficialBattleSkillEntityPlayback(empty)))
        {
            playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> {
                Cast(30, .5f), Cast(999, 0) } }, checkpoint, resolve);
            playback.Tick(.25f);
            Require(empty.EntitiesStarted.Count == 0, "Empty graph cast started before its delay");
            playback.Tick(.25f);
            Require(empty.EntitiesStarted.Count == 1 && empty.EntitiesStarted[0] == 30 && empty.Plays.Count == 0,
                "Known empty graph must create a native entity; unknown ID must remain excluded");
            playback.Tick(.5f);
            Require(empty.EntitiesStarted.Count == 1, "Empty graph cast was duplicated");
        }
        actor.Direction = 2;
        var timed = new OfflineBattleNativePlayableScope();
        var queue = new OfficialBattleSkillGraphPlayback(catalog, timed, new OfficialBattleSkillEntityPlayback(timed));
        int cloneBindings = 0;
        queue.BindPlayerClonePrefabs(instance => {
            Require(instance.PartnerAdapters == 1, "Clone binding replaced or preceded the original partner adapter");
            Require(instance.MecanimAdapters == 1, "Clone binding preceded source-aware Mecanim binding");
            Require(instance.EmbeddedAdapters == 1, "Clone binding replaced or preceded the original embedded monster adapter");
            cloneBindings++;
        });
        try { queue.BindPlayerClonePrefabs(_ => {}); throw new Exception("Clone binding was replaced"); }
        catch (InvalidOperationException) {}
        queue.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> {
            Cast(10, .75f), Cast(20, .25f), Cast(10, .25f), Cast(999, 0) } }, checkpoint, resolve);
        Require(timed.Plays.Count == 0, "Delayed casts played immediately");
        actor.Direction = 3; // Queued prefab choice must retain dispatch-time direction.
        queue.Tick(.125f);
        Require(timed.Plays.Count == 0, "Cast played before its delay");
        queue.Tick(.875f); // One long frame crosses all three cues.
        Require(string.Join(",", timed.Plays) == "fallback:target,UpPlayablePrefab:target,UpPlayablePrefab:target", "Cue order or same-time stability changed");
        Require(timed.Times[0] == .25f && timed.Times[1] == .25f && timed.Times[2] == .75f && timed.Clock == 1f, "Native graph clock did not split at cue times");
        Require(cloneBindings == 3, "Original instantiated skill graphs did not all receive composed clone binding");
        queue.LateTick();
        Require(timed.LateTicks == 1 && timed.Clock == 1f, "PostProcessFrame phase advanced the clock or was not dispatched");
        queue.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { Cast(20, 1) } }, checkpoint, resolve);
        queue.Dispose();
        queue.Dispose();
        Require(timed.Disposed && timed.Plays.Count == 3, "Dispose played a pending cue");
        try { queue.Tick(1); throw new Exception("Disposed queue accepted a tick"); } catch (ObjectDisposedException) {}
        try { queue.LateTick(); throw new Exception("Disposed queue accepted a late tick"); } catch (ObjectDisposedException) {}
        var moveScope = new OfflineBattleNativePlayableScope();
        using (var playback = new OfficialBattleSkillGraphPlayback(catalog, moveScope, new OfficialBattleSkillEntityPlayback(moveScope)))
        {
            Func<int,float,bool,float,FightBattleMovementEvent> move = (unit, delay, native, duration) => new FightBattleMovementEvent {
                UnitId=unit, Delay=delay, HasDirection=native, HasFacing=native, MoveBehaviourId=777,
                MoveDuration=duration, NativeDuration=.75f,
                Path=new List<FightGridPoint> {new FightGridPoint(),new FightGridPoint{X=1}} };
            var step = new FightBattleStepEvent { Movements = new List<FightBattleMovementEvent> {
                move(1,0,false,.25f), move(1,0,true,.01f), move(1,0,true,.01f), move(2,.125f,true,.01f) } };
            playback.Play(step,checkpoint,resolve);
            Require(moveScope.Moves.Count==0,"Native movement bypassed its delay or preceding fallback movement.");
            playback.Tick(1.5f);
            Require(moveScope.Moves.SequenceEqual(new[]{2,1,1})
                && moveScope.MoveTimes.SequenceEqual(new[]{.125f,.25f,1f}) && moveScope.Clock==1.5f,
                "Native movement lost independent unit clocks, original duration, fallback ordering or long-frame splitting.");
            playback.Play(new FightBattleStepEvent {Movements=new List<FightBattleMovementEvent>{move(1,1,true,0)}},checkpoint,resolve);
        }
        Require(moveScope.Moves.Count==3,"Dispose started a pending movement.");
        var ordinaryScope = new OfflineBattleNativePlayableScope();
        using (var playback = new OfficialBattleSkillGraphPlayback(catalog, ordinaryScope, new OfficialBattleSkillEntityPlayback(ordinaryScope)))
        {
            Func<FightBattleMovementEvent> ordinary = () => new FightBattleMovementEvent {
                UnitId = 1, Delay = .15f, OrdinaryMoveDelay = .15f, HasDirection = true, HasFacing = true,
                MoveBehaviourId = 777, MoveSkillEntityId = 15300, NativeDuration = .75f,
                OrdinarySkillPresentation = new FightBattleSkillCastPresentation(),
                Path = new List<FightGridPoint> { new FightGridPoint(), new FightGridPoint { X = 1 } } };
            var first = ordinary();
            Require(((IBattleMovementSkillView)playback).OwnsMovementSkill(first), "Ordinary parent skill ownership was lost");
            var legacy = ordinary(); legacy.OrdinarySkillPresentation = null;
            Require(!playback.OwnsMovementSkill(legacy), "Legacy movement was assigned a missing parent snapshot");
            playback.Play(new FightBattleStepEvent { Movements = new List<FightBattleMovementEvent> { first, ordinary() },
                SkillCasts = new List<FightBattleSkillCastEvent> { Cast(20, 0) } }, checkpoint, resolve);
            Require(ordinaryScope.EntitiesStarted.SequenceEqual(new[] { 15300, 20 }) && ordinaryScope.Moves.Count == 0,
                "Ordinary entity must start before both its delayed action and a simultaneous active cast");
            playback.Tick(1.5f);
            Require(ordinaryScope.EntitiesStarted.SequenceEqual(new[] { 15300, 20, 15300 })
                && ordinaryScope.CastSlots.SequenceEqual(new[] { -1, 0, -1 })
                && Math.Abs(ordinaryScope.CastTimes[2] - .9f) < .0001f
                && ordinaryScope.MoveTimes.Count == 2 && Math.Abs(ordinaryScope.MoveTimes[0] - .15f) < .0001f
                && Math.Abs(ordinaryScope.MoveTimes[1] - 1.05f) < .0001f,
                "Ordinary parent/action clocks lost their original delay, non-equipped slot or repeated path ordering");
        }
        Console.WriteLine("Skill graph dispatch passed: official fields/fallback, delayed ordering, equal-time stability, native/fallback movement ordering, ordinary parent creation and negative slots, independent unit clocks, long-frame splitting, settled-dead cast and disposal.");
    }
}
