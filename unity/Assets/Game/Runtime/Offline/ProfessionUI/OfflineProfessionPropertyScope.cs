using System;
using System.Collections.Generic;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Formatting arguments are evaluated by the original static helper. Keep the newest live window's owner.
    public sealed class OfflineProfessionPropertyScope:IDisposable
    {
        private static readonly List<OfflineProfessionPropertyScope> owners=new List<OfflineProfessionPropertyScope>();
        private static IProfessionPropertyBindings baseline;
        private IProfessionPropertyBindings bindings;
        public OfflineProfessionPropertyScope(IProfessionPropertyBindings bindings)
        {
            this.bindings=bindings??throw new ArgumentNullException(nameof(bindings));
            if(owners.Count==0)baseline=ProfessionPropertyBindings.Current;
            owners.Add(this);ProfessionPropertyBindings.Current=bindings;
        }
        public void Dispose()
        {
            if(bindings==null)return;
            var currentOwner=owners[owners.Count-1].bindings;
            owners.Remove(this);bindings=null;
            if(ReferenceEquals(ProfessionPropertyBindings.Current,currentOwner))
                ProfessionPropertyBindings.Current=owners.Count==0?baseline:owners[owners.Count-1].bindings;
            if(owners.Count==0)baseline=null;
        }
    }
}
