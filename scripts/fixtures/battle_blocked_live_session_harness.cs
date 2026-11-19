#pragma warning disable 0067
using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;
using UnityEngine;

namespace UnityEngine { public class GameObject { public string name; } public static class Time { public static float timeScale=1f; } public struct Vector3 { public static Vector3 operator -(Vector3 a,Vector3 b)=>a; } }
namespace Zjcs.Official.World.NativeTerrain.Common { public struct Pos2d { public int X,Y; public Pos2d(int x,int y){X=x;Y=y;} } }
namespace Zjcs.Official.StoryPlayables { public class MyPlayable { public string name; public UnityEngine.GameObject myTarget; } public struct EcEntityIdentifier { public int Value; } }
namespace Zjcs.Combat.Official {
 public struct FightGridPoint {public int X,Y;public FightGridPoint(int x,int y){X=x;Y=y;}}
 public class FightBattleCheckpoint {public List<FightBattleUnitState> Units;}
 public class FightBattleUnitState {public int Id;public List<FightBattleSkillDefinition> Skills;}
 public class FightBattleSkillDefinition {public int EntityId;public List<FightBattleHitEntity> HitGraphEntities;public List<FightBattleHitRoot> HitGraphRoots;}
 public class FightBattleHitRoot {public int EntityId;public float Delay;}
 public class FightBattleHitEntity {public int EntityId;public List<FightBattleHitScopeEntry> ScopeEntries;}
 public class FightBattleHitScopeEntry {public float Delay;public FightGridPoint Position;}
 public class FightBattleSkillCastPresentation {public float Duration;public int FacingIndex,SourceX,SourceY;}
 public class FightBattleSkillCastEvent {public int SkillEntityId,ActorId;public FightBattleSkillCastPresentation Presentation;public List<FightBattleHitSelectionSnapshot> HitSelections;}
 public class FightBattleHitSelectionSnapshot {public int HitEntityId;public float Delay,DelayDistFactor;public List<FightGridPoint> EffectCenters=new List<FightGridPoint>();public List<TargetOccurrence> Targets;public class TargetOccurrence {public int EffectCenterIndex,ScopeIndex;public float ScopeDelay;public bool HasScopeIndex;}public class SpawnedUnit {}}
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
 public class FightBattleStatusViewEvent {}
 public class FightBattleGridViewEvent {}
 public class FightBattleUnitLifeEvent {}
 public class FightBattleSecondsStatusNotice {public FightBattleStatusViewEvent Event;}
 public class FightBattleStatusViewNotice {public FightBattleStatusViewEvent Event;}
 public class FightBattleGridViewNotice {public FightBattleGridViewEvent Event;}
 public class FightBattleDisperseNotice {public FightBattleDamageEvent Damage;}
 public class FightBattleUnitLifeNotice {public FightBattleUnitLifeEvent Event;}
 public class FightBattleSummonNotice {public FightBattleUnitState Unit;public FightBattleHitSelectionSnapshot.SpawnedUnit Receipt;}
 public class FightBattleImpactLatch {
  public static FightBattleImpactLatch Last;public FightBattlePreparedMovement Pending;public FightBattlePreparedCast PendingCast;public FightBattleSelectedHit PendingSelection;public FightBattleSecondsStatusNotice PendingStatus;public FightBattleStatusViewNotice PendingView;public FightBattleGridViewNotice PendingGrid;public FightBattleSettledImpact PendingResult;public FightBattleCompletedCast PendingCompletion;public int Completions,CastCompletions,ViewCompletions,GridCompletions,Records,Waits;public bool Arrived,Disposed;public FightBattleMovementEvent LastContact;
  private object active;private readonly Stack<object> suspended=new Stack<object>();
  public object CurrentWorkerHandoff => (object)PendingCast??Pending??(object)PendingSelection??PendingStatus??(object)PendingView??(object)PendingGrid??(object)PendingResult??PendingCompletion??active;
  private void Taken(object value){if(value==null)return;if(active!=null)suspended.Push(active);active=value;}
  private void Finished(object value){if(!ReferenceEquals(active,value))throw new InvalidOperationException("Wrong active handoff");active=suspended.Count>0?suspended.Pop():null;}
  public FightBattleImpactLatch(){Last=this;}
  public bool TryTake(out FightBattlePendingImpact v){v=null;return false;}
  public bool TryTakeSecondsStatus(out FightBattleSecondsStatusNotice v){v=PendingStatus;PendingStatus=null;Taken(v);return v!=null;}
  public void CompleteSecondsStatus(FightBattleSecondsStatusNotice v,out long revision){Finished(v);revision=1;}
  public bool TryTakeStatusView(out FightBattleStatusViewNotice v){v=PendingView;PendingView=null;Taken(v);return v!=null;}
  public bool TryTakeGridView(out FightBattleGridViewNotice v){v=PendingGrid;PendingGrid=null;Taken(v);return v!=null;}
  public void CompleteGridView(FightBattleGridViewNotice v,out long revision){Finished(v);GridCompletions++;revision=1;}
  public bool TryTakeDisperse(out FightBattleDisperseNotice v){v=null;return false;}
  public void CompleteDisperse(FightBattleDisperseNotice v,out long revision){Finished(v);revision=1;}
  public void CompleteStatusView(FightBattleStatusViewNotice v,out long revision){Finished(v);ViewCompletions++;revision=1;}
  public bool TryTakeUnitLife(out FightBattleUnitLifeNotice v){v=null;return false;}
  public void CompleteUnitLife(FightBattleUnitLifeNotice v,out long revision){Finished(v);revision=1;}
  public bool TryTakeSummon(out FightBattleSummonNotice v){v=null;return false;}
  public void CompleteSummon(FightBattleSummonNotice v,out long revision){Finished(v);revision=1;}
  public bool TryTakePreparation(out FightBattlePreparedCast v){v=PendingCast;PendingCast=null;Taken(v);return v!=null;}
  public void CompletePreparation(FightBattlePreparedCast x,out long revision){Finished(x);revision=1;}
  public bool TryTakeMovement(out FightBattlePreparedMovement v){v=Pending;Pending=null;Taken(v);return v!=null;}
  public void CompleteMovement(FightBattlePreparedMovement v,out long revision){Finished(v);Completions++;revision=1;}
  public bool RecordBlockedContact(FightBattleMovementEvent v,out long revision){revision=0;if(Disposed)return false;LastContact=v;Records++;if(Arrived)return false;Arrived=true;revision=2;return true;}
  public void WaitForWorkerBoundary(long revision){Waits++;}
  public bool TryTakeSelection(out FightBattleSelectedHit v){v=PendingSelection;PendingSelection=null;Taken(v);return v!=null;}
  public void CompleteSelection(FightBattleSelectedHit v){Finished(v);}
  public bool TryTakeSettlement(out FightBattleSettledImpact v){v=PendingResult;PendingResult=null;Taken(v);return v!=null;}
  public bool TryTakeCastCompletion(out FightBattleCompletedCast v){v=PendingCompletion;PendingCompletion=null;Taken(v);return v!=null;}
  public void DeferStatusResolution(FightBattleSettledImpact x,out long revision){revision=1;}
  public void CompleteCast(FightBattleCompletedCast c,out long revision){Finished(c);CastCompletions++;revision=1;}
  public bool TryCompleteSettlement(FightBattleSettledImpact x,Func<FightBattlePendingImpact,FightBattleDamageEvent,bool> f,out long revision){revision=0;if(!f(x.Impact,x.Damage))return false;Finished(x);revision=1;return true;}
 }
 public class FightBattleIsolatedStep:IDisposable {
  public readonly FightBattleImpactLatch Latch=new FightBattleImpactLatch();public bool IsCompleted;
  public FightBattleIsolatedStep(FightBattleCheckpoint c,Func<FightBattleCheckpoint,FightBattleExecutor> restore,int s,int n,bool observe,bool observeStatusViews,bool observeUnitLives,bool observeSummons,bool observeDisperses,bool observeGridViews){}
  public bool TryAdvanceStatusSeconds(double s,out System.Threading.Tasks.Task<bool> task){task=null;return false;}
  public bool TryTakeCompleted(out FightBattleExecutor e,out FightBattleExecutionResult r){e=null;r=null;return false;}
  public void Dispose(){Latch.Disposed=true;}
 }
}
namespace Zjcs.Presentation.Combat {
 internal sealed class OfficialBattleFlameSprayImpact {internal static OfficialBattleFlameSprayImpact Attach(GameObject p,MyPlayable leaf,FightBattleHitRoot[] roots,FightBattleHitEntity hit,Vector3 origin,Vector3 step,Action<int,int> arrival)=>null;internal void Detach(){}}
 internal sealed class OfficialBattleWindBladeImpact {internal static OfficialBattleWindBladeImpact Last;private Action<int> arrival;internal static OfficialBattleWindBladeImpact Attach(UnityEngine.GameObject p,MyPlayable leaf,FightBattleHitEntity hit,Action<int> a){Last=new OfficialBattleWindBladeImpact{arrival=a};return Last;}internal void Trigger(int index){arrival?.Invoke(index);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleIceConeImpact {internal static OfficialBattleIceConeImpact Last;private Action<int> arrival;internal static OfficialBattleIceConeImpact Attach(UnityEngine.GameObject p,MyPlayable leaf,FightBattleHitEntity hit,Action<int> a){Last=new OfficialBattleIceConeImpact{arrival=a};return Last;}internal void Trigger(int index){arrival?.Invoke(index);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleFallingWindImpact {internal static OfficialBattleFallingWindImpact Last;private Action<int> arrival;internal static OfficialBattleFallingWindImpact Attach(UnityEngine.GameObject p,MyPlayable leaf,FightBattleHitRoot root,FightBattleHitEntity hit,Action<int> a){Last=new OfficialBattleFallingWindImpact{arrival=a};return Last;}internal void Trigger(int index){arrival?.Invoke(index);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleFireRingImpact {internal static OfficialBattleFireRingImpact Last;private Action<int> arrival;internal static OfficialBattleFireRingImpact Attach(UnityEngine.GameObject p,MyPlayable leaf,FightBattleHitRoot root,FightBattleHitEntity hit,Action<int> a){Last=new OfficialBattleFireRingImpact{arrival=a};return Last;}internal void Trigger(int index){arrival?.Invoke(index);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleSwordWindImpact {internal static OfficialBattleSwordWindImpact Last;private Action<float> arrival;internal static OfficialBattleSwordWindImpact Attach(UnityEngine.GameObject p,MyPlayable leaf,Action<float> a){Last=new OfficialBattleSwordWindImpact{arrival=a};return Last;}internal void Trigger(float delay){arrival?.Invoke(delay);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleTargetParticleImpact {internal static OfficialBattleTargetParticleImpact Last;internal string Particle;private Action<float> arrival;internal static OfficialBattleTargetParticleImpact Attach(UnityEngine.GameObject p,MyPlayable leaf,string particle,Action<float> a){Last=new OfficialBattleTargetParticleImpact{arrival=a,Particle=particle};return Last;}internal static OfficialBattleTargetParticleImpact AttachWaterDragon(UnityEngine.GameObject p,MyPlayable leaf,int wave,float delay,Action<float> a)=>Attach(p,leaf,"hit (3)",a);internal void Trigger(float delay){arrival?.Invoke(delay);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleProjectileImpact {internal static OfficialBattleProjectileImpact Last;private Action arrival;internal static OfficialBattleProjectileImpact Attach(MyPlayable leaf,float factor,Action a){if(factor!=.1f&&factor!=.125f)return null;Last=new OfficialBattleProjectileImpact{arrival=a};return Last;}internal void Trigger(){var callback=arrival;arrival=null;callback?.Invoke();}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleMountainWaveImpact {internal static OfficialBattleMountainWaveImpact Last;private Action<int> arrival;internal static OfficialBattleMountainWaveImpact Attach(MyPlayable p,Action<int> a){Last=new OfficialBattleMountainWaveImpact{arrival=a};return Last;}internal void Trigger(int wave){arrival?.Invoke(wave);}internal void Detach(){arrival=null;}}
 internal sealed class OfficialBattleSlashImpact {internal static OfficialBattleSlashImpact Last;internal string Particle;private Action arrival;internal static OfficialBattleSlashImpact Attach(MyPlayable p,string particle,Action a){Last=new OfficialBattleSlashImpact{arrival=a,Particle=particle};return Last;}internal void Trigger(){arrival?.Invoke();}internal void Detach(){arrival=null;}}
 public class OfficialBattleSkillEntityPlayback {public Vector3 GridToPos(Zjcs.Official.World.NativeTerrain.Common.Pos2d pos)=>new Vector3();public Vector3 ToWorldPos(Vector3 pos)=>pos;public event Action<FightBattleSkillCastEvent,EcEntityIdentifier,GameObject,MyPlayable> SkillGraphPlayableStarted;public event Action<FightBattleSkillCastEvent,FightBattleHitSelectionSnapshot,int,GameObject,MyPlayable> HitGraphPlayableStarted;internal event Func<EcEntityIdentifier,EcEntityIdentifier,Action,bool> HitViewDeferralRequested;internal bool TryGetHitIdentity(GameObject target,out EcEntityIdentifier id){id=new EcEntityIdentifier{Value=7};return target!=null;}internal bool HasLiveHit(EcEntityIdentifier id)=>true;internal bool Defer(EcEntityIdentifier skill,EcEntityIdentifier hit,Action resume)=>HitViewDeferralRequested?.Invoke(skill,hit,resume)??false;public void CompleteLiveCast(FightBattleSkillCastEvent c,EcEntityIdentifier id,float t){} public void Graph(FightBattleSkillCastEvent c,string prefab="playable11120",string leaf="effect"){SkillGraphPlayableStarted?.Invoke(c,new EcEntityIdentifier(),new GameObject{name=prefab},new MyPlayable{name=leaf});}public void HitGraph(FightBattleSkillCastEvent c,FightBattleHitSelectionSnapshot hit,int center,string leaf,string prefab="playable10701"){HitGraphPlayableStarted?.Invoke(c,hit,center,new GameObject{name=prefab},new MyPlayable{name=leaf,myTarget=new GameObject()});} }
 public class OfficialBattleImpactViewBridge:IDisposable {
  public static OfficialBattleImpactViewBridge Last;
  public static readonly List<int> Arrivals=new List<int>();
  private readonly FightBattleImpactLatch latch;
  public event Action<long> WorkerReleasedByArrival;
  internal Func<FightBattleSkillCastEvent,FightBattleHitSelectionSnapshot,int,bool> IgnoreNativeScopeArrival;
  public OfficialBattleImpactViewBridge(OfficialBattleSkillEntityPlayback v,FightBattleImpactLatch l){latch=l;Last=this;}
  public void NativeArrival(FightBattleSkillCastEvent c,FightBattleHitSelectionSnapshot h,int center,int scope){if(IgnoreNativeScopeArrival?.Invoke(c,h,scope)!=true)RecordArrival(c,h,center,scope);}
  public long QueueSelection(FightBattleSelectedHit h,EcEntityIdentifier id,float t){latch.CompleteSelection(h);return 1;}
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
   foreach(bool arrivalFirst in new[]{false,true})
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10800,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true)){
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10801,DelayDistFactor=.125f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     OfficialBattleProjectileImpact.Last=null;views.HitGraph(cast,selection,0,"track","playable_10801_SourceToHit");
     var observer=OfficialBattleProjectileImpact.Last;Check(observer!=null,"The fireball projectile was not observed");
     int effects=0;var hitId=new EcEntityIdentifier{Value=7};
     if(arrivalFirst)observer.Trigger();
     Check(!views.Defer(new EcEntityIdentifier{Value=4},hitId,()=>effects++),"An unrelated cast deferred a fireball effect");
     bool deferred=views.Defer(new EcEntityIdentifier(),hitId,()=>effects++);
     Check(deferred && effects==(arrivalFirst?1:0),"The fireball delayed view ignored projectile ordering");
     if(!arrivalFirst){observer.Trigger();observer.Trigger();Check(effects==1,"The original fireball effect did not resume exactly once");}
     Check(effects==1 && !views.Defer(new EcEntityIdentifier(),hitId,()=>effects++),"The arrived fireball continued deferring its original effect");
    }
   }
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10800,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true);
    FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
    var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10801,DelayDistFactor=.125f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
    cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
    views.HitGraph(cast,selection,0,"track","playable_10801_SourceToHit");var observer=OfficialBattleProjectileImpact.Last;int effects=0;
    Check(observer!=null && views.Defer(new EcEntityIdentifier(),new EcEntityIdentifier{Value=7},()=>effects++),"The fireball effect was not held");
    session.Dispose();observer.Trigger();Check(effects==0,"A cancelled fireball resumed its held effect");
   }
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10800,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true)){
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10801,DelayDistFactor=.125f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     selection.EffectCenters.Add(new FightGridPoint(7,5));cast.HitSelections.Add(selection);
     FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     FightBattleImpactLatch.Last.PendingCompletion=new FightBattleCompletedCast{Cast=cast};session.Pump(0,StartCast);
     Check(FightBattleImpactLatch.Last.CastCompletions==0,"An empty fireball finished before its projectile arrived");
     views.HitGraph(cast,selection,0,"track","playable_10801_SourceToHit");var observer=OfficialBattleProjectileImpact.Last;int effects=0;
     Check(observer!=null && views.Defer(new EcEntityIdentifier(),new EcEntityIdentifier{Value=7},()=>effects++),"The empty fireball effect was not held");
     observer.Trigger();Check(effects==1 && FightBattleImpactLatch.Last.CastCompletions==1,"The empty fireball did not finish after its original effect");
    }
   }
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10500,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();OfficialBattleProjectileImpact.Last=null;
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10501,DelayDistFactor=.1f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     views.HitGraph(cast,selection,0,"effect","playable10501");var observer=OfficialBattleProjectileImpact.Last;
     Check(observer!=null&&OfficialBattleImpactViewBridge.Arrivals.Count==0,"The water projectile must not settle at launch");
     observer.Trigger();observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The projectile endpoint must settle its selected scope once");
    }
   }
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10850,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true)){
     bool StartInline(FightBattleSkillCastEvent current,out EcEntityIdentifier id){id=new EcEntityIdentifier();views.Graph(current,"playable10850","effect");return true;}
     OfficialBattleImpactViewBridge.Arrivals.Clear();OfficialBattleTargetParticleImpact.Last=null;
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartInline);
     var observer=OfficialBattleTargetParticleImpact.Last;Check(observer!=null&&observer.Particle=="hit (4)","The aura child lost its original hit effect");
     observer.Trigger(0f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The aura effect fabricated an unselected hit");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10851,Delay=0f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartInline);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The aura effect did not reach its selected target");
     observer.Trigger(0f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The aura effect repeated a hit");
     session.Dispose();observer.Trigger(0f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"A disposed aura effect repeated a hit");
    }
   }
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=11300,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true)){
     bool StartInline(FightBattleSkillCastEvent current,out EcEntityIdentifier id){id=new EcEntityIdentifier();views.Graph(current,"playable11300","effect");return true;}
     OfficialBattleImpactViewBridge.Arrivals.Clear();OfficialBattleTargetParticleImpact.Last=null;
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartInline);
     var observer=OfficialBattleTargetParticleImpact.Last;Check(observer!=null&&observer.Particle=="hit_da","The ice child lost its zero-time original effect");
     observer.Trigger(0f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The ice effect fabricated an unselected hit");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=11301,Delay=0f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartInline);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The ice effect did not reach its selected target");
     observer.Trigger(0f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The ice effect repeated a hit");
     session.Dispose();observer.Trigger(0f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"A disposed ice effect repeated a hit");
    }
   }
   {
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=11290,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(new FightBattleCheckpoint(),_=>new FightBattleExecutor(),views,0,1,true)){
     bool StartInline(FightBattleSkillCastEvent current,out EcEntityIdentifier id){id=new EcEntityIdentifier();views.Graph(current,"playable11290","effect");return true;}
     OfficialBattleImpactViewBridge.Arrivals.Clear();OfficialBattleSwordWindImpact.Last=null;
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartInline);
     var observer=OfficialBattleSwordWindImpact.Last;Check(observer!=null,"The triggered child lost its zero-time original effect");
     observer.Trigger(.1f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The passive fabricated an unselected hit");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=11291,Delay=.1f,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartInline);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The original passive ring did not reach its selected target");
     observer.Trigger(.1f);Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The passive ring repeated a hit");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10601,ScopeEntries=new List<FightBattleHitScopeEntry>{new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1)}}};
    var skill=new FightBattleSkillDefinition{EntityId=10600,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10600,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     int graphEvents=0;session.SkillGraphPlayableStarted+=(c,id,p,l)=>{if(ReferenceEquals(c,cast)&&l.name=="effect")graphEvents++;};
     bool StartInline(FightBattleSkillCastEvent current,out EcEntityIdentifier id){id=new EcEntityIdentifier();views.Graph(current,"playable10600","effect");return true;}
     OfficialBattleImpactViewBridge.Arrivals.Clear();OfficialBattleSlashImpact.Last=null;
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartInline);
     var observer=OfficialBattleSlashImpact.Last;
     Check(observer!=null&&graphEvents==1,"A zero-time original graph leaf must attach exactly once during cast creation");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10601,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartInline);
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The zero-time graph leaf did not retain its selected hit");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10591,ScopeEntries=new List<FightBattleHitScopeEntry>{
     new FightBattleHitScopeEntry{Position=new FightGridPoint(0,0)},new FightBattleHitScopeEntry{Position=new FightGridPoint(-1,0),Delay=.1f},
     new FightBattleHitScopeEntry{Position=new FightGridPoint(1,0),Delay=.1f},new FightBattleHitScopeEntry{Position=new FightGridPoint(0,-1),Delay=.1f},
     new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1),Delay=.1f}}};
    var skill=new FightBattleSkillDefinition{EntityId=10590,HitGraphEntities=new List<FightBattleHitEntity>{hit},HitGraphRoots=new List<FightBattleHitRoot>{new FightBattleHitRoot{EntityId=10591,Delay=.45f}}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10590,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     views.Graph(cast,"playable10590");var observer=OfficialBattleFallingWindImpact.Last;Check(observer!=null,"The original falling sword was not observed");
     observer.Trigger(0);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The sword fabricated a selection before one existed");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10591,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     for(int index=0;index<5;index++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=index});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1&&OfficialBattleImpactViewBridge.Arrivals[0]==0,"The landed sword did not reach its centre scope");
     observer.Trigger(1);observer.Trigger(1);Check(OfficialBattleImpactViewBridge.Arrivals.Count==5,"The ground effect did not reach the four outer scopes once");
    }
   }
   foreach(bool empty in new[]{false,true})
   {
    var hit=new FightBattleHitEntity{EntityId=10571,ScopeEntries=new List<FightBattleHitScopeEntry>{
     new FightBattleHitScopeEntry{Position=new FightGridPoint(0,0)},new FightBattleHitScopeEntry{Position=new FightGridPoint(-1,0)},
     new FightBattleHitScopeEntry{Position=new FightGridPoint(1,0)},new FightBattleHitScopeEntry{Position=new FightGridPoint(0,-1)},
     new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1)}}};
    var skill=new FightBattleSkillDefinition{EntityId=10570,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10570,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10571,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     selection.EffectCenters.Add(new FightGridPoint(4,4));if(!empty)for(int index=0;index<5;index++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=index});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     OfficialBattleImpactViewBridge.Last.NativeArrival(cast,selection,0,0);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The unplayed thorn particle must not release the original scope arrival");
     views.HitGraph(cast,selection,0,"effect","playable_scope_10570");var observer=OfficialBattleTargetParticleImpact.Last;
     Check(observer!=null&&observer.Particle=="tieji (1)","The original thorn particle was not observed");
     FightBattleImpactLatch.Last.PendingCompletion=new FightBattleCompletedCast{Cast=cast};session.Pump(0,StartCast);
     Check(FightBattleImpactLatch.Last.CastCompletions==0,"The thorn cast completed before its visible effect");
     observer.Trigger(.05f);observer.Trigger(.05f);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==(empty?0:5)&&FightBattleImpactLatch.Last.CastCompletions==1,
      "The actual thorn particle must release each selected scope once, including an empty cast");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10901,ScopeEntries=new List<FightBattleHitScopeEntry>()};
    for(int index=0;index<5;index++)hit.ScopeEntries.Add(new FightBattleHitScopeEntry{Position=new FightGridPoint(0,index+1)});
    var skill=new FightBattleSkillDefinition{EntityId=10900,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10900,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     views.Graph(cast,"playable10900","effect (3)");var observer=OfficialBattleIceConeImpact.Last;Check(observer!=null,"The original ice cone was not observed");
     observer.Trigger(0);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The ice cone fabricated an unselected hit");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10901,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     for(int index=0;index<5;index++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=index});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.SequenceEqual(new[]{0}),"A previously reached first scope must be delivered once selected");
     for(int index=1;index<5;index++){observer.Trigger(index);observer.Trigger(index);}
     Check(OfficialBattleImpactViewBridge.Arrivals.SequenceEqual(new[]{0,1,2,3,4}),"The ice cone must reach each scope once in order");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10831,ScopeEntries=new List<FightBattleHitScopeEntry>()};
    for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)if(Math.Abs(x)+Math.Abs(y)<=2)
     hit.ScopeEntries.Add(new FightBattleHitScopeEntry{Position=new FightGridPoint(x,y),Delay=.05f*(Math.Abs(x)+Math.Abs(y)+1)});
    var skill=new FightBattleSkillDefinition{EntityId=10830,HitGraphEntities=new List<FightBattleHitEntity>{hit},HitGraphRoots=new List<FightBattleHitRoot>{new FightBattleHitRoot{EntityId=10831,Delay=.5f}}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10830,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     views.Graph(cast,"playable10830");var observer=OfficialBattleFireRingImpact.Last;Check(observer!=null,"The original fire ring was not observed");
     observer.Trigger(0);observer.Trigger(1);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The fire ring fabricated unselected hits");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10831,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     for(int index=0;index<13;index++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=index});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==5,"Previously reached centre and near scopes must be delivered once selected");
     observer.Trigger(2);observer.Trigger(2);Check(OfficialBattleImpactViewBridge.Arrivals.Count==13,"The fire ring must reach all eight outer scopes once");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10701,ScopeEntries=new List<FightBattleHitScopeEntry>()};
    for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)hit.ScopeEntries.Add(new FightBattleHitScopeEntry{Position=new FightGridPoint(x,y),Delay=.15f});
    var skill=new FightBattleSkillDefinition{EntityId=10700,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10700,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10701,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     for(int index=0;index<9;index++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=index});
     selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     views.HitGraph(cast,selection,0,"LineEffect");Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"The initial pull line must not settle damage");
     views.HitGraph(cast,selection,0,"AtkEffect");
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==9,"The original pull attack effect did not reach all nine scopes once");
     views.HitGraph(cast,selection,0,"AtkEffect");Check(OfficialBattleImpactViewBridge.Arrivals.Count==9,"Pull attack effect repeated a scope arrival");
    }
   }
   {
    using(var session=NewSession()){
     var transition=new FightBattleGridViewEvent();
     FightBattleImpactLatch.Last.PendingGrid=new FightBattleGridViewNotice{Event=transition};
     Check(!session.Pump(0,StartCast)&&session.PendingGridView!=null&&FightBattleImpactLatch.Last.GridCompletions==0,
      "An unhandled live grid transition must remain held for its view");
     int delivered=0;session.GridViewChanged+=value=>{Check(ReferenceEquals(value,transition),"The live grid snapshot changed during handoff");delivered++;};
     Check(session.Pump(0,StartCast)&&delivered==1&&FightBattleImpactLatch.Last.GridCompletions==1,
      "A held grid transition must resume exactly once after presentation");
    }
    using(var session=NewSession()){
     var transition=new FightBattleStatusViewEvent();int delivered=0;
     session.StatusViewChanged+=value=>{Check(ReferenceEquals(value,transition),"The live status snapshot changed during handoff");delivered++;};
     FightBattleImpactLatch.Last.PendingView=new FightBattleStatusViewNotice{Event=transition};
     Check(session.Pump(0,StartCast)&&delivered==1&&FightBattleImpactLatch.Last.ViewCompletions==1,
      "A live status transition must be presented once before its rule worker resumes");
    }
    using(var session=NewSession()){
     FightBattleImpactLatch.Last.PendingView=new FightBattleStatusViewNotice{Event=new FightBattleStatusViewEvent()};
     Check(!session.Pump(0,StartCast)&&session.PendingStatusView!=null&&FightBattleImpactLatch.Last.ViewCompletions==0,
      "An unhandled live status must remain held until a presentation listener is available");
     int delivered=0;session.StatusViewChanged+=_=>delivered++;
     Check(session.Pump(0,StartCast)&&delivered==1&&FightBattleImpactLatch.Last.ViewCompletions==1,
      "The held live status must resume once a presentation listener is available");
    }
   }
   {
    var child=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10100};
    using(var session=NewSession()){
     var notice=new FightBattleSecondsStatusNotice{Event=new FightBattleStatusViewEvent()};
     FightBattleImpactLatch.Last.PendingStatus=notice;
     Check(!session.Pump(0,StartCast),"A status without a presentation listener must stay held");
     int children=0,notices=0;
     bool StartChild(FightBattleSkillCastEvent cast,out EcEntityIdentifier id){id=new EcEntityIdentifier();children++;return ReferenceEquals(cast,child);}
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=child};
     Check(session.Pump(0,StartChild)&&children==1&&notices==0&&ReferenceEquals(session.PendingSecondsStatus,notice),
      "A nested collision cast must start while an outer status notice is held");
     session.SecondsStatusChanged+=_=>notices++;
     Check(session.Pump(0,StartChild)&&notices==1&&children==1,
      "The outer status notice must resume exactly once after its nested cast");
    }
   }
   {
    var parent=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10640};
    var child=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10100};
    using(var session=NewSession()){
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=parent};session.Pump(0,StartCast);
     var result=new FightBattleSettledImpact{Impact=new FightBattlePendingImpact{Cast=parent},Damage=new FightBattleDamageEvent{HasDamageTriggerStatus=true}};
     FightBattleImpactLatch.Last.PendingResult=result;
     Check(!session.Pump(0,StartCast)&&ReferenceEquals(session.PendingSettlement,result),"An unavailable damage view must remain held");
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=child};
     int delivered=0;
     Check(!session.TryCompletePendingSettlement((impact,damage)=>{delivered++;return true;})&&delivered==0,
      "A suspended outer result must not complete while a collision child is current");
     Check(session.Pump(0,StartCast),"A collision child must start while an outer result remains held");
     Check(session.TryCompletePendingSettlement((impact,damage)=>{delivered++;return ReferenceEquals(impact,result.Impact);})&&delivered==1,
      "The outer result must resume exactly once after the collision child");
    }
   }
   {
    var parent=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10640};
    var child=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10100};
    using(var session=NewSession()){
     var started=new List<FightBattleSkillCastEvent>();bool allowParent=false;
     bool StartNested(FightBattleSkillCastEvent cast,out EcEntityIdentifier id){id=new EcEntityIdentifier();if(ReferenceEquals(cast,parent)&&!allowParent)return false;started.Add(cast);return true;}
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=parent};
     Check(!session.Pump(0,StartNested),"The initial unavailable cast must remain held");
     FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=child};
     Check(session.Pump(0,StartNested)&&started.Count==1&&ReferenceEquals(started[0],child),
      "A nested collision cast must start while an outer cast remains held");
     allowParent=true;Check(session.Pump(0,StartNested)&&started.Count==2&&ReferenceEquals(started[1],parent),
      "The outer cast must resume after its nested collision");
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10641,ScopeEntries=new List<FightBattleHitScopeEntry>()};
    for(int i=1;i<=4;i++)hit.ScopeEntries.Add(new FightBattleHitScopeEntry{Position=new FightGridPoint(0,i)});
    var skill=new FightBattleSkillDefinition{EntityId=10640,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10640,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     OfficialBattleWindBladeImpact.Last=null;views.Graph(cast,"playable10640","effect (1)");Check(OfficialBattleWindBladeImpact.Last==null,"Wind buildup cannot deliver a hit");
     views.Graph(cast,"playable10640","effect (2)");var observer=OfficialBattleWindBladeImpact.Last;Check(observer!=null,"Original wind blade was not observed");
     observer.Trigger(0);Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"An early blade fabricated its hit selection");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10641,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     for(int i=0;i<4;i++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=i});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1&&OfficialBattleImpactViewBridge.Arrivals[0]==0,"The first blade scope was not retained");
     for(int i=1;i<4;i++){observer.Trigger(i);observer.Trigger(i);Check(OfficialBattleImpactViewBridge.Arrivals.Count==i+1&&OfficialBattleImpactViewBridge.Arrivals[i]==i,"Blade scope was missing or repeated");}
     session.Dispose();observer.Trigger(0);Check(OfficialBattleImpactViewBridge.Arrivals.Count==4,"A disposed blade accepted a stale callback");
    }
   }
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
    var hit=new FightBattleHitEntity{EntityId=10611,ScopeEntries=new List<FightBattleHitScopeEntry>{new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1)}}};
    var skill=new FightBattleSkillDefinition{EntityId=10610,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10610,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     OfficialBattleSlashImpact.Last=null;views.Graph(cast,"playable10610","effect (1)");Check(OfficialBattleSlashImpact.Last==null,"The heavy windup cannot arm the impact");
     views.Graph(cast,"playable10610","effect (2)");var observer=OfficialBattleSlashImpact.Last;
     Check(observer!=null&&observer.Particle=="dimian_guangyun_an","The original heavy-chop ground impact was not observed");
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"A heavy effect without a selected hit fabricated an arrival");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10611,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==1&&OfficialBattleImpactViewBridge.Arrivals[0]==0,"An early heavy impact was not routed to its selected target");
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==1,"The same heavy impact was delivered twice");
    }
   }
   {
    var hits=new List<FightBattleHitEntity>();for(int i=0;i<3;i++)hits.Add(new FightBattleHitEntity{EntityId=10631+i,ScopeEntries=new List<FightBattleHitScopeEntry>{new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1)}}});
    var skill=new FightBattleSkillDefinition{EntityId=10630,HitGraphEntities=hits};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10630,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     for(int i=0;i<3;i++){
      OfficialBattleSlashImpact.Last=null;views.Graph(cast,"playable10630","effect ("+(i+1)+")");var observer=OfficialBattleSlashImpact.Last;
      Check(observer!=null&&observer.Particle==(i==0?"daoguang da (24)":"daoguang da (28)"),"Combo did not bind its original slash particle");
      observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==i,"An early combo effect fabricated its hit selection");
      var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10631+i,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>{new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=0}}};
      cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
      Check(OfficialBattleImpactViewBridge.Arrivals.Count==i+1,"The combo effect did not reach its own hit selection");
      observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==i+1,"A combo impact was delivered twice");
     }
    }
   }
   {
    var hit=new FightBattleHitEntity{EntityId=10651,ScopeEntries=new List<FightBattleHitScopeEntry>{
     new FightBattleHitScopeEntry{Position=new FightGridPoint(-1,0)},new FightBattleHitScopeEntry{Position=new FightGridPoint(0,-1)},
     new FightBattleHitScopeEntry{Position=new FightGridPoint(0,1)},new FightBattleHitScopeEntry{Position=new FightGridPoint(1,0)}}};
    var skill=new FightBattleSkillDefinition{EntityId=10650,HitGraphEntities=new List<FightBattleHitEntity>{hit}};
    var cp=new FightBattleCheckpoint{Units=new List<FightBattleUnitState>{new FightBattleUnitState{Id=1,Skills=new List<FightBattleSkillDefinition>{skill}}}};
    var views=new OfficialBattleSkillEntityPlayback();var cast=new FightBattleSkillCastEvent{ActorId=1,SkillEntityId=10650,HitSelections=new List<FightBattleHitSelectionSnapshot>()};
    using(var session=new OfficialBattleLiveImpactSession(cp,_=>new FightBattleExecutor(),views,0,1,true)){
     OfficialBattleImpactViewBridge.Arrivals.Clear();FightBattleImpactLatch.Last.PendingCast=new FightBattlePreparedCast{Cast=cast};session.Pump(0,StartCast);
     OfficialBattleSlashImpact.Last=null;views.Graph(cast,"playable10650");var observer=OfficialBattleSlashImpact.Last;
     Check(observer!=null&&observer.Particle=="shizi","The original cross slash particle was not observed");
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==0,"An early cross effect fabricated its hit selection");
     var selection=new FightBattleHitSelectionSnapshot{HitEntityId=10651,Targets=new List<FightBattleHitSelectionSnapshot.TargetOccurrence>()};
     for(int i=0;i<4;i++)selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence{HasScopeIndex=true,ScopeIndex=i});
     cast.HitSelections.Add(selection);FightBattleImpactLatch.Last.PendingSelection=new FightBattleSelectedHit{Cast=cast,Selection=selection};session.Pump(0,StartCast);
     Check(OfficialBattleImpactViewBridge.Arrivals.Count==4,"The cross did not deliver every original scope once");
     observer.Trigger();Check(OfficialBattleImpactViewBridge.Arrivals.Count==4,"The same cross effect was delivered twice");
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
