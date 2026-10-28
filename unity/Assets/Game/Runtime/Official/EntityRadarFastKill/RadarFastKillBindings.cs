using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.EntityRadarFastKill
{
    public interface IRadarFastKillRoot { int CurrentMapClassId{get;} MyEntity GetEntity(EcEntityIdentifier id); }
    public interface IRadarFastKillPosition { int QueryRegion(Pos2d position); }
    public sealed class RadarFastKillPlayer { public int Level,SubRank;public long CombatRating; }
    public sealed class RadarFastKillCampOwner { public object Original;public int ClassId; }
    public interface IRadarStageFast { int GetMapFastType(); }
    public interface IRadarFastKillQueries
    {
        bool TryGetCampOwnerEntity(ECViewEntityData entity,out RadarFastKillCampOwner owner);
        bool IsFastGame(int mapClassId,int region,int mapStageFastType,int playerLevel,int rank,long combatRating);
    }
    public interface IRadarFastKillServices
    {
        IRadarFastKillRoot ResolveRoot(MyEntity entity);
        IRadarFastKillPosition ResolvePosition(MyEntity entity);
        IRadarFastKillQueries Queries{get;}
        EcEntityIdentifier GetEntIdByLoEntId(int id);
        RadarFastKillPlayer MainPlayer{get;}
    }
}
