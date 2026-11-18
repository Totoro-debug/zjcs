#pragma warning disable 0067
using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace UnityEngine { public class GameObject { public string name; } }
namespace Zjcs.Official.StoryPlayables { public class MyPlayable { public string name; } public struct EcEntityIdentifier { public int Value; } }
namespace Zjcs.Combat.Official {
 public struct FightGridPoint {public int X,Y;public FightGridPoint(int x,int y){X=x;Y=y;}}
 public class FightBattleCheckpoint {public List<FightBattleUnitState> Units;}
 public class FightBattleUnitState {public int Id;public List<FightBattleSkillDefinition> Skills;}
 public class FightBattleSkillDefinition {public int EntityId;public List<FightBattleHitEntity> HitGraphEntities;}
 public class FightBattleHitEntity {public int EntityId;public List<FightBattleHitScopeEntry> ScopeEntries;}
 public class FightBattleHitScopeEntry {public float Delay;public FightGridPoint Position;}
 public class FightBattleSkillCastPresentation {public float Duration;}
 public class FightBattleSkillCastEvent {public int SkillEntityId,ActorId;public FightBattleSkillCastPresentation Presentation;public List<FightBattleHitSelectionSnapshot> HitSelections;}
 public class FightBattleHitSelectionSnapshot {public int HitEntityId;public List<TargetOccurrence> Targets;public class TargetOccurrence {public int EffectCenterIndex,ScopeIndex;public bool HasScopeIndex;}}
 public class FightBattleMovementEvent {public int UnitId;}
 public class FightBattleDamageEvent {public bool HasDamageTriggerStatus;}
 public class FightBattleExecutionResult {}
 public class FightBattleExecutor {}
 public class FightBattlePreparedCast {public FightBattleSkillCastEvent Cast;}
 public class FightBattlePreparedMovement {public FightBattleSkillCastEvent Cast;public FightBattleMovementEvent Movement;public bool HasBlockedPosition;public FightGridPoint BlockedPosition;}
 public class FightBattleSelectedHit {public FightBattleSkillCastEvent Cast;public FightBattleHitSelectionSnapshot Selection;}
 public class FightBattlePendingImpact {public FightBattleSkillCastEvent Cast;}
 public class FightBattleSettledImpact {public bool IsStatusResolution;public FightBattlePendingImpact Impact;public FightBattleDamageEvent Damage;}
 public class FightBattleCompletedCast {public FightBattleSkillCastEvent Cast;}
 public class FightBattleImpactLatch {
  public static FightBattleImpactLatch Last;public FightBattlePreparedMovement Pending;public FightBattlePreparedCast PendingCast;public FightBattleSelectedHit PendingSelection;public int Completions,Records,Waits;public bool Arrived,Disposed;public FightBattleMovementEvent LastContact;
  public FightBattleImpactLatch(){Last=this;}
  public bool TryTake(out FightBattlePendingImpact v){v=null;return false;}
  public bool TryTakePreparation(out FightBattlePreparedCast v){v=PendingCast;PendingCast=null;return v!=null;}
  public void CompletePreparation(FightBattlePreparedCast x,out long revision){revision=1;}
  public bool TryTakeMovement(out FightBattlePreparedMovement v){v=Pending;Pending=null;return v!=null;}
  public void CompleteMovement(FightBattlePreparedMovement v,out long revision){Completions++;revision=1;}
  public bool RecordBlockedContact(FightBattleMovementEvent v,out long revision){revision=0;if(Disposed)return false;LastContact=v;Records++;if(Arrived)return false;Arrived=true;revision=2;return true;}
  public void WaitForWorkerBoundary(long revision){Waits++;}
  public bool TryTakeSelection(out FightBattleSelectedHit v){v=PendingSelection;PendingSelection=null;return v!=null;}
  public bool TryTakeSettlement(out FightBattleSettledImpact v){v=null;return false;}
  public bool TryTakeCastCompletion(out FightBattleCompletedCast v){v=null;return false;}
  public void DeferStatusResolution(FightBattleSettledImpact x,out long revision){revision=1;}
  public void CompleteCast(FightBattleCompletedCast c,out long revision){revision=1;}
  public bool TryCompleteSettlement(FightBattleSettledImpact x,Func<FightBattlePendingImpact,FightBattleDamageEvent,bool> f,out long revision){revision=0;return false;}
 }
 public class FightBattleIsolatedStep:IDisposable {
  public readonly FightBattleImpactLatch Latch=new FightBattleImpactLatch();public bool IsCompleted;
  public FightBattleIsolatedStep(FightBattleCheckpoint c,Func<FightBattleCheckpoint,FightBattleExecutor> restore,int s,int n,bool observe){}
  public bool TryTakeCompleted(out FightBattleExecutor e,out FightBattleExecutionResult r){e=null;r=null;return false;}
  public void Dispose(){Latch.Disposed=true;}
 }
}
namespace Zjcs.Presentation.Combat {
 internal sealed class OfficialBattleMountainWaveImpact {internal static OfficialBattleMountainWaveImpact Last;private Action<int> arrival;internal static OfficialBattleMountainWaveImpact Attach(MyPlayable p,Action<int> a){Last=new OfficialBattleMountainWaveImpact{arrival=a};return Last;}internal void Trigger(int wave){arrival?.Invoke(wave);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleSlashImpact {internal static OfficialBattleSlashImpact Last;private Action arrival;internal static OfficialBattleSlashImpact Attach(MyPlayable p,Action a){Last=new OfficialBattleSlashImpact{arrival=a};return Last;}internal void Trigger(){arrival?.Invoke();}internal void Detach(){arrival=null;}}
 public class OfficialBattleSkillEntityPlayback {public event Action<FightBattleSkillCastEvent,EcEntityIdentifier,GameObject,MyPlayable> SkillGraphPlayableStarted;public void CompleteLiveCast(FightBattleSkillCastEvent c,EcEntityIdentifier id,float t){} public void Graph(FightBattleSkillCastEvent c,string prefab="playable11120"){SkillGraphPlayableStarted?.Invoke(c,new EcEntityIdentifier(),new GameObject{name=prefab},new MyPlayable{name="effect"});} }
 public class OfficialBattleImpactViewBridge:IDisposable {
  public static readonly List<int> Arrivals=new List<int>();
  public event Action<long> WorkerReleasedByArrival;
  public OfficialBattleImpactViewBridge(OfficialBattleSkillEntityPlayback v,FightBattleImpactLatch l){}
  public long QueueSelection(FightBattleSelectedHit h,EcEntityIdentifier id,float t)=>1;
  public bool TryQueueSettlement(FightBattleSettledImpact s,EcEntityIdentifier id,out long revision,Action<FightBattleDamageEvent> after,Func<FightBattleDamageEvent,bool> canStart){revision=0;return false;}
  internal bool TryQueueUnresolved(FightBattleSettledImpact s,EcEntityIdentifier id,Action<FightBattleDamageEvent> after,Func<FightBattleDamageEvent,bool> canStart)=>false;
  internal bool TryResolveStatus(FightBattleSettledImpact s,out long revision){revision=0;return false;}
  public void RecordArrival(FightBattleSkillCastEvent c,FightBattleHitSelectionSnapshot h,int a,int b){Arrivals.Add(b);}
  public void Dispose(){}
 }
 public static class BlockedLiveSessionHarness {
  private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  private static bool StartCast(FightBattleSkillCastEvent cast,out EcEntityIdentifier id){id=new EcEntityIdentifier();return true;}
  private static OfficialBattleLiveImpactSession NewSession(){return new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),new OfficialBattleSkillEntityPlayback(),0,1,true);}
  private static FightBattleMovementEvent Prepare(){var move=new FightBattleMovementEvent{UnitId=4};FightBattleImpactLatch.Last.Pending=new FightBattlePreparedMovement{Movement=move,HasBlockedPosition=true,BlockedPosition=new FightGridPoint(6,7)};return move;}
  public static void Run(){
   {
    var hit=new FightBattleHitEntity{EntityId=10601,ScopeEntries=new List<FightBattleHitScopeEntry>{new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1)}}};
    var skill=new FightBattleSkillDefinition{EntityId=10600,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10600,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     OfficialBattleSlashImpact.Last=null;views.Graph(cast,"wrong");Check(OfficialBattleSlashImpact.Last==null,"An unrelated graph armed the slash impact");
     views.Graph(cast,"playable10600");var observer=OfficialBattleSlashImpact.Last;Check(observer!=null,"Original slash effect was not observed");
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"A slash without a selected hit fabricated an arrival");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10601,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1&&OfficialBattleImpactViewBridge.Arrivals[0]==0,"An early slash was not routed to its selected target");
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The same visible slash was delivered twice");
     session.Dispose();observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"A disposed session accepted its old slash");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=11121,ScopeEntries=new List<FightBattleHitScopeEntry>()};
    for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)if(Math.Abs(x)+Math.Abs(y)>=1&&Math.Abs(x)+Math.Abs(y)<=2)
     hit.ScopeEntries.Add(new FightBattleHitScopeEntry{Position=new FightGridPoint(x,y)});
    int far=hit.ScopeEntries.FindIndex(value=>Math.Abs(value.Position.X)+Math.Abs(value.Position.Y)==2);
    var skill=new FightBattleSkillDefinition{EntityId=11120,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var actor=new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{actor}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=11120,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     views.Graph(cast);var observer=OfficialBattleMountainWaveImpact.Last;Check(observer!=null,"Original mountain effect was not observed");
     observer.Trigger(2);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"A wave without a selected hit fabricated an arrival");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=11121,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=far}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1&&OfficialBattleImpactViewBridge.Arrivals[0]==far,"An early visible wave was not retained for its later selected hit");
     observer.Trigger(1);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The inner wave affected an outer target");
     session.Dispose();observer.Trigger(2);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"A disposed session accepted its old wave");
    }
   }
   using(var session=NewSession()){
    var move=Prepare();Action declined=null,contact=null;
    bool Decline(FightBattleSkillCastEvent c,FightBattleMovementEvent m,FightGridPoint p,Action action){declined=action;return false;}
    bool Accept(FightBattleSkillCastEvent c,FightBattleMovementEvent m,FightGridPoint p,Action action){Check(ReferenceEquals(move,m)&&p.X==6&&p.Y==7,"incorrect blocked identity");contact=action;return true;}
    Check(!session.Pump(0,StartCast,null,null,Decline)&&session.PendingMovement!=null,"declined movement was released");declined();Check(FightBattleImpactLatch.Last.Records==0,"declined callback remained armed");
    Check(session.Pump(0,StartCast,null,null,Accept)&&FightBattleImpactLatch.Last.Completions==1,"accepted movement was not started");declined();Check(FightBattleImpactLatch.Last.Records==0,"an old declined callback released a later start");
    int waits=FightBattleImpactLatch.Last.Waits;contact();Check(FightBattleImpactLatch.Last.Records==1&&ReferenceEquals(FightBattleImpactLatch.Last.LastContact,move)&&FightBattleImpactLatch.Last.Waits==waits+1,"actual contact did not resume the same worker");contact();Check(FightBattleImpactLatch.Last.Waits==waits+1,"duplicate contact resumed worker twice");
   }
   using(var session=NewSession()){
    Prepare();bool StartEarly(FightBattleSkillCastEvent c,FightBattleMovementEvent m,FightGridPoint p,Action action){action();return true;}
    Check(session.Pump(0,StartCast,null,null,StartEarly)&&FightBattleImpactLatch.Last.Records==1&&FightBattleImpactLatch.Last.Completions==1,"synchronous contact was not held until start accepted");
   }
   using(var session=NewSession()){
    Prepare();Action stale=null;bool Incorrect(FightBattleSkillCastEvent c,FightBattleMovementEvent m,FightGridPoint p,Action action){stale=action;action();return false;}
    bool rejected=false;try{session.Pump(0,StartCast,null,null,Incorrect);}catch(InvalidOperationException){rejected=true;}
    Check(rejected&&FightBattleImpactLatch.Last.Records==0&&session.PendingMovement!=null,"declined synchronous contact was accepted");stale();Check(FightBattleImpactLatch.Last.Records==0,"rejected callback was not disabled");
   }
   {
    var session=NewSession();Prepare();Action stale=null;bool Start(FightBattleSkillCastEvent c,FightBattleMovementEvent m,FightGridPoint p,Action action){stale=action;return true;}
    session.Pump(0,StartCast,null,null,Start);var latch=FightBattleImpactLatch.Last;session.Dispose();stale();Check(latch.Records==0,"disposed session accepted a contact");
   }
   Console.WriteLine("Blocked live-session routing, early contact, retry and cancellation passed.");
  }
 }
}
