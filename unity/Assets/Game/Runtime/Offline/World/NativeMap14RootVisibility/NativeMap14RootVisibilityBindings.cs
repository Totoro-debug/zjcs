using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Zjcs.Official.MapRootFog;
using Zjcs.Official.MapRootVisibility;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap14RootFog;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Offline.World.NativeMap14RootVisibility
{
    // Original Console event invocation lists own the dispatch. Each delegate
    // has its own native registration; remove detaches the last equal callback.
    internal sealed class NativeFogEvent<T> : IDisposable where T : Delegate
    {
        private readonly object owner;
        private readonly EventInfo eventInfo;
        private readonly Func<T, Action<object[]>> convert;
        private readonly List<KeyValuePair<T, Delegate>> registrations = new List<KeyValuePair<T, Delegate>>();
        public NativeFogEvent(object owner, string name, Func<T, Action<object[]>> convert)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.convert = convert;
            eventInfo = owner.GetType().GetEvent(name) ?? throw new MissingMemberException(owner.GetType().FullName, name);
        }
        public void Add(T callback)
        {
            if (callback == null) return;
            var invoke = eventInfo.EventHandlerType.GetMethod("Invoke");
            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var args = Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object))));
            var body = Expression.Invoke(Expression.Constant(convert(callback)), args);
            var native = Expression.Lambda(eventInfo.EventHandlerType, body, parameters).Compile();
            eventInfo.AddEventHandler(owner, native);
            registrations.Add(new KeyValuePair<T, Delegate>(callback, native));
        }
        public void Remove(T callback)
        {
            for (int i = registrations.Count - 1; i >= 0; i--)
                if (Equals(registrations[i].Key, callback))
                { eventInfo.RemoveEventHandler(owner, registrations[i].Value); registrations.RemoveAt(i); return; }
        }
        public void Dispose()
        { foreach (var pair in registrations) eventInfo.RemoveEventHandler(owner, pair.Value); registrations.Clear(); }
    }

    public sealed class NativeMap14RootVisibilityBindings
    {
        private readonly NativeMap14SharedAssemblies shared;
        public NativeMap14RootVisibilityBindings(NativeMap14SharedAssemblies shared)
        { this.shared = shared ?? throw new ArgumentNullException(nameof(shared)); }
        public void Bind(NativeMap14SharedViewEntity view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (view.GetComponent<FogVisibilityAgentData>() != null) return;
            var native = view.Original.GetType().GetMethod("GetComponent", new[] { typeof(Type) })
                .Invoke(view.Original, new object[] { shared.CommonType("GridVisibilityAgentComponent") });
            if (native == null) return;
            var inputs = new NativeMap14RootFogInputs(shared, view.Original);
            if (!ReferenceEquals(inputs.Visibility.Original, native)) throw new InvalidOperationException("Original visibility identity differs.");
            view.BindAdditionalComponent<FogVisibilityAgentData>(new VisibilityInput(native, inputs.Ground));
        }
        private static object Get(object owner, string name) => NativeMap14SharedPrefabs.Get(owner, name);
        private static Pos2d Position(object value) => new Pos2d((int)Get(value, "X"), (int)Get(value, "Y"));
        private sealed class VisibilityInput : FogVisibilityAgentData
        {
            public override object Original { get; }
            public override object ComponentInfo => Get(Original, "_Info");
            private readonly FogGroundData ground;
            private readonly MultiCellInput handle;
            private readonly NativeFogEvent<Action<Pos2d, bool, bool, bool>> updates;
            private readonly NativeFogEvent<Action<Pos2d, bool>> unlocks;
            public override FogMultiCellHandle Handle => handle;
            public VisibilityInput(object original, FogGroundData ground)
            {
                Original = original; this.ground = ground;
                updates = new NativeFogEvent<Action<Pos2d, bool, bool, bool>>(original, "OnPosVisibilityUpdate", cb => a => cb(Position(a[0]), (bool)a[1], (bool)a[2], (bool)a[3]));
                unlocks = new NativeFogEvent<Action<Pos2d, bool>>(original, "OnOneCellUnlock", cb => a => cb(Position(a[0]), (bool)a[1]));
                handle = new MultiCellInput(Get(original, "Handle"));
            }
            public override T GetComponent<T>() => ground as T ?? throw new InvalidOperationException("Unbound original visibility dependency: " + typeof(T));
            public override bool GetPosVisibility(int x,int y) => (bool)Original.GetType().GetMethod("GetPosVisibility",new[]{typeof(int),typeof(int)}).Invoke(Original,new object[]{x,y});
            public override event Action<Pos2d, bool, bool, bool> OnPosVisibilityUpdate { add => updates.Add(value); remove => updates.Remove(value); }
            public override event Action<Pos2d, bool> OnOneCellUnlock { add => unlocks.Add(value); remove => unlocks.Remove(value); }
            public override void Dispose() { updates.Dispose(); unlocks.Dispose(); handle.Dispose(); }
        }
        private sealed class MultiCellInput : FogMultiCellHandle, IDisposable
        {
            private readonly NativeFogEvent<Action<HashSet<Pos2d>, bool>> events;
            public MultiCellInput(object original)
            {
                events = new NativeFogEvent<Action<HashSet<Pos2d>, bool>>(original, "OnMultiCellsUnlock", cb => args =>
                {
                    var positions = new HashSet<Pos2d>();
                    foreach (var value in (IEnumerable)args[0]) positions.Add(Position(value));
                    cb(positions, (bool)args[1]);
                });
            }
            public override event Action<HashSet<Pos2d>, bool> OnMultiCellsUnlock { add => events.Add(value); remove => events.Remove(value); }
            public void Dispose() => events.Dispose();
        }
    }
}
