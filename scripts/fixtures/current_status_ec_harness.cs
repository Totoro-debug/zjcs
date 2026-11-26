using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;

namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value != null; }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string value) { } }
    public class SerializeField : Attribute { }
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>(bool inactive) => gameObject.GetComponent<T>();
    }
    public class Transform : Component { }
    public class GameObject : Object
    {
        public string Name;
        public Transform transform;
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();
        public GameObject(string name = "") { Name = name; transform = new Transform { gameObject = this }; }
        public T GetComponent<T>() => components.TryGetValue(typeof(T), out var value) ? (T)value : default;
        public T Add<T>(T value) where T : Component { value.gameObject = this; components[typeof(T)] = value; return value; }
    }
    public static class Debug { public static void LogError(object value) => throw new Exception(value.ToString()); }
}
namespace Zjcs.Official.EntityViews
{
    public abstract class ECViewEntityData { public abstract T GetComponent<T>(); }
    public abstract class ECViewBase
    {
        protected GameObject mOwner;
        protected ECViewEntityData mEntity;
        protected T GetComponent<T>() => mEntity.GetComponent<T>();
        protected virtual void Load() { }
        protected virtual void Unload() { }
        public void TestLoad(GameObject owner, ECViewEntityData entity) { mOwner = owner; mEntity = entity; Load(); }
        public void TestUnload() { Unload(); mEntity = null; }
    }
}
namespace Zjcs.Official.EntityLifecycle
{
    public class MyEntity : Component { public ECViewEntityData Entity; }
}
namespace Zjcs.Official.StoryAttachment { }
namespace Zjcs.Official.StoryPlayables
{
    public class MyPlayable : UnityEngine.Object
    {
        public bool Stopped;
        public event Action<MyPlayable> OnStop;
        public void Stop() { Stopped = true; OnStop?.Invoke(this); }
    }
}
namespace Zjcs.Official.StoryPlayableManager
{
    public interface IMyPlayableManager
    {
        MyPlayable Play(GameObject graph, GameObject source, GameObject target, float speed = 1,
            GameObject parent = null, Dictionary<string, object> param = null);
    }
}
namespace Zjcs.Official.BattleSkillPlayables
{
    public interface IFightGetUsePlayable { void TryGetPlayables(ref HashSet<GameObject> playables); }
}
namespace Zjcs.Official.BattleStatusPlayables
{
    public class MyFightStatus : Component
    {
        public IFightStatusPlayableContext Context;
        public IFightStatusPlayableState Component => Context.State;
    }
    public class MyFightStatusAgent : Component
    {
        public HashSet<MyFightStatus> MyStatuses = new HashSet<MyFightStatus>();
        public Dictionary<int, MyFightStatus> MyDisplayFightStatusEachShareStatus = new Dictionary<int, MyFightStatus>();
        public Dictionary<int, int> StatusToShareStack = new Dictionary<int, int>();
    }
    public static class CurrentStatusViewLookup
    {
        public static Dictionary<MyFightStatus, ECFightStatusPlayableView> Views = new Dictionary<MyFightStatus, ECFightStatusPlayableView>();
        public static ECFightStatusPlayableView Get(MyFightStatus status) => Views.TryGetValue(status, out var view) ? view : null;
    }
    internal sealed class CurrentStatusViewSubscription : IDisposable
    {
        private Action action;
        public CurrentStatusViewSubscription(Action value) => action = value;
        public void Dispose() { var value = action; action = null; value?.Invoke(); }
    }
}

