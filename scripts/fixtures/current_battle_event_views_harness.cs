using System;
using System.Collections.Generic;
using System.Reflection;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using UnityEngine;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value)=>value!=null; }
    public class Component:Object { public GameObject gameObject; }
    public class GameObject:Object
    {
        public string name;public GameObject Parent;readonly Dictionary<Type,object> components=new Dictionary<Type,object>();
        public T GetComponent<T>()=>components.TryGetValue(typeof(T),out var value)?(T)value:default;
        public T GetComponentInParent<T>()
        {for(var node=this;node!=null;node=node.Parent)if(node.components.TryGetValue(typeof(T),out var value))return (T)value;return default;}
        public void Add<T>(T value){components[typeof(T)]=value;if(value is Component component)component.gameObject=this;}
    }
    public sealed class SerializeField:Attribute { }
    public sealed class SerializeReference:Attribute { }
    public static class Debug
    {
        public static readonly List<string> Errors=new List<string>(),Warnings=new List<string>();
        public static void LogError(object value)=>Errors.Add(value.ToString());
        public static void LogWarning(object value)=>Warnings.Add(value.ToString());
        public static void LogException(Exception value)=>Errors.Add(value.Message);
    }
}
namespace Zjcs.Official.EntityViews
{
    public struct EcEntityIdentifier
    {
        public int Kind,EntityId;
        public EcEntityIdentifier(int kind,int id){Kind=kind;EntityId=id;}
    }
    public abstract class ECViewEntityData { public abstract T GetComponent<T>(); }
    public abstract class ECViewBase
    {
        protected GameObject mOwner;protected ECViewEntityData mEntity;protected IServiceProvider mServiceProvider;
        protected virtual void Load(){}protected virtual void Unload(){}
        protected T GetComponent<T>()=>mEntity.GetComponent<T>();protected T GetView<T>() where T:ECViewBase=>default;
        public void BindForTest(GameObject owner,ECViewEntityData entity,IServiceProvider provider)
        {mOwner=owner;mEntity=entity;mServiceProvider=provider;}
        public void BeginForTest()=>Load();public void EndForTest()=>Unload();
    }
}
namespace Zjcs.Official.EntityLifecycle
{
    public class MyEntity:Component {public EcEntityIdentifier Id;}
}
namespace Zjcs.Official.EntityActions
{
    public interface IMyActionEntityLookup { MyEntity GetEntity(EcEntityIdentifier id); }
}
namespace Zjcs.Official.StoryPlayables { }
namespace Zjcs.Official.StoryPlayableManager
{
    public interface IMyPlayableManager {object Play(GameObject prefab,GameObject source,GameObject target);}
}
namespace Zjcs.Official.BattleSkillPlayables
{
    public interface IFightGetUsePlayable {void TryGetPlayables(ref HashSet<GameObject> values);}
}
namespace Zjcs.Official.BattleStatusPlayables
{
    public interface IFightStatusPlayableState {event Action<IFightStatusPlayableState> OnInvoked;}
    public class MyFightStatus:Component {public StatusContext Context;}
    public class StatusContext {public GameObject Owner;public UnityEngine.Object Agent;}
    internal sealed class CurrentStatusViewSubscription:IDisposable
    {
        Action unsubscribe;internal CurrentStatusViewSubscription(Action action)=>unsubscribe=action;
        public void Dispose(){var action=unsubscribe;unsubscribe=null;action?.Invoke();}
    }
}

