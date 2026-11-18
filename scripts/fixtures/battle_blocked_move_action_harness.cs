using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object x) => x != null; }
 public class Transform : Object {}
 public class MonoBehaviour : Object {}
 public class GameObject : Object {
  public T[] GetComponents<T>() where T : class => typeof(T)==typeof(Zjcs.Official.EntityActions.MyAction) ? new[]{new Zjcs.Official.EntityActions.MyAction() as T} : new[]{new Zjcs.Official.BattleSkillPlayables.MyFightActionSkillMove() as T};
 }
 public static class Mathf { public static float Min(params float[] v) {float r=float.PositiveInfinity;foreach(var x in v)r=Math.Min(r,x);return r;} public static float Max(float a,float b)=>Math.Max(a,b); }
}
namespace Zjcs.Official.StoryPlayables { public struct EcEntityIdentifier {public int Id; public EcEntityIdentifier(int x){Id=x;}} }
namespace Zjcs.Official.EntityLifecycle {
 using Zjcs.Official.BattleSkillPlayables;
 public class MyContainer {}
 public class MyEntity { public MyFightActionSkillMove Move=new MyFightActionSkillMove(); public T GetComponent<T>() where T:class => Move as T; public void MyStart(){Move.Begin();} }
}
namespace Zjcs.Official.EntityActions {
 using Zjcs.Official.StoryPlayables; using Zjcs.Official.EntityLifecycle;
 public interface IMyActionEntityLookup { MyEntity GetEntity(EcEntityIdentifier id); }
 public class MyAction { public IMyActionEntityLookup EntityLookup; public object mMyPlayableManager; public bool myEffect,myEndEffect,LoopEffectPlayable; public float Duration=-1; }
}
namespace Zjcs.Official.BattleSkillPlayables {
 using UnityEngine;
 public class MyFightActionSkillMove {
  public object mMyPlayableManager; public Transform EntitySlot; public bool ManualTiming; public bool BlockedPlaybackPending; public float RemainingMoveTime=float.PositiveInfinity;
  public void Begin() { BlockedPlaybackPending=true; RemainingMoveTime=0; }
  public void Advance(float delta) { RemainingMoveTime=float.PositiveInfinity; }
 }
}
namespace Zjcs.Offline.BattleNativePlayables { using System; public class OfflineBattleNativePlayableScope { public void Dispatch(Action action)=>action(); } }
namespace Zjcs.Presentation.Combat {
 using System; using UnityEngine; using Zjcs.Official.StoryPlayables; using Zjcs.Official.EntityLifecycle; using Zjcs.Offline.BattleNativePlayables;
 public enum OfficialBattleNativeEntityKind { MoveAction }
 public class OfficialBattleMoveActionData {
  public EcEntityIdentifier Id; public MovementData Movement=new MovementData();
  public class MovementData { public int? BlockedPos; public float TotalMoveDuration; public InfoData Info=new InfoData(); }
  public class InfoData { public float DelayDestroyTime; }
 }
 public class OfficialBattleNativeEntityLoader {
  public static int Removed; public static MyEntity Last; public Transform EntitySlot=new Transform();
  public OfficialBattleNativeEntityLoader(Transform parent, object container, OfflineBattleNativePlayableScope scope, OfficialBattleNativeEntityKind kind, Action<GameObject> init) {init(new GameObject());}
  public MyEntity Create(OfficialBattleMoveActionData data) {Last=new MyEntity();return Last;}
  public void Remove(EcEntityIdentifier id){Removed++;} public void Dispose(){} public bool ContainsClass(int id)=>true; public MyEntity GetEntity(EcEntityIdentifier id)=>Last;
 }
}
public static class BlockedActionHarness {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run() {
  var scope=new Zjcs.Offline.BattleNativePlayables.OfflineBattleNativePlayableScope();
  var views=new Zjcs.Presentation.Combat.OfficialBattleMoveActionPlayback(new UnityEngine.Transform(),null,scope,_=>null);
  var blocked=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();blocked.Movement.BlockedPos=4;
  views.Create(blocked); Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==0,"zero distance removed too soon");
  views.Advance(0); Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==0,"blocked graph was removed while playing");
  Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Last.Move.BlockedPlaybackPending=false; views.Advance(.01f);
  Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==1,"finished blocked graph not removed");
  var delayed=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();delayed.Movement.BlockedPos=4;delayed.Movement.Info.DelayDestroyTime=1;
  bool rejected=false;try{views.Create(delayed);}catch(NotSupportedException){rejected=true;} Check(rejected,"unverified server delay accepted");
  var normal=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();normal.Movement.TotalMoveDuration=.5f;normal.Movement.Info.DelayDestroyTime=.1f;
  views.Create(normal);views.Advance(.4f);Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==1,"ordinary action ended early");
  views.Advance(.21f);Check(Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Removed==2,"ordinary action did not end");views.Dispose();
  Console.WriteLine("Blocked and ordinary action lifecycle checks passed.");
 }
}
