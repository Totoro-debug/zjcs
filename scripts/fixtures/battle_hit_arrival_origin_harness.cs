using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Presentation.Combat;

// Exercises the actual receipt scheduler without starting Unity or modifying a save.
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
    }
}
namespace Zjcs.Official.EntityViews { }
namespace Zjcs.Official.EntityLifecycle
{
    public sealed class MyEntity
    {
        public T GetComponent<T>() => (T)(object)new HitBinding();
    }
}
namespace Zjcs.Official.EntityTransformViews
{
    public class ECGridTransformInfo { }
    public interface IGridTransformPosUpdatedEventReceiver { }
}
namespace Zjcs.Official.World.NativeTerrain.Common
{
    public struct Pos2d { public int X, Y; public Pos2d(int x, int y) { X = x; Y = y; } }
    public enum Direction { Up, Down, Left, Right }
}
namespace Zjcs.Official.StoryPlayables
{
    public struct EcEntityIdentifier : IEquatable<EcEntityIdentifier>
    {
        public long ContainerId; public int EntityId;
        public EcEntityIdentifier(long containerId, int entityId) { ContainerId = containerId; EntityId = entityId; }
        public bool Equals(EcEntityIdentifier other) => ContainerId == other.ContainerId && EntityId == other.EntityId;
        public override bool Equals(object value) => value is EcEntityIdentifier other && Equals(other);
        public override int GetHashCode() => ContainerId.GetHashCode() * 23 + EntityId;
    }
}
namespace Zjcs.Official.BattleSkillPlayables
{
    public abstract class FightHitGridTransformData
    {
        public abstract Pos2d Pos { get; }
        public abstract Direction Direction { get; }
        public abstract bool FlipX { get; }
        public abstract ECGridTransformInfo Info { get; }
        public abstract UnityEngine.Vector3 GridToPosition(Pos2d position);
        public abstract Pos2d PositionToGrid(UnityEngine.Vector3 position);
        public abstract void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver);
        public abstract void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver);
        public abstract Pos2d LocalToWorldPos(Pos2d position);
    }
    public interface IFightHitViewBinding { object DelayInfoProvider { get; } }
    public sealed class HitBinding : IFightHitViewBinding { public object DelayInfoProvider => null; }
    public static class FightHitViewFormula
    {
        public static float CalcSkillHitDelay(object info, Pos2d source, Pos2d target,
            OfficialBattleSkillEntityPlayback views, bool flip) => 0f;
    }
}
namespace Zjcs.Combat.Official
{
    public struct FightGridPoint { public int X, Y; public FightGridPoint(int x, int y) { X = x; Y = y; } }
    public sealed class FightBattleHitScopeTransform { public float RealCenterX, RealCenterY; }
    public static class FightGridTransformMath
    {
        public static FightGridPoint CalcLocalToWorldPos(FightGridPoint local, FightGridPoint origin,
            float x, float y, int facing, bool mirrored) => new FightGridPoint(local.X + origin.X, local.Y + origin.Y);
    }
    public sealed class FightBattleHitScopeEntry { public float Delay; }
    public sealed class FightBattleHitEntity
    {
        public int EntityId; public string HitComponentType = "FightHitFixedComponent";
        public float DestroyDelay; public FightBattleHitScopeTransform ScopeTransform;
        public List<FightBattleHitScopeEntry> ScopeEntries = new List<FightBattleHitScopeEntry>();
    }
    public sealed class FightBattleSkillDefinition { public List<FightBattleHitEntity> HitGraphEntities; }
    public sealed class FightOfficialCombatCatalog
    {
        public FightBattleSkillDefinition CreateSkill(int id, int slot, int item, int rank, int level, int subRank, object props)
            => new FightBattleSkillDefinition { HitGraphEntities = new List<FightBattleHitEntity> {
                new FightBattleHitEntity { EntityId = 10101, DestroyDelay = 1f } } };
    }
    public sealed class FightBattleHitSelectionSnapshot
    {
        public int HitEntityId, SourceX, SourceY, FacingIndex; public bool FlipX, HasChainedOrigin;
        public float Delay; public List<FightGridPoint> EffectCenters = new List<FightGridPoint>();
        public List<float> EffectDelays = new List<float>();
    }
    public sealed class FightBattleSkillCastEvent
    {
        public int SkillEntityId; public List<FightBattleHitSelectionSnapshot> HitSelections = new List<FightBattleHitSelectionSnapshot>();
    }
}
namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleHitEntityData
    {
        public readonly EcEntityIdentifier Id; public readonly HitData Hit;
        public OfficialBattleHitEntityData(EcEntityIdentifier id, EcEntityIdentifier skill,
            FightBattleHitEntity definition, FightHitGridTransformData grid, Pos2d source)
        { Id = id; Hit = new HitData { GridTransform = grid }; }
        public sealed class HitData { public FightHitGridTransformData GridTransform; }
    }
    public sealed class OfficialBattleSkillEntityPlayback
    {
        public readonly List<EcEntityIdentifier> Created = new List<EcEntityIdentifier>();
        public readonly List<EcEntityIdentifier> Removed = new List<EcEntityIdentifier>();
        public Action<EcEntityIdentifier> OnCreate;
        public UnityEngine.Vector3 GridToPos(Pos2d position) => new UnityEngine.Vector3 { x = position.X, y = position.Y };
        public MyEntity CreateHit(OfficialBattleHitEntityData data) { Created.Add(data.Id); OnCreate?.Invoke(data.Id); return new MyEntity(); }
        public void RemoveHit(EcEntityIdentifier id) => Removed.Add(id);
    }

    public static class BattleHitArrivalOriginHarness
    {
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("Hit arrival origin: " + message); }

        private static FightBattleHitSelectionSnapshot Selection(float delay, params FightGridPoint[] centers)
        {
            var result = new FightBattleHitSelectionSnapshot { HitEntityId = 10101, Delay = delay };
            foreach (var center in centers) { result.EffectCenters.Add(center); result.EffectDelays.Add(delay); }
            return result;
        }

        public static string Run()
        {
            var views = new OfficialBattleSkillEntityPlayback();
            var playback = new OfficialBattleHitReceiptPlayback(views, new FightOfficialCombatCatalog());
            var first = new FightBattleSkillCastEvent { SkillEntityId = 10100 };
            var firstSelection = Selection(0f, new FightGridPoint(1, 2), new FightGridPoint(3, 4));
            var secondSelection = Selection(0f, new FightGridPoint(1, 2));
            first.HitSelections.Add(firstSelection); first.HitSelections.Add(secondSelection);
            var second = new FightBattleSkillCastEvent { SkillEntityId = 10100 };
            var repeated = Selection(0f, new FightGridPoint(1, 2)); second.HitSelections.Add(repeated);
            var observed = new List<OfficialBattleHitReceiptPlayback.Origin>();
            views.OnCreate = id => {
                Check(playback.TryGetOrigin(id, out var origin), "identity must exist before the hit view starts");
                observed.Add(origin);
            };
            playback.Enqueue(first, new EcEntityIdentifier(2, 1));
            playback.Enqueue(second, new EcEntityIdentifier(2, 2));
            Check(observed.Count == 4 && views.Created.Count == 4, "each effect centre has one native entity");
            Check(ReferenceEquals(observed[0].Cast, first) && ReferenceEquals(observed[0].Selection, firstSelection)
                && observed[0].CenterIndex == 0 && ReferenceEquals(observed[1].Selection, firstSelection)
                && observed[1].CenterIndex == 1 && ReferenceEquals(observed[2].Selection, secondSelection)
                && ReferenceEquals(observed[3].Cast, second) && ReferenceEquals(observed[3].Selection, repeated),
                "repeated skills, selections or centres were confused");
            playback.Advance(2f); playback.DispatchDue();
            foreach (var id in views.Created)
                Check(!playback.TryGetOrigin(id, out _), "removed hit retained its mapping");
            Check(views.Removed.Count == 4, "completed native entities were not removed");
            var live = new FightBattleSkillCastEvent { SkillEntityId = 10100 };
            var selected = Selection(2f, new FightGridPoint(7, 8));
            int beforeLive = observed.Count;
            playback.EnqueueSelection(live, selected, new EcEntityIdentifier(2, 4), .75f);
            Check(observed.Count == beforeLive, "A future original hit was created early");
            playback.Advance(1.24f); playback.DispatchDue();
            Check(observed.Count == beforeLive, "The selection delay was not relative to the cast start");
            playback.Advance(.02f); playback.DispatchDue();
            Check(observed.Count == beforeLive + 1 && ReferenceEquals(observed[beforeLive].Cast, live)
                && ReferenceEquals(observed[beforeLive].Selection, selected),
                "The live selection did not retain its original identity at the due boundary");
            var late = Selection(1f, new FightGridPoint(8, 9));
            playback.EnqueueSelection(live, late, new EcEntityIdentifier(2, 4), 1.5f);
            Check(observed.Count == beforeLive + 2 && ReferenceEquals(observed[beforeLive + 1].Selection, late),
                "A selection offered after its configured boundary did not dispatch immediately");
            bool invalidElapsed = false;
            try { playback.EnqueueSelection(live, selected, new EcEntityIdentifier(2, 4), float.NaN); }
            catch (ArgumentOutOfRangeException) { invalidElapsed = true; }
            Check(invalidElapsed, "An invalid live cast clock was accepted");
            var pending = new FightBattleSkillCastEvent { SkillEntityId = 10100 };
            pending.HitSelections.Add(Selection(10f, new FightGridPoint(9, 9)));
            playback.Enqueue(pending, new EcEntityIdentifier(2, 3)); playback.Dispose();
            Check(!playback.TryGetOrigin(new EcEntityIdentifier(4, 7), out _), "disposing retained a pending hit mapping");
            return "Hit arrival origin passed: repeated casts, repeated selections, multiple centres, removal and disposal.";
        }
    }
}
