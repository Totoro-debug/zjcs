using System;
using System.Collections.Generic;
using Zjcs.Official.EntityViews;
namespace Zjcs.Official.EntityRadarCost
{
    public interface IRadarStageCostQueries
    { Dictionary<int,long> GetStageCost(ECViewEntityData entity); }
    public interface IRadarItemIconQueries { string GetItemIcon(int itemId); }
    // Same consumed R3 bag semantics as WorldInputResourceBag, recovered from APK.
    internal struct RadarCostResourceBag : IDisposable
    {
        private List<IDisposable> values;
        private int count;
        private bool disposed;
        public T Add<T>(T value) where T:IDisposable
        {
            if(disposed)value.Dispose();
            else { if(values==null)values=new List<IDisposable>();values.Add(value);count++; }
            return value;
        }
        public void Dispose()
        {
            if(values!=null) { for(int i=0;i<count;i++)values[i]?.Dispose();values=null;count=0; }
            disposed=true;
        }
    }
}
