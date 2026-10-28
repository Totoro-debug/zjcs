using System;
using System.Collections.Generic;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityRadarCost;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
namespace Zjcs.Offline.World.NativeMap14RadarCost
{
    // Both consumed original client StageUtils.GetStageCost overloads. Shared
    // methods operate on original objects and return the original cost dictionary.
    public sealed class NativeMap14RadarStageCostQueries : IRadarStageCostQueries
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly object formula;
        private readonly Func<long,object> queryContainerStatus;
        public NativeMap14RadarStageCostQueries(NativeMap14SharedAssemblies shared,object formula,Func<long,object> queryContainerStatus)
        {
            this.shared=shared??throw new ArgumentNullException(nameof(shared));
            this.formula=formula??throw new ArgumentNullException(nameof(formula));
            this.queryContainerStatus=queryContainerStatus??throw new ArgumentNullException(nameof(queryContainerStatus));
            if(!shared.CommonType("IFormulaHandler").IsInstanceOfType(formula))throw new ArgumentException("Original formula handler is required.");
        }
        public Dictionary<int,long> GetStageCost(ECViewEntityData entity)
        {
            if(entity==null)return null;
            var native=entity as NativeMap14SharedViewEntity??throw new ArgumentException("Original shared entity is required.");
            var stage=native.Stage;
            if(stage==null)return null;
            var info=stage.Original.GetType().GetMethod("GetStateBehaviourComponent").Invoke(stage.Original,new object[]{0});
            var component=Get(info,"Component");
            if(component==null)return null;
            return GetStageCost(component,(int)Get(info,"CompIndex"));
        }
        public Dictionary<int,long> GetStageCost(object component,int compIndex)
        {
            if(!shared.CommonType("StageBehaviourComponent").IsInstanceOfType(component))return null;
            var dictionary=(Dictionary<int,long>)component.GetType().GetMethod("GetCost").Invoke(component,new object[]{compIndex});
            if(dictionary==null)return null;
            var entity=Get(component,"Entity");
            var source=entity.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(entity,new object[]{shared.CommonType("EntitySourceComponent")});
            if(source!=null)
                dictionary=(Dictionary<int,long>)formula.GetType().GetMethod("FilterCostByEntitySource")
                    .Invoke(formula,new[]{Get(source,"EntitySourceType"),(object)dictionary});
            if(dictionary.Count<=0)return dictionary;
            queryContainerStatus((long)Get(Get(entity,"Id"),"ContainerId"));
            return dictionary;
        }
        private static object Get(object owner,string name)=>NativeMap14SharedPrefabs.Get(owner,name);
    }
}
