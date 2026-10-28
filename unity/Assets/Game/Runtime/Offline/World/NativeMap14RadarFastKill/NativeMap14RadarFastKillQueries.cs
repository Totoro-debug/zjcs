using System;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadarFastKill;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14RadarFastKill
{
    public sealed class NativeMap14RadarFastKillQueries:IRadarFastKillQueries
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly object entityHandler;
        public NativeMap14RadarFastKillQueries(NativeMap14SharedAssemblies shared,object entityHandler)
        {
            this.shared=shared??throw new ArgumentNullException(nameof(shared));
            this.entityHandler=entityHandler??throw new ArgumentNullException(nameof(entityHandler));
            if(!shared.CommonType("IEntityHandler").IsInstanceOfType(entityHandler))throw new ArgumentException("Original entity handler is required.");
        }
        public bool TryGetCampOwnerEntity(ECViewEntityData entity,out RadarFastKillCampOwner owner)
        {
            var native=entity as NativeMap14SharedViewEntity??throw new ArgumentException("Original shared entity is required.");
            var args=new object[]{native.Original,null};
            var result=(bool)entityHandler.GetType().GetMethod("TryGetCampOwnerEntity").Invoke(entityHandler,args);
            owner=args[1]==null?null:new RadarFastKillCampOwner{Original=args[1],ClassId=(int)NativeMap14SharedPrefabs.Get(args[1],"ClassId")};
            return result;
        }
        public bool IsFastGame(int mapClassId,int region,int mapStageFastType,int playerLevel,int rank,long combatRating)
        {
            var value=shared.Shared.GetType("MapUtils",true).GetMethod("GetGameFastType").Invoke(null,new object[]{mapClassId,region,Enum.ToObject(shared.CommonType("MapStageFastForwardType"),mapStageFastType),playerLevel,Enum.ToObject(shared.CommonType("SubRank"),rank),combatRating});
            return (bool)shared.CommonType("EnumExt").GetMethod("IsFastGame").Invoke(null,new[]{value});
        }
        public static void BindStageFast(NativeMap14SharedAssemblies shared,NativeMap14SharedViewEntity entity)
        {
            if(entity.GetComponent<IRadarStageFast>()!=null)return;
            var component=entity.Original.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(entity.Original,new object[]{shared.CommonType("IStageBehStageFast")});
            if(component!=null)entity.BindAdditionalComponent<IRadarStageFast>(new StageFast(component));
        }
        private sealed class StageFast:IRadarStageFast
        {
            private readonly object original;
            public StageFast(object original){this.original=original;}
            public int GetMapFastType()=>Convert.ToInt32(original.GetType().GetMethod("GetMapFastType").Invoke(original,null));
        }
    }
}
