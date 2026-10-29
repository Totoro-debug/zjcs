using System;
using Zjcs.Offline.World.NativeMap14ClientComponents;
using Zjcs.Offline.World.NativeMap14RootComponents;
using Zjcs.Offline.World.NativeMap14DisplayData;
using DisplayData=Zjcs.Offline.World.NativeMap14DisplayData.NativeMap14DisplayData;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Offline.World.NativeMap14Visibility;
using Zjcs.Offline.World.NativeMap14VisibilityHandlers;

namespace Zjcs.Offline.World.NativeMap14ClientViewBindings
{
    // Supply this method to NativeMap14ClientEntityState before any consumer
    // binds. All original components keep the one stable shared view identity.
    public sealed class NativeMap14ClientViewBindings
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly DisplayData data;
        private readonly NativeMap14ClientComponentBindings components;
        private readonly NativeMap14RootComponentBindings rootComponents;
        public NativeMap14ClientViewBindings(NativeMap14SharedAssemblies shared,DisplayData data)
        {
            this.shared=shared??throw new ArgumentNullException(nameof(shared));
            this.data=data??throw new ArgumentNullException(nameof(data));
            components=new NativeMap14ClientComponentBindings(shared);
            rootComponents=new NativeMap14RootComponentBindings(shared);
        }
        public void Bind(NativeMap14SharedViewEntity view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            components.Bind(view);
            rootComponents.Bind(view);
            NativeMap14VisibilityData.Bind(shared,view);
            NativeMap14ActionComponentData.Bind(shared,view);
            NativeMap14VisibilityAwardItemData.Bind(shared,view);
            NativeMap14DisplayEntityState.Bind(view,data);
        }
    }
}
