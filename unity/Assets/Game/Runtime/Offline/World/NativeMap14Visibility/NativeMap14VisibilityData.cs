using System;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Offline.World.NativeMap14Visibility
{
    // Read/write/event projection over the existing original shared component.
    // Flag mutation and original receiver dispatch stay in unchanged Console IL.
    public sealed class NativeMap14VisibilityData : Map13VisibilityData, IDisposable
    {
        private readonly NativeMap14SharedAssemblies shared;
        private readonly NativeMap14SharedEventSubscription subscription;
        private bool disposed;
        public object Original { get; }

        private NativeMap14VisibilityData(NativeMap14SharedAssemblies shared, object original)
        {
            this.shared=shared;Original=original;
            subscription=new NativeMap14SharedEventSubscription(Get("OnClientVisibilityUpdate"),
                shared.CommonType("IEcEntityClientVisibilityUpdatedEventReceiver"),args=>
                {
                    if (!ReferenceEquals(args[0],Original)) throw new InvalidOperationException("Original visibility event owner differs.");
                    OnClientVisibilityUpdate.FireEvent(this);
                });
        }
        public static NativeMap14VisibilityData Bind(NativeMap14SharedAssemblies shared,NativeMap14SharedViewEntity view)
        {
            if (shared==null || view==null) throw new ArgumentNullException(shared==null?nameof(shared):nameof(view));
            var original=view.Original.GetType().GetMethod("GetComponent",new[]{typeof(Type)})
                .Invoke(view.Original,new object[]{shared.CommonType("VisibilityComponent")});
            var existing=view.GetComponent<Map13VisibilityData>();
            if (existing!=null)
            {
                if (!(existing is NativeMap14VisibilityData native) || native.disposed || !ReferenceEquals(native.Original,original))
                    throw new InvalidOperationException("Original visibility projection is already bound differently.");
                return native;
            }
            if (original==null) return null;
            var projection=new NativeMap14VisibilityData(shared,original);
            try { view.BindAdditionalComponent<Map13VisibilityData>(projection);return projection; }
            catch { projection.Dispose();throw; }
        }
        public override ClientVisibilityType ClientVisibilityType
        {
            get => (ClientVisibilityType)Convert.ToInt32(Get("ClientVisibilityType"));
            set => Set("ClientVisibilityType",Enum.ToObject(shared.CommonType("ClientVisibilityType"),(int)value));
        }
        public override bool IsShowNeedFadeIn
        {
            get => (bool)Get("IsShowNeedFadeIn");
            set => Set("IsShowNeedFadeIn",value);
        }
        public override void AddClientVisibilityRequest(ClientVisibilityType type)=>Call("AddClientVisibilityRequest",type);
        public override void RemoveClientVisibilityType(ClientVisibilityType type)=>Call("RemoveClientVisibilityType",type);
        private void Call(string method,ClientVisibilityType type)
        {
            RequireActive();Original.GetType().GetMethod(method).Invoke(Original,
                new[]{Enum.ToObject(shared.CommonType("ClientVisibilityType"),(int)type)});
        }
        private object Get(string name) { RequireActive();return NativeMap14SharedPrefabs.Get(Original,name); }
        private void Set(string name,object value) { RequireActive();Original.GetType().GetField(name).SetValue(Original,value); }
        private void RequireActive() { if (disposed) throw new ObjectDisposedException(nameof(NativeMap14VisibilityData)); }
        public void Dispose() { if (disposed)return;subscription.Dispose();disposed=true; }
    }
}
