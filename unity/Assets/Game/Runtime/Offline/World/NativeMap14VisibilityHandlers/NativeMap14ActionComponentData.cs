using System;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StoryPlayables;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Offline.World.NativeMap14VisibilityHandlers
{
    public sealed class NativeMap14ActionComponentData:ActionComponentData
    {
        public object Original { get; }
        private NativeMap14ActionComponentData(object original){Original=original;}
        public override EcEntityIdentifier ApplyId
        {
            get {var id=NativeMap14SharedPrefabs.Get(Original,"ApplyId");return new EcEntityIdentifier((long)NativeMap14SharedPrefabs.Get(id,"ContainerId"),(int)NativeMap14SharedPrefabs.Get(id,"EntityId"));}
        }
        public override object ComponentInfo=>NativeMap14SharedPrefabs.Get(Original,"Info");
        public static NativeMap14ActionComponentData Bind(NativeMap14SharedAssemblies shared,NativeMap14SharedViewEntity view)
        {
            if(shared==null||view==null)throw new ArgumentNullException(shared==null?nameof(shared):nameof(view));
            var original=view.Original.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(view.Original,new object[]{shared.CommonType("ActionComponent")});
            var previous=view.GetComponent<ActionComponentData>();
            if(previous!=null)
            {
                if(!(previous is NativeMap14ActionComponentData native)||!ReferenceEquals(native.Original,original))throw new InvalidOperationException("Original action projection is already bound differently.");
                return native;
            }
            if(original==null)return null;
            var value=new NativeMap14ActionComponentData(original);view.BindAdditionalComponent<ActionComponentData>(value);return value;
        }
    }
}
