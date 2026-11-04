using System;
using System.Collections.Generic;
using Zjcs.Presentation.Combat;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using UnityEngine;

// Records the native playback seam; compiles the actual catalog and dispatcher below it.
namespace UnityEngine
{
    public class GameObject { public string name; public GameObject(string value) { name = value; } }
    public class Transform { public GameObject gameObject; public Transform(string name) { gameObject = new GameObject(name); } }
    public class TextAsset { public string text = "catalog"; }
    public static class Resources
    {
        public static readonly Dictionary<string, object> Assets = new Dictionary<string, object>();
        public static T Load<T>(string path) where T : class { return Assets.TryGetValue(path, out var value) ? value as T : null; }
    }
    public static class JsonUtility { public static object Data; public static T FromJson<T>(string text) { return (T)Data; } }
    public static class Mathf { public static float Max(float a, float b) { return Math.Max(a, b); } }
}
namespace Zjcs.Combat.Official
{
    public class FightBattleUnitState { public int Id, Direction; public long Hp; }
    public class FightBattleCheckpoint { public List<FightBattleUnitState> Units = new List<FightBattleUnitState>(); }
    public class FightBattleSkillCastPresentation { public int FacingIndex, TargetId; }
    public class FightBattleSkillCastEvent { public int ActorId, SkillEntityId; public float Delay; public FightBattleSkillCastPresentation Presentation; public List<int> TargetIds = new List<int>(); }
    public class FightBattleStepEvent { public List<FightBattleSkillCastEvent> SkillCasts = new List<FightBattleSkillCastEvent>(); }
}
namespace Zjcs.Official.StoryPlayableManager { public interface IBattleCameraOffsetHost {} }
namespace Zjcs.Presentation.Combat
{
    // Exercise dispatch through its real entity-lifecycle seam. Native entity
    // playback is verified in Unity with the actual original MyFightSkill.
    public class OfficialBattleSkillEntityPlayback : IDisposable
    {
        private readonly OfflineBattleNativePlayableScope scope;
        public OfficialBattleSkillEntityPlayback(OfflineBattleNativePlayableScope scope) { this.scope = scope; }
        public void Start(FightBattleSkillCastEvent cast, GameObject prefab, Transform source, Transform target)
            => scope.Play(prefab, source.gameObject, target?.gameObject ?? source.gameObject, 1,
                scope.PlayableSlot.gameObject, new Dictionary<string, object>());
        public void Tick(float delta) => scope.Tick(delta);
        public void Dispose() => scope.Dispose();
    }
}
namespace Zjcs.Offline.BattleNativePlayables
{
    public class OfflineBattleNativePlayableScope : IDisposable
    {
        public readonly Transform PlayableSlot = new Transform("slot");
        public readonly List<string> Plays = new List<string>();
        public readonly List<float> Times = new List<float>();
        public float Clock;
        public int LateTicks;
        public bool Disposed;
        public void BindHosts(Zjcs.Official.StoryPlayableManager.IBattleCameraOffsetHost host) {}
        public void Tick(float delta) { Clock += delta; }
        public void LateTick() { LateTicks++; }
        public void Play(GameObject prefab, GameObject source, GameObject target, float speed,
            GameObject parent, Dictionary<string, object> parameters)
        { Plays.Add(prefab.name + ":" + target.name); Times.Add(Clock); }
        public void Dispose() { Disposed = true; }
    }
}
public static class BattleSkillGraphDispatchHarness
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static OfficialBattleSkillGraphBinding Binding(string field, string resource)
        { return new OfficialBattleSkillGraphBinding { field = field, resource = resource }; }
    static FightBattleSkillCastEvent Cast(int skill, float delay)
        { return new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = skill, Delay = delay, TargetIds = new List<int> { 2 } }; }
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
            new OfficialBattleSkillGraphRecord { skill_entity_id = 20, playables = new[] { Binding("myPlayablePrefab", "fallback") } }
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
                playback.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { Cast(10, 0) } }, checkpoint, resolve);
                Require(scope.Plays.Count == 1 && scope.Plays[0] == fields[direction] + ":target", "Official direction or settled-dead cast was lost");
            }
        }
        Resources.Assets.Remove(fields[0]);
        Require(catalog.Find(10, fields[0]).name == "fallback", "Missing direction did not fall back to the original default");
        Resources.Assets[fields[0]] = new GameObject(fields[0]);
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
        actor.Direction = 2;
        var timed = new OfflineBattleNativePlayableScope();
        var queue = new OfficialBattleSkillGraphPlayback(catalog, timed, new OfficialBattleSkillEntityPlayback(timed));
        queue.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> {
            Cast(10, .75f), Cast(20, .25f), Cast(10, .25f), Cast(999, 0) } }, checkpoint, resolve);
        Require(timed.Plays.Count == 0, "Delayed casts played immediately");
        actor.Direction = 3; // Queued prefab choice must retain dispatch-time direction.
        queue.Tick(.125f);
        Require(timed.Plays.Count == 0, "Cast played before its delay");
        queue.Tick(.875f); // One long frame crosses all three cues.
        Require(string.Join(",", timed.Plays) == "fallback:target,UpPlayablePrefab:target,UpPlayablePrefab:target", "Cue order or same-time stability changed");
        Require(timed.Times[0] == .25f && timed.Times[1] == .25f && timed.Times[2] == .75f && timed.Clock == 1f, "Native graph clock did not split at cue times");
        queue.LateTick();
        Require(timed.LateTicks == 1 && timed.Clock == 1f, "PostProcessFrame phase advanced the clock or was not dispatched");
        queue.Play(new FightBattleStepEvent { SkillCasts = new List<FightBattleSkillCastEvent> { Cast(20, 1) } }, checkpoint, resolve);
        queue.Dispose();
        queue.Dispose();
        Require(timed.Disposed && timed.Plays.Count == 3, "Dispose played a pending cue");
        try { queue.Tick(1); throw new Exception("Disposed queue accepted a tick"); } catch (ObjectDisposedException) {}
        try { queue.LateTick(); throw new Exception("Disposed queue accepted a late tick"); } catch (ObjectDisposedException) {}
        Console.WriteLine("Skill graph dispatch passed: official fields/fallback, delayed ordering, equal-time stability, long-frame clock splitting, settled-dead cast and disposal.");
    }
}
