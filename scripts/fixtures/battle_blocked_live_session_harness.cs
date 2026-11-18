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
 public class FightBattleHitScopeEntry {public float Delay;}
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
  public static FightBattleImpactLatch Last;public FightBattlePreparedMovement Pending;public int Completions,Records,Waits;public bool Arrived,Disposed;public FightBattleMovementEvent LastContact;
  public FightBattleImpactLatch(){Last=this;}
  public bool TryTake(out FightBattlePendingImpact v){v=null;return false;}
  public bool TryTakePreparation(out FightBattlePreparedCast v){v=null;return false;}
  public void CompletePreparation(FightBattlePreparedCast x,out long revision){revision=1;}
  public bool TryTakeMovement(out FightBattlePreparedMovement v){v=Pending;Pending=null;return v!=null;}
  public void CompleteMovement(FightBattlePreparedMovement v,out long revision){Completions++;revision=1;}
  public bool RecordBlockedContact(FightBattleMovementEvent v,out long revision){revision=0;if(Disposed)return false;LastContact=v;Records++;if(Arrived)return false;Arrived=true;revision=2;return true;}
  public void WaitForWorkerBoundary(long revision){Waits++;}
  public bool TryTakeSelection(out FightBattleSelectedHit v){v=null;return false;}
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
 public class OfficialBattleSkillEntityPlayback {public event Action<FightBattleSkillCastEvent,EcEntityIdentifier,GameObject,MyPlayable> SkillGraphPlayableStarted;public void CompleteLiveCast(FightBattleSkillCastEvent c,EcEntityIdentifier id,float t){} }
 public class OfficialBattleImpactViewBridge:IDisposable {
  public event Action<long> WorkerReleasedByArrival;
  public OfficialBattleImpactViewBridge(OfficialBattleSkillEntityPlayback v,FightBattleImpactLatch l){}
  public long QueueSelection(FightBattleSelectedHit h,EcEntityIdentifier id,float t)=>1;
  public bool TryQueueSettlement(FightBattleSettledImpact s,EcEntityIdentifier id,out long revision,Action<FightBattleDamageEvent> after){revision=0;return false;}
  public void RecordArrival(FightBattleSkillCastEvent c,FightBattleHitSelectionSnapshot h,int a,int b){}
  public void Dispose(){}
 }
 public static class BlockedLiveSessionHarness {
  private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  private static bool StartCast(FightBattleSkillCastEvent cast,out EcEntityIdentifier id){id=new EcEntityIdentifier();return true;}
  private static OfficialBattleLiveImpactSession NewSession(){return new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),new OfficialBattleSkillEntityPlayback(),0,1,true);}
  private static FightBattleMovementEvent Prepare(){var move=new FightBattleMovementEvent{UnitId=4};FightBattleImpactLatch.Last.Pending=new FightBattlePreparedMovement{Movement=move,HasBlockedPosition=true,BlockedPosition=new FightGridPoint(6,7)};return move;}
  public static void Run(){
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
