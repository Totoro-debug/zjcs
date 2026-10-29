using Zjcs.Official.EntityLifecycle;
namespace Zjcs.Official.EntityRootComponents
{
public interface IRootClientComponentData:IEntityComponentInfoSource { object Original {get;} }
public abstract class MapComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class WorldComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class MapCharacterSpawnerComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class GridGroundComponentData : EntityGridGroundData, IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class AreaAgentComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class EntityVisibilityAgentComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class AreaExploreSpawnerComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
public abstract class MapStoryEntitySpawnerComponentData : IRootClientComponentData { public abstract object Original {get;} public abstract object ComponentInfo {get;} }
}