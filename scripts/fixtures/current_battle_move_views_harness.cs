using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.NativeTerrain.Common;

namespace UnityEngine
{
    public class Object {public static implicit operator bool(Object value)=>value!=null;}
    public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;}
    public class GameObject:Object
    {
        public string name;public readonly Transform transform;readonly Dictionary<Type,object> components=new Dictionary<Type,object>();
        public GameObject(string name=""){this.name=name;transform=new Transform(this);}
        public void Add<T>(T value){components[typeof(T)]=value;if(value is Component component)component.gameObject=this;}
        public T GetComponent<T>()=>components.TryGetValue(typeof(T),out var value)?(T)value:default;
        public T GetComponentInParent<T>(bool includeInactive=false)
        {for(var node=this;node!=null;node=node.transform.parent?.gameObject)if(node.components.TryGetValue(typeof(T),out var value))return (T)value;return default;}
    }
    public class Transform:Object
    {
        public GameObject gameObject;public Transform parent;public Vector3 localPosition,position;
        public Transform(GameObject owner){gameObject=owner;}
        public void SetParent(Transform value,bool worldPositionStays=false){parent=value;}
        public Vector3 TransformPoint(Vector3 point)=>point+localPosition;
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
        public float magnitude=>(float)Math.Sqrt(x*x+y*y+z*z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);}
    public static class Debug {public static void LogError(object value){throw new Exception(value.ToString());}}
    public class TooltipAttribute:Attribute {public TooltipAttribute(string value){}}
    public class HeaderAttribute:Attribute {public HeaderAttribute(string value){}}
    public class SerializeField:Attribute {}
}
namespace Zjcs.Official.World.NativeTerrain.Common
{
    public enum Direction {Up,Down,Left,Right}
    public struct Pos2d
    {
        public int X,Y;public Pos2d(int x,int y){X=x;Y=y;}
        public static bool operator ==(Pos2d a,Pos2d b)=>a.X==b.X&&a.Y==b.Y;
        public static bool operator !=(Pos2d a,Pos2d b)=>!(a==b);
        public override bool Equals(object value)=>value is Pos2d p&&this==p;
        public override int GetHashCode()=>X*31+Y;
    }
}
namespace Zjcs.Official.EntityViews
{
    public struct EcEntityIdentifier {public int EntityId;public EcEntityIdentifier(int id){EntityId=id;}}
    public abstract class ECViewEntityData {public abstract T GetComponent<T>();}
    public abstract class ECViewBase
    {
        protected GameObject mOwner;protected ECViewEntityData mEntity;
        protected T GetComponent<T>()=>mEntity.GetComponent<T>();
        protected virtual void Load(){}protected virtual void Unload(){}
        public void StartForTest(GameObject owner,ECViewEntityData data){mOwner=owner;mEntity=data;Load();}
        public void EndForTest(){Unload();mEntity=null;}
    }
}
namespace Zjcs.Official.EntityLifecycle
{
    public interface IEntityComponentInfoSource {object ComponentInfo {get;}}
    public class Grid
    {
        public bool FlipX;public int PositionResets,RotationResets,ScaleResets;
        public Pos2d GetClientGrid()=>new Pos2d(0,0);
        public Vector3 GridToPosition(Pos2d p)=>new Vector3(p.X,p.Y);
        public void ResetPosition(){PositionResets++;}public void ResetRotation(){RotationResets++;}
        public void ResetScale(){ScaleResets++;}public void SetScaleByFlipX(bool value){FlipX=value;}
    }
    public class Container:UnityEngine.Object {public Transform myEntitySlot;}
    public class MyEntity:Component {public EcEntityIdentifier Id;public ECViewEntityData Entity;public Zjcs.Official.EntityTransformViews.MyGridTransform myGridTransform=new Zjcs.Official.EntityTransformViews.MyGridTransform();public Container myContainer;}
}
namespace Zjcs.Official.EntityTransformViews {public class MyGridTransform:Zjcs.Official.EntityLifecycle.Grid {}}
namespace Zjcs.Official.EntityActions
{
    public interface IMyActionEntityLookup {MyEntity GetEntity(EcEntityIdentifier id);}
    public class ActionComponentData {public EcEntityIdentifier ApplyId;}
}
namespace Zjcs.Official.StoryPlayables
{
    public class MyPlayable:UnityEngine.Object
    {
        public enum PlayableStatus {Play,Stop}
        public PlayableStatus myStatus;public Action Stopping;
        public void Stop(){myStatus=PlayableStatus.Stop;var action=Stopping;Stopping=null;action?.Invoke();}
    }
}
namespace Zjcs.Official.StoryPlayableManager
{
    public interface IMyPlayableManager
    {MyPlayable Play(GameObject prefab,GameObject source,GameObject target,float speed,object unused,Dictionary<string,object> param);}
}
namespace Zjcs.Official.BattleSkillPlayables
{
    public interface IFightGetUsePlayable {void TryGetPlayables(ref HashSet<GameObject> values);}
    internal static class FightMovementViewMath
    {
        public static bool CalcFlipX(Pos2d a,Pos2d b,bool previous)=>a.X==b.X?previous:b.X<a.X;
        public static float CalcRealDistance(Pos2d start,List<Pos2d> path)
        {float total=0;foreach(var p in path){total+=(float)Math.Sqrt((p.X-start.X)*(p.X-start.X)+(p.Y-start.Y)*(p.Y-start.Y));start=p;}return total;}
    }
}

