using System;using System.Collections.Generic;using System.Linq;
namespace UnityEngine {
 public class Object {public string name;public static implicit operator bool(Object x)=>x!=null;public static void Destroy(Object x){} }
 public class Component:Object {public GameObject gameObject;}
 public class Transform:Component {}
 public enum WeightedMode {None,In,Out,Both}
 public struct Keyframe {public float time,value,inTangent,outTangent;public WeightedMode weightedMode;public Keyframe(float t,float v,float i,float o){time=t;value=v;inTangent=i;outTangent=o;weightedMode=WeightedMode.None;}}
 public class AnimationCurve {public Keyframe[] keys;public AnimationCurve(params Keyframe[] v){keys=v;}public float Evaluate(float t){for(int i=1;i<keys.Length;i++)if(t<=keys[i].time){var l=keys[i-1];var r=keys[i];float d=r.time-l.time,u=(t-l.time)/d;return (2*u*u*u-3*u*u+1)*l.value+(u*u*u-2*u*u+u)*d*l.outTangent+(-2*u*u*u+3*u*u)*r.value+(u*u*u-u*u)*d*r.inTangent;}return keys[keys.Length-1].value;}}
 public class GameObject:Object {public object BoundComponent;public Component[] Children=new Component[0];public T GetComponent<T>() where T:class=>BoundComponent as T;public T GetComponentInChildren<T>(bool includeInactive) where T:class=>BoundComponent as T??Children.OfType<T>().FirstOrDefault();public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class=>Children.OfType<T>().ToArray();public T[] GetComponents<T>() where T:class=>typeof(T)==typeof(Zjcs.Official.EntityActions.MyAction)?new[]{new Zjcs.Official.EntityActions.MyAction() as T}:new[]{new Zjcs.Official.BattleSkillPlayables.MyFightActionSkillMove{gameObject=this} as T};public T AddComponent<T>() where T:Component,new()=>new T{gameObject=this};}
 public static class Mathf {public static float Min(params float[] v){float r=float.PositiveInfinity;foreach(var x in v)r=Math.Min(r,x);return r;}public static float Max(float a,float b)=>Math.Max(a,b);}
}
namespace Zjcs.Official.StoryPlayables {
 using UnityEngine;public struct EcEntityIdentifier {public int Id;public EcEntityIdentifier(int i){Id=i;}}
 public class GAction<T> {private event Action<T> handlers;public IDisposable Subscribe(Action<T> h){handlers+=h;return new Lease(()=>handlers-=h);}public void Invoke(T v)=>handlers?.Invoke(v);private class Lease:IDisposable{private Action act;public Lease(Action a){act=a;}public void Dispose(){act?.Invoke();act=null;}}}
 public class MyPlayable:Component {public Dictionary<string,object> Params=new Dictionary<string,object>();public object TryGetParam(string key)=>Params.TryGetValue(key,out var x)?x:null;public GAction<MyPlayable> OnStop=new GAction<MyPlayable>();}
 public class MyPlayableTrigger:Component {public virtual void Play(MyPlayable p){}}
 public class MyPlayableTriggerTimeCfg {public float myTime;public MyPlayableTrigger myTrigger;}
}
namespace Zjcs.Official.BattleTrackPlayables {
 using UnityEngine;using Zjcs.Official.StoryPlayables;
 public class MyCurveTrack:Object {public float myDuration;public AnimationCurve myCurveY;}
 public class MyPlayableTrack:MyPlayable {public MyCurveTrack myTrack;public List<MyPlayableTriggerTimeCfg> myTrackTriggerList=new List<MyPlayableTriggerTimeCfg>();public bool myTrackTriggerReliable=true;public void Progress(float p){foreach(var c in myTrackTriggerList.ToArray())if(p>=c.myTime)c.myTrigger.Play(this);}public void Stop(){if(myTrackTriggerReliable)foreach(var c in myTrackTriggerList.ToArray())c.myTrigger.Play(this);OnStop.Invoke(this);}}
}
namespace Zjcs.Official.EntityLifecycle {using Zjcs.Official.BattleSkillPlayables;using UnityEngine;public class MyContainer{}public class MyEntity:Component {public MyFightActionSkillMove Move;public MyEntity(){gameObject=new GameObject();Move=new MyFightActionSkillMove{gameObject=gameObject};gameObject.BoundComponent=Move;}public T GetComponent<T>() where T:class=>Move as T;public void MyStart(){Move.Begin();}}}
namespace Zjcs.Official.EntityActions {using Zjcs.Official.StoryPlayables;using Zjcs.Official.EntityLifecycle;public interface IMyActionEntityLookup {MyEntity GetEntity(EcEntityIdentifier id);}public class MyAction {public IMyActionEntityLookup EntityLookup;public object mMyPlayableManager;public bool myEffect,myEndEffect,LoopEffectPlayable;public float Duration=-1;}}
namespace Zjcs.Official.BattleSkillPlayables {using UnityEngine;public class MyFightActionSkillMove:Component,IFightActionSkillMoveView {public object mMyPlayableManager;public Transform EntitySlot;public GameObject BlockPlayablePrefab;GameObject IFightActionSkillMoveView.BlockPlayablePrefab=>BlockPlayablePrefab;public IFightActionSkillMoveView CurrentView;public bool ManualTiming;public bool BlockedPlaybackPending;bool IFightActionSkillMoveView.BlockedPlaybackPending=>BlockedPlaybackPending;public float RemainingMoveTime=float.PositiveInfinity;float IFightActionSkillMoveView.RemainingMoveTime=>RemainingMoveTime;public void Begin(){BlockedPlaybackPending=true;RemainingMoveTime=0;}public void Advance(float d){RemainingMoveTime=float.PositiveInfinity;}}public class ECFightActionSkillMoveView:IFightActionSkillMoveView {public object MyPlayableManager;public Zjcs.Official.EntityActions.IMyActionEntityLookup EntityLookup;public Transform EntitySlot;public GameObject BlockPlayablePrefab=>null;public bool BlockedPlaybackPending=>false;public float RemainingMoveTime=>float.PositiveInfinity;public void Advance(float d){}}}
namespace Zjcs.Official.EntityViews {public interface IECViewInjection {void Inject(object value);}public class ECViewDriver:UnityEngine.Component {public object View;public T GetView<T>() where T:class=>View as T;}}
namespace Zjcs.Official.StoryPlayableManager {public interface IMyPlayableManager {}}
namespace Zjcs.Offline.BattleNativePlayables {using UnityEngine;using Zjcs.Official.StoryPlayables;public class OfflineBattleNativePlayableScope:Zjcs.Official.StoryPlayableManager.IMyPlayableManager {public event Action<GameObject,MyPlayable> NativePlayableStarted;public void Dispatch(Action a)=>a();public void Start(GameObject p,MyPlayable v)=>NativePlayableStarted?.Invoke(p,v);}}
namespace Zjcs.Presentation.Combat {
 using UnityEngine;using Zjcs.Official.StoryPlayables;using Zjcs.Official.EntityLifecycle;using Zjcs.Offline.BattleNativePlayables;
 public enum OfficialBattleNativeEntityKind {MoveAction}
 public static class OfficialBattleCurrentViewBinding {public static void Prepare(GameObject root,object services){}}
 public class OfficialBattleMoveActionData {public int ClassId;public EcEntityIdentifier Id;public MovementData Movement=new MovementData();public class MovementData {public int? BlockedPos;public float TotalMoveDuration;public InfoData Info=new InfoData();}public class InfoData {public float DelayDestroyTime;}}
 public class OfficialBattleNativeEntityLoader {public static readonly List<MyEntity> Entities=new List<MyEntity>();public static int Removed;public static MyEntity Last;public static GameObject Prefab;public Transform EntitySlot=new Transform();public OfficialBattleNativeEntityLoader(Transform p,object c,OfflineBattleNativePlayableScope s,OfficialBattleNativeEntityKind k,Action<GameObject> init){init(new GameObject());}public GameObject GetPrefab(int id)=>Prefab;public MyEntity Create(OfficialBattleMoveActionData d){var x=new MyEntity();Entities.Add(x);Last=x;return x;}public void Remove(EcEntityIdentifier id){Removed++;}public void Dispose(){}public bool ContainsClass(int i)=>true;public MyEntity GetEntity(EcEntityIdentifier id)=>null;}
}
public static class CollisionContactHarness {
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static Zjcs.Official.BattleTrackPlayables.MyPlayableTrack Track(UnityEngine.GameObject action){var track=new Zjcs.Official.BattleTrackPlayables.MyPlayableTrack{name="move",gameObject=new UnityEngine.GameObject()};track.Params["BlockedAction"]=action;track.myTrack=new Zjcs.Official.BattleTrackPlayables.MyCurveTrack{name="playable_move_blocked_track",myDuration=1,myCurveY=new UnityEngine.AnimationCurve(new UnityEngine.Keyframe(0,0,2.4876611f,2.4876611f),new UnityEngine.Keyframe(.4036439f,1.0041293f,3.1199872f,1.5297953f))};return track;}
 public static void Run(){
  var scope=new Zjcs.Offline.BattleNativePlayables.OfflineBattleNativePlayableScope();var views=new Zjcs.Presentation.Combat.OfficialBattleMoveActionPlayback(new UnityEngine.Transform(),null,scope,_=>null);
  var d=new Zjcs.Presentation.Combat.OfficialBattleMoveActionData();d.Movement.BlockedPos=1;int first=0,second=0;
  var graph=new UnityEngine.GameObject{name="playable_move_blocked",Children=new UnityEngine.Component[]{Track(new UnityEngine.GameObject())}};
  Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Prefab=new UnityEngine.GameObject{BoundComponent=new Zjcs.Official.BattleSkillPlayables.MyFightActionSkillMove{BlockPlayablePrefab=graph}};
  graph.name="unverified";bool rejected=false;try{views.Create(d,()=>first++);}catch(NotSupportedException){rejected=true;}Check(rejected && Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Entities.Count==0,"unknown track was not rejected before creating an action");graph.name="playable_move_blocked";
  views.Create(d,()=>first++);var a=Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Entities.Last().Move.gameObject;
  views.Create(d,()=>second++);var b=Zjcs.Presentation.Combat.OfficialBattleNativeEntityLoader.Entities.Last().Move.gameObject;
  var prefab=new UnityEngine.GameObject{name="playable_move_blocked"};var one=Track(a);var two=Track(b);scope.Start(prefab,one);scope.Start(prefab,two);
  Check(Zjcs.Official.BattleSkillPlayables.BattleBlockedTrackContact.TryContactProgress(one.myTrack,out float contact) && Math.Abs(contact-.4023186f)<.00001f,"incorrect original collision curve crossing");
  Check(one.myTrack.myCurveY.Evaluate(contact)>=1f && one.myTrack.myCurveY.Evaluate(contact-.0001f)<1f,"collision did not use the first contact with the endpoint");
  var unsupported=Track(a).myTrack;var keys=unsupported.myCurveY.keys;keys[1].weightedMode=UnityEngine.WeightedMode.In;unsupported.myCurveY=new UnityEngine.AnimationCurve(keys);
  Check(!Zjcs.Official.BattleSkillPlayables.BattleBlockedTrackContact.TryContactProgress(unsupported,out _),"unverified weighted curve was accepted");
  keys[1].weightedMode=UnityEngine.WeightedMode.None;keys[1].inTangent=-100;unsupported.myCurveY=new UnityEngine.AnimationCurve(keys);
  Check(!Zjcs.Official.BattleSkillPlayables.BattleBlockedTrackContact.TryContactProgress(unsupported,out _),"nonmonotonic curve was accepted");
  Check(one.myTrackTriggerList.Count==1 && two.myTrackTriggerList.Count==1,"missing original track trigger");
  one.Progress(.2f);Check(first==0 && second==0,"contact fired before arrival");
  one.Progress(.41f);one.Progress(.5f);Check(first==1 && second==0,"contact routed incorrectly or repeated");
  two.Stop();Check(second==0 && two.myTrackTriggerList.Count==0 && two.myTrackTriggerReliable,"interruption fired collision or failed cleanup");
  scope.Start(prefab,two);two.Progress(.41f);Check(second==1,"reused track did not deliver the subsequent contact");two.Stop();
  one.Stop();Check(one.myTrackTriggerList.Count==0 && one.myTrackTriggerReliable,"completed trigger did not detach");
  scope.Start(prefab,one);Check(one.myTrackTriggerList.Count==0,"completed action attached twice on reuse");views.Dispose();
  Console.WriteLine("Collision trigger routing, contact, interruption and reuse checks passed.");
 }
}
