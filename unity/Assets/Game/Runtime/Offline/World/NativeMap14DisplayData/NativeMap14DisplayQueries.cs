using System;
using UnityEngine;
using Zjcs.Official.MapDisplays;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;

namespace Zjcs.Offline.World.NativeMap14DisplayData
{
    // Platform inputs to the original display manager. Function-disable and
    // destruction state remain explicit host inputs, without invented defaults.
    public sealed class NativeMap14DisplayQueries:IMapDisplayQueries,IMapDisplayPosition,IMapDisplayRoot
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14SharedRoot root;
        private readonly NativeMap14DisplayData data;
        private readonly Func<bool> destroying,forbidLowMemory;
        public Transform MapRoot { get; }
        public EntityMapDisplayInfoParser Selector { get; }
        public NativeMap14DisplayQueries(NativeMap14SharedAssemblies shared,NativeMap14SharedRoot root,
            NativeMap14DisplayData data,Transform mapRoot,EntityMapDisplayInfoParser selector,
            Func<bool> destroying,Func<bool> forbidLowMemory)
        {
            this.shared=shared??throw new ArgumentNullException(nameof(shared));
            this.root=root??throw new ArgumentNullException(nameof(root));
            this.data=data??throw new ArgumentNullException(nameof(data));
            MapRoot=mapRoot?mapRoot:throw new ArgumentNullException(nameof(mapRoot));
            Selector=selector??throw new ArgumentNullException(nameof(selector));
            this.destroying=destroying??throw new ArgumentNullException(nameof(destroying));
            this.forbidLowMemory=forbidLowMemory??throw new ArgumentNullException(nameof(forbidLowMemory));
        }
        public int CurrentMapClassId=>(int)NativeMap14SharedPrefabs.Get(root.Container,"ClassId");
        public bool Destroying=>destroying();
        public bool IsMapForbidLowMemoryDeal()=>forbidLowMemory();
        public int GetMapGroupId(int mapClassId)=>(int)NativeMap14SharedPrefabs.Get(data.GetMapInfo(mapClassId),"MapGroupId");
        public string GetAreaTag(int mapClassId,int areaId)=>(string)NativeMap14SharedPrefabs.Get(data.GetAreaInfo(mapClassId,areaId),"AreaTag");
        public int QueryArea(Pos2d pos)=>(int)root.AreaAgent.GetType().GetMethod("GridToArea").Invoke(root.AreaAgent,new[]{Position(pos)});
        public GroundType GetGridGroundType(Pos2d pos)
        {
            var info=NativeMap14SharedPrefabs.Get(root.Ground,"Info");
            return (GroundType)Convert.ToInt32(info.GetType().GetMethod("GetGridGroundType").Invoke(info,new[]{Position(pos)}));
        }
        private object Position(Pos2d pos)=>Activator.CreateInstance(shared.CommonType("Pos2d"),pos.X,pos.Y);
    }
}