public static class CurrentBattleMoveViewsHarness
{
    sealed class Data:ECViewEntityData
    {
        public FightActionSkillMoveComponent Move;public ActionComponentData Action=new ActionComponentData();
        public override T GetComponent<T>()=>typeof(T)==typeof(FightActionSkillMoveComponent)?(T)(object)Move:
            typeof(T)==typeof(ActionComponentData)?(T)(object)Action:default;
    }
    sealed class Services:IMyPlayableManager,IMyActionEntityLookup
    {
        public MyEntity Role;public readonly List<Vector3> Targets=new List<Vector3>();
        public readonly List<float> Speeds=new List<float>();public readonly List<MyPlayable> Plays=new List<MyPlayable>();
        public readonly List<Dictionary<string,object>> Parameters=new List<Dictionary<string,object>>();
        public MyEntity GetEntity(EcEntityIdentifier id)=>id.EntityId==Role.Id.EntityId?Role:null;
        public MyPlayable Play(GameObject prefab,GameObject source,GameObject target,float speed,object unused,Dictionary<string,object> param)
        {
            Check(source==Role.gameObject,"move source lost its actual action owner");var position=target.transform.position;
            Targets.Add(position);Speeds.Add(speed);Parameters.Add(new Dictionary<string,object>(param));
            var playable=new MyPlayable{Stopping=()=>source.transform.localPosition=position};Plays.Add(playable);return playable;
        }
    }
    static void Check(bool value,string message){if(!value)throw new Exception("Current move view: "+message);}
    static void Field(object instance,string name,object value)=>instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(instance,value);
    public static string Run()
    {
        var roleGo=new GameObject("role");var role=new MyEntity{Id=new EcEntityIdentifier(17),myContainer=new Container{myEntitySlot=new GameObject("coordinates").transform}};roleGo.Add(role);
        var root=new GameObject("11343");var data=new Data{Move=new FightActionSkillMoveComponent{Path=new List<Pos2d>{new Pos2d(1,0),new Pos2d(2,0)},Direction=Direction.Right,
            Info=new FightActionSkillMoveComponentInfo{MoveDuration=.4f}}};data.Action.ApplyId=role.Id;data.Move.InitializeMovement(new Pos2d(0,0));
        var entity=new MyEntity{Entity=data};root.Add(entity);var child=new GameObject("11343_view");child.transform.SetParent(root.transform);
        var services=new Services{Role=role};var view=new ECFightActionSkillMoveView{MyPlayableManager=services,EntityLookup=services,EntitySlot=role.myContainer.myEntitySlot};
        var movePrefab=new GameObject("move");var blockedPrefab=new GameObject("blocked");Field(view,"mPlayablePrefab",movePrefab);Field(view,"mBlockPlayablePrefab",blockedPrefab);
        var uses=new HashSet<GameObject>();view.TryGetPlayables(ref uses);Check(uses.Count==2,"the declared native playables were omitted");
        view.StartForTest(child,data);Check(services.Plays.Count==1&&services.Targets[0].x==1&&Math.Abs(view.RemainingMoveTime-.4f)<.0001f,"the nested view did not start its first original path segment");
        view.Advance(.39f);Check(services.Plays.Count==1,"the first movement timer ended early");
        view.Advance(view.RemainingMoveTime);Check(services.Plays.Count==2&&services.Targets[1].x==2&&services.Plays[0].myStatus==MyPlayable.PlayableStatus.Stop,"the authored boundary did not stop the old segment and play the next");
        Check((Direction)services.Parameters[1]["ParamSource2TargetDirection"]==Direction.Right&&services.Parameters[1]["BlockedAction"]==root,"the move direction or collision receipt root changed");
        view.Advance(.4f);Check(!view.BlockedPlaybackPending&&float.IsPositiveInfinity(view.RemainingMoveTime),"the completed path kept an active timer");
        view.EndForTest();Check(role.myGridTransform.PositionResets==1&&role.myGridTransform.RotationResets==1,"unload did not restore the original grid transform");
        roleGo.transform.localPosition=new Vector3();data.Move.Path=new List<Pos2d>{new Pos2d(0,0)};data.Move.BlockedPos=new Pos2d(1,0);data.Move.InitializeMovement(new Pos2d(0,0));
        view.StartForTest(child,data);view.Advance(.4f);Check(view.BlockedPlaybackPending&&Math.Abs(services.Speeds[services.Speeds.Count-1]-1.25f)<.0001f,"zero-distance collision lost its original half-speed blocked graph");
        services.Plays[services.Plays.Count-1].Stop();Check(!view.BlockedPlaybackPending,"a stopped collision graph kept the action alive");view.EndForTest();
        int count=services.Plays.Count;Field(view,"mPlayablePrefab",null);view.StartForTest(child,data);view.Advance(2f);view.EndForTest();Check(services.Plays.Count==count,"an absent native movement graph was substituted");
        return "Current EC movement parent lookup, action owner, authored segments, timers, collision graph and transform restoration passed.";
    }
}
