using System;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Consumed original MiniMap surface. The concrete original root derives from
    // this component and supplies its actual handler and full-screen action.
    public abstract class MiniMapSizingContext:MiniMapRegionContext
    {
        public abstract MiniMapKeepSizeHandler SizingHandler{get;}
        public abstract GAction HideFullScreenAction{get;}
    }
    internal struct MiniMapSizingDisposableBag:IDisposable
    {
        private List<IDisposable> leases;
        public void Add(IDisposable lease){if(leases==null)leases=new List<IDisposable>();leases.Add(lease);}
        public void Dispose(){if(leases==null)return;foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
    internal static class MiniMapSizingDisposable
    {
        public static T AddTo<T>(T lease,ref MiniMapSizingDisposableBag bag) where T:IDisposable {bag.Add(lease);return lease;}
    }
}
