using System;
using System.Collections.Generic;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneUiControls;

// Observe the scheduling boundary; RealWait executes the production UniTask.
public sealed class TipPorts : IHomeHangUpTipServices
{
    readonly IHomeHangUpTipServices source;
    Pending current;
    public TipPorts(IHomeHangUpTipServices source) { this.source=source; }
    public bool RealWait;
    public int Scheduled, Canceled, Fired;
    public float LastSeconds => current?.Seconds??0;
    public IHomeHangUpTipPlayer MainPlayer=>source.MainPlayer;
    public IHomeHangUpTipPlayer PlayerData=>source.PlayerData;
    public bool InMyHome=>source.InMyHome;
    public IReadOnlyDictionary<EFurniture,List<int>> RecordHangUpIdDict=>source.RecordHangUpIdDict;
    public HomeHangUpTipSettings Settings=>source.Settings;
    public long GetLevelUpExp(int level)=>source.GetLevelUpExp(level);
    public void SetTipState(EFurniture furniture,int level)=>source.SetTipState(furniture,level);
    public void Publish(HangUpTipLevelChangeData data)=>source.Publish(data);
    public IDisposable Wait(float seconds,Action callback)
    {
        Scheduled++;var next=new Pending(this,seconds);current=next;
        if(RealWait)next.Inner=source.Wait(seconds,()=>{Fired++;callback();});
        return next;
    }
    sealed class Pending:IDisposable
    {
        TipPorts owner;
        public readonly float Seconds;
        public IDisposable Inner;
        public Pending(TipPorts owner,float seconds){this.owner=owner;Seconds=seconds;}
        public void Dispose()
        {
            var value=owner;owner=null;if(value==null)return;
            value.Canceled++;if(ReferenceEquals(value.current,this))value.current=null;
            Inner?.Dispose();Inner=null;
        }
    }
}