public static class CurrentStatusEcHarness
{
    private sealed class State : IFightStatusPlayableState
    {
        public int StackedCount { get; set; } = 1;
        public bool CanStackCount { get; set; }
        public bool Forbidden { get; set; }
        public event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
        public event Action<IFightStatusPlayableState> OnInvoked;
        public event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
        public void Stack(int count) { int old = StackedCount; StackedCount = count; OnStatusStackUpdate?.Invoke(this, old, count); }
        public void Forbid(bool value) { bool old = Forbidden; Forbidden = value; OnForbiddenChanged?.Invoke(this, old); }
    }
    private sealed class Context : IFightStatusPlayableContext, ICurrentFightStatusPlayableContext
    {
        public IFightStatusPlayableState State { get; set; }
        public GameObject Owner { get; set; }
        public GameObject Creator => Owner;
        public GameObject Master { get; set; }
        public MyFightStatusAgent Agent { get; set; }
        public int? ShareId { get; set; }
        public bool HasIcon => false;
        public ICurrentFightStatusPlayableView CurrentPlayableView { get; set; }
    }
    private sealed class Data : ECViewEntityData
    {
        public State State;
        public override T GetComponent<T>() => (T)(object)State;
    }
    private sealed class Played
    {
        public GameObject Graph;
        public MyPlayable Handle;
    }
    private sealed class Manager : IMyPlayableManager
    {
        public readonly List<Played> Calls = new List<Played>();
        public Context Context;
        public MyFightStatus Shell;
        public MyPlayable Play(GameObject graph, GameObject source, GameObject target, float speed = 1,
            GameObject parent = null, Dictionary<string, object> param = null)
        {
            Need(source == (graph.Name == "master" ? Context.Master : Context.Owner) && target == Context.Owner
                && speed == 1 && parent == null, "Original status graph source, target and speed");
            if (param != null) Need(ReferenceEquals(param["Status"], Shell), "Original Status parameter identity");
            var handle = new MyPlayable(); Calls.Add(new Played { Graph = graph, Handle = handle }); return handle;
        }
    }
    private static void Need(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Field(object value, string name, object data) => value.GetType().GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(value, data);
    private sealed class Lease
    {
        public ECFightStatusPlayableView View;
        public MyFightStatus Shell;
        public GameObject Root;
        public State State;
        public Data Data;
        public Context Context;
        public Manager Manager;
        public void Load() => View.TestLoad(Root, Data);
    }
    private static Lease Make(State state, bool master, int? share = null, MyFightStatusAgent sharedAgent = null)
    {
        var owner = sharedAgent?.gameObject ?? new GameObject("owner");
        if (owner.GetComponent<MyEntity>() == null) owner.Add(new MyEntity());
        var agent = sharedAgent ?? owner.Add(new MyFightStatusAgent());
        var root = new GameObject("status"); var data = new Data { State = state };
        root.Add(new MyEntity { Entity = data });
        var context = new Context { State = state, Owner = owner, Master = master ? new GameObject("master") : null,
            Agent = agent, ShareId = share };
        var shell = root.Add(new MyFightStatus { Context = context }); agent.MyStatuses.Add(shell);
        var manager = new Manager { Context = context, Shell = shell };
        var view = new ECFightStatusPlayableView { MyPlayableManager = manager }; context.CurrentPlayableView = view;
        CurrentStatusViewLookup.Views.Add(shell, view);
        return new Lease { View = view, Shell = shell, Root = root, State = state, Data = data, Context = context, Manager = manager };
    }
    public static void Run()
    {
        int cases = 0;
        for (int mask = 0; mask < 16; mask++) for (int stack = 0; stack < 2; stack++)
        for (int forbidden = 0; forbidden < 2; forbidden++) for (int master = 0; master < 2; master++)
        {
            var lease = Make(new State { CanStackCount = stack != 0, Forbidden = forbidden != 0 }, master != 0);
            var fields = new[] { "mPlayablePrefab", "mStackedPlayable", "mStackPlayable", "mMasterCastPlayable" };
            var graphs = new[] { new GameObject("start"), new GameObject("stacked"), new GameObject("stack"), new GameObject("master") };
            for (int i = 0; i < fields.Length; i++) Field(lease.View, fields[i], (mask & (1 << i)) != 0 ? graphs[i] : null);
            lease.Load();
            var expected = graphs.Where((value, index) => (mask & (1 << index)) != 0 && (index != 1 || stack != 0) && (index != 3 || master != 0)).ToArray();
            Need(lease.Manager.Calls.Select(call => call.Graph).SequenceEqual(forbidden != 0 ? new GameObject[0] : expected), "Original status load branches and graph order");
            if (forbidden == 0) { int count = lease.Manager.Calls.Count; lease.State.Stack(2); Need(lease.Manager.Calls.Count == count, "Existing stacked handle is retained"); }
            lease.State.Forbid(true);
            Need(lease.Manager.Calls.All(call => call.Handle.Stopped), "Forbidding stops every active original handle");
            int previous = lease.Manager.Calls.Count; lease.State.Forbid(false);
            Need(lease.Manager.Calls.Skip(previous).Select(call => call.Graph).SequenceEqual(expected), "Unforbidding starts the original ordered graphs once");
            lease.View.TestUnload();
            Need(lease.Manager.Calls.All(call => call.Handle.Stopped), "Unload stops all original handles");
            previous = lease.Manager.Calls.Count; lease.State.Forbid(true); lease.State.Forbid(false); lease.State.Stack(3);
            Need(lease.Manager.Calls.Count == previous, "Unload detaches stack and forbidden notifications");
            cases++;
        }
        var quirk = Make(new State { CanStackCount = true, Forbidden = true }, false);
        Field(quirk.View, "mStackedPlayable", new GameObject("stacked")); quirk.Load(); quirk.State.Stack(2);
        Need(quirk.Manager.Calls.Count == 1, "Preserve the current official unguarded stack notification branch"); quirk.View.TestUnload();
        var first = Make(new State { CanStackCount = true, StackedCount = 2 }, false, 10);
        first.Context.Agent.StatusToShareStack[10] = 5;
        var map = new List<StackCountPlayableMap> { new StackCountPlayableMap { StackCount = 1, MaxStackCount = 10,
            BeginPlayable = new GameObject("begin"), PlayablePrefab = new GameObject("stay"), EndPlayable = new GameObject("end") } };
        Field(first.View, "mStackPlayableMap", map); first.Load();
        Need(first.Context.Agent.MyDisplayFightStatusEachShareStatus[10] == first.Shell && first.Manager.Calls.Count == 2, "Shared total selects first display segment");
        var second = Make(new State { CanStackCount = true, StackedCount = 3 }, false, 10, first.Context.Agent);
        Field(second.View, "mStackPlayableMap", map); second.Load();
        Need(first.Context.Agent.MyDisplayFightStatusEachShareStatus[10] == second.Shell
            && first.Manager.Calls.Last().Graph.Name == "end" && second.Manager.Calls.Count == 2, "Larger status replaces display and stops old segment");
        first.Context.Agent.StatusToShareStack[10] = 6; first.State.Stack(3);
        Need(first.Context.Agent.MyDisplayFightStatusEachShareStatus[10] == first.Shell, "Equal stacks retain the current notifying shell as in official iteration");
        first.Context.Agent.StatusToShareStack[10] = 3; first.State.Stack(0); second.State.Stack(0);
        Need(!first.Context.Agent.MyDisplayFightStatusEachShareStatus.ContainsKey(10), "Zero stacks stop and clear the shared display");
        first.View.TestUnload(); second.View.TestUnload();
        Console.WriteLine("Current official status EC view: " + cases + " lifecycle/field combinations, symmetric cleanup, shared display replacement/ties/zero and original stack branch passed.");
    }
}
