using System;
using Zjcs.Official.TreasureCultivation;

namespace Zjcs.Offline.Progression
{
    // Scene/player owner supplies its next-frame dispatcher. Subscribing never replays existing inventory.
    public sealed class OfflineTreasureAutoSetup : IDisposable
    {
        private readonly OfflineTreasureService cultivation;
        private readonly OfflineTreasureSlotService slots;
        private readonly Action<Action> nextFrame;
        private readonly Action<Exception> reportError;
        private bool disposed;
        public event Action<int,int,ProgressionResult> OnCompleted;

        public OfflineTreasureAutoSetup(OfflineTreasureService cultivation,OfflineTreasureSlotService slots,Action<Action> nextFrame,Action<Exception> reportError=null)
        {
            this.cultivation=cultivation??throw new ArgumentNullException(nameof(cultivation));
            this.slots=slots??throw new ArgumentNullException(nameof(slots));
            this.nextFrame=nextFrame??throw new ArgumentNullException(nameof(nextFrame));
            this.reportError=reportError??(error=>UnityEngine.Debug.LogException(error));
            cultivation.OnTreasureActive+=Active;
        }
        private void Active(TreasureItem item)
        {
            // Original async state-machine errors are reported by Forget and cannot interrupt the activation callback.
            try { Prepare(item); } catch(Exception error) { reportError(error); }
        }
        private void Prepare(TreasureItem item)
        {
            if(disposed || item==null || (int)item.Info.ItemType!=305) return;
            int id=item.Info.ClassId;
            int index=slots.GetAutomaticSetupTargetIndex(id);
            if(index<0) return;
            string receipt="treasure:auto-slot:"+Guid.NewGuid().ToString("N");
            // The original captures the missing-key index before its NextFrame await.
            nextFrame(()=>{
                if(disposed) return;
                try {
                    var result=slots.SetupTreasureSlot(id,index,receipt);
                    OnCompleted?.Invoke(id,index,result);
                } catch(Exception error) {reportError(error);}
            });
        }
        public void Dispose()
        {
            if(disposed) return;
            disposed=true;cultivation.OnTreasureActive-=Active;
        }
    }
}