public static class CurrentBattleEventViewsHarness
{
    sealed class State:IFightStatusPlayableState
    {
        public event Action<IFightStatusPlayableState> OnInvoked;
        public int Count=>OnInvoked?.GetInvocationList().Length??0;
        public void Invoke()=>OnInvoked?.Invoke(this);
    }
    sealed class Data:ECViewEntityData
    {
        public State State;
        public override T GetComponent<T>()=>typeof(T)==typeof(IFightStatusPlayableState)?(T)(object)State:default;
    }
    sealed class Services:IServiceProvider,IMyActionEntityLookup,IMyPlayableManager
    {
        public MyEntity Entity;public int Plays;public readonly List<string> Order=new List<string>();
        public object GetService(Type type)=>type==typeof(IMyActionEntityLookup)||type==typeof(IMyPlayableManager)?this:null;
        public MyEntity GetEntity(EcEntityIdentifier id)=>Entity!=null&&Entity.Id.Equals(id)?Entity:null;
        public object Play(GameObject prefab,GameObject source,GameObject target)
        {Check(source==Entity.gameObject&&target==source,"the handler changed the owner's source/target");Plays++;Order.Add("play");return null;}
    }
    sealed class Observer:FightViewHandlerBase<PlayPlayableWithSourceAndTargetData>
    {
        public Action Action;public int Released;public StatusInvokedArgs Seen;
        public MyEntity OwnerDuring,StatusDuring;
        public override void Handle(FightViewEventArgs args)
        {Seen=(StatusInvokedArgs)args;OwnerDuring=Seen.StatusOwner;StatusDuring=Seen.Status;base.Handle(args);}
        protected override void HandleData(PlayPlayableWithSourceAndTargetData data)=>Action?.Invoke();
        protected override void OnRelease()=>Released++;
    }
    sealed class WrongConverter:FightViewArgsConverter<int>
    {public override bool TryConvert(FightViewEventArgs args,out int data){data=0;return true;}}
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException("Current fight events: "+message);}
    static void Field(object value,string name,object data)
    {
        for(Type type=value.GetType();type!=null;type=type.BaseType)
        {var field=type.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);if(field!=null){field.SetValue(value,data);return;}}
        throw new InvalidOperationException("Missing serialized field "+name);
    }
    public static string Run()
    {
        var owner=new GameObject{name="owner"};var role=new MyEntity{Id=new EcEntityIdentifier(1,17)};owner.Add(role);
        var root=new GameObject{name="status"};root.Add(new MyEntity{Id=new EcEntityIdentifier(4,29)});
        var view=new GameObject{name="status_view",Parent=new GameObject{name="container",Parent=root}};
        var context=new StatusContext{Owner=owner,Agent=new UnityEngine.Object()};root.Add(new MyFightStatus{Context=context});
        var state=new State();var data=new Data{State=state};var services=new Services{Entity=role};
        var converter=new StatusInvokedArgsToPlayPlayableWithSourceAndTargetDataConverter();
        Check(!converter.TryConvert(new StatusInvokedArgs(),out var absent),"a missing owner converted successfully");
        var args=new StatusInvokedArgs{StatusOwner=role,Status=root.GetComponent<MyEntity>()};
        Check(converter.TryConvert(args,out var converted)&&converted.SourceId.Equals(role.Id)&&converted.TargetId.Equals(role.Id),
            "the converter used the status identity instead of its owner");
        var before=new Observer{Action=()=>services.Order.Add("before")};Field(before,"mConverter",converter);
        var after=new Observer{Action=()=>services.Order.Add("after")};Field(after,"mConverter",converter);
        var handler=new PlayPlayableWithSourceAndTargetHandler();Field(handler,"mConverter",converter);
        var prefab=new GameObject{name="original playable"};Field(handler,"mPlayable",prefab);
        var source=new ECStatusInvokedEventSourceView();Field(source,"mHandlers",new List<FightViewHandler>{before,handler,after});
        source.BindForTest(view,data,services);source.BeginForTest();
        Check(state.Count==1,"the status source did not subscribe exactly once");state.Invoke();
        Check(services.Plays==1&&string.Join(",",services.Order)=="before,play,after",
            "the original ordered event chain did not dispatch once");
        Check(before.OwnerDuring==role&&before.StatusDuring==root.GetComponent<MyEntity>(),
            "a nested view failed to resolve the status shell and entity on its parent");
        Check(before.Seen.StatusOwner==null&&before.Seen.Status==null,"shared event arguments were not reset");
        HashSet<GameObject> playables=new HashSet<GameObject>();source.TryGetPlayables(ref playables);
        Check(playables.Count==1&&playables.Contains(prefab),"nested handler playables were omitted");
        context.Agent=null;state.Invoke();Check(services.Plays==1,"an absent status agent dispatched an event");context.Agent=new UnityEngine.Object();
        bool nested=false;before.Action=()=>{if(!nested){nested=true;state.Invoke();}};
        int errors=Debug.Errors.Count;state.Invoke();
        Check(services.Plays==2&&Debug.Errors.Count==errors+1,"re-entrant dispatch was not dropped");
        before.Action=()=>throw new InvalidOperationException("expected handler exception");errors=Debug.Errors.Count;state.Invoke();
        Check(services.Plays==3&&Debug.Errors.Count==errors+1&&after.Seen.StatusOwner==null,
            "a handler exception stopped later handlers or leaked shared arguments");
        source.EndForTest();Check(state.Count==0&&before.Released==1&&after.Released==1,"unload leaked subscriptions or handlers");
        state.Invoke();Check(services.Plays==3,"an unloaded source still dispatched");
        var invalid=new PlayPlayableWithSourceAndTargetHandler();Field(invalid,"mConverter",new WrongConverter());Field(invalid,"mPlayable",prefab);
        var invalidSource=new ECStatusInvokedEventSourceView();Field(invalidSource,"mHandlers",new List<FightViewHandler>{invalid});
        invalidSource.BindForTest(root,data,services);int warnings=Debug.Warnings.Count;invalidSource.BeginForTest();state.Invoke();invalidSource.EndForTest();
        Check(services.Plays==3&&Debug.Warnings.Count==warnings+1,"a mismatched converter was executed");
        var missing=new ECStatusInvokedEventSourceView();missing.BindForTest(root,new Data(),services);errors=Debug.Errors.Count;
        missing.BeginForTest();missing.EndForTest();Check(Debug.Errors.Count==errors+1&&state.Count==0,"a failed bind left an active source");
        return "Current fight-event owner conversion, ordered handlers, re-entry, errors, pooled arguments and unsubscribe passed.";
    }
}
