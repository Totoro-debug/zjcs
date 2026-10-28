// Migrated original shared visibility source; see Map13 source receipt.
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.NativeTerrain.Common;
using System;
using System.Collections.Generic;

// Explicit consumed entity/map interfaces for the original shared component.
// These declarations provide no movement, body, camp, ownership or map rules.

namespace Zjcs.Official.World.Map13GridVisibility
{
public abstract class VisibilitySerializable { public abstract ushort GetOpCode(); }
public interface IVisibilitySerializationCallbacks { void OnBeforeSerialize(); void OnAfterDeserialize(); }

public abstract class VisibilityComponentSource<T> : VisibilitySerializable
{
    public GridVisibilitySourceContext Host;
    public VisibilityEntity Entity => Host.Entity;
    public EcContainer Container => Host.Container;
    protected TValue GetComponent<TValue>() => Entity.GetComponent<TValue>();
    public abstract void Start();
    public abstract void End(VisibilitySerializable userData);
}

public abstract class GridVisibilitySourceContext
{
    public abstract VisibilityEntity Entity { get; }
    public abstract EcContainer Container { get; }
    public abstract long? MainPlayerId { get; }
    public abstract IMapCharacter MainMapCharacter { get; }
    public abstract List<Pos2d> CalcAllVisiblePos(GridGroundComponent ground, Pos2d position, int range);
    public abstract bool TryGetCampOwnerEntity(VisibilityEntity entity, out VisibilityEntity owner);
    public abstract HashSet<Pos2d> GetLoEntityIncludePosList(EcContainer container, List<int> entities);
    public abstract int GetSpawnCharacterViewRange(int mapClassId);
}

public abstract class VisibilityEntity
{
    public abstract int ClassId { get; }
    public abstract object Id { get; }
    public abstract EcContainer Container { get; }
    public abstract GridTransformComponent GridTransform { get; }
    public abstract T GetComponent<T>();
}

public abstract class EcContainer
{
    public abstract object Id { get; }
    public abstract int ClassId { get; }
    public abstract VisibilityEntity RootEntity { get; }
    public EcContainerStartEntityEventDelegate StartEntityEvent = new EcContainerStartEntityEventDelegate();
}

public sealed class MapComponent
{
    public event Action<IMapCharacter> OnCharacterAdd;
    public event Action<IMapCharacter> OnCharacterRemove;
    public readonly Dictionary<object, IMapCharacter> MapCharacterDict = new Dictionary<object, IMapCharacter>();
    public void NotifyCharacterAdded(IMapCharacter character) => OnCharacterAdd?.Invoke(character);
    public void NotifyCharacterRemoved(IMapCharacter character) => OnCharacterRemove?.Invoke(character);
}

public interface IMapCharacter
{
    VisibilityEntity Entity { get; }
    GridTransformComponent GridTransform { get; }
    VisibilityCharacterData Character { get; }
    int ViewRange { get; }
}
public sealed class VisibilityCharacterData { public long PlayerId; }

public abstract class GridTransformComponent
{
    public GridTransformPosUpdatedEventDelegate PosUpdatedEvent = new GridTransformPosUpdatedEventDelegate();
    public Pos2d Pos;
    public GridGroundComponent GridGround;
    public List<Pos2d> RangeList;
    public HashSet<Pos2d> AddOneRangeList;
    public VisibilityBodyInfo Info;
    public abstract T GetComponent<T>();
}
public sealed class VisibilityBodyInfo { public int UnlockWholeRequestCount; }

public abstract class GridGroundComponent
{
    public abstract DefaultAStarMovableHandler CheckStandHandler { get; }
    public abstract GamePosData GetPosData(Pos2d position);
    public abstract bool InGround(Pos2d position);
}
public sealed class DefaultAStarMovableHandler { public Func<Pos2d, bool> CanStand; }
public abstract class GamePosData
{
    public abstract bool ExistEntitys();
    public abstract IEnumerable<VisibilityEntity> Entitys { get; }
}
public abstract class AreaAgentComponent { public abstract int GridToRegion(int mapClassId, Pos2d position); }
public sealed class LoEntityCampComponent { public LoEntityCampComponentInfo Info; }
public sealed class LoEntityCampComponentInfo { public List<int> CampMonsterEntities; }

}
