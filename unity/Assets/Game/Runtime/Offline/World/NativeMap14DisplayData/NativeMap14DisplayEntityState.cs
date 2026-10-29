using System;
using Zjcs.Official.MapDisplays;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Pos2d=Zjcs.Official.World.NativeTerrain.Common.Pos2d;

namespace Zjcs.Offline.World.NativeMap14DisplayData
{
    public sealed class NativeMap14DisplayEntityState:IMapDisplayEntityState
    {
        private readonly NativeMap14SharedViewEntity view;
        private readonly NativeMap14DisplayData data;
        public NativeMap14DisplayEntityState(NativeMap14SharedViewEntity view,NativeMap14DisplayData data)
        {this.view=view??throw new ArgumentNullException(nameof(view));this.data=data??throw new ArgumentNullException(nameof(data));}
        public bool Removing=>(bool)NativeMap14SharedPrefabs.Get(view.Original,"Removing");
        public Pos2d GridPosition=>view.Grid.Pos;
        public string EntityTag=>data.GetEntityTag(view.Original);
    }
}
