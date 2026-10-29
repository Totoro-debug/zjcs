using System;
using Zjcs.Official.EntityVisibilityHandlers;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Offline.World.NativeMap14SharedViews;

namespace Zjcs.Offline.World.NativeMap14VisibilityHandlers
{
    public sealed class NativeMap14VisibilityAwardItemData:VisibilityAwardItemData,IDisposable
    {
        public object Original { get; }
        private readonly NativeMap14SharedEventSubscription subscription;
        private bool disposed;
        private NativeMap14VisibilityAwardItemData(NativeMap14SharedAssemblies shared,object original)
        {
            Original=original;
            subscription=new NativeMap14SharedEventSubscription(NativeMap14SharedPrefabs.Get(original,"OnStageResultAwardItemUpdated"),
                shared.CommonType("IEcEntityStageResultItemUpdatedEventReceiver"),args=>
                {
                    if(!ReferenceEquals(args[0],Original))throw new InvalidOperationException("Original award update owner differs.");
                    OnStageResultAwardItemUpdated.FireEvent(this);
                });
        }
        public override bool TryGetAwardItemInfo(int compIndex,out object result)
        {
            if(disposed)throw new ObjectDisposedException(nameof(NativeMap14VisibilityAwardItemData));
            var args=new object[]{compIndex,null};bool success=(bool)Original.GetType().GetMethod("TryGetAwardItemInfo").Invoke(Original,args);result=args[1];return success;
        }
        public static NativeMap14VisibilityAwardItemData Bind(NativeMap14SharedAssemblies shared,NativeMap14SharedViewEntity view)
        {
            if(shared==null||view==null)throw new ArgumentNullException(shared==null?nameof(shared):nameof(view));
            var original=view.Original.GetType().GetMethod("GetComponent",new[]{typeof(Type)}).Invoke(view.Original,new object[]{shared.CommonType("StageResultAwardItemComponent")});
            var previous=view.GetComponent<VisibilityAwardItemData>();
            if(previous!=null)
            {
                if(!(previous is NativeMap14VisibilityAwardItemData native)||native.disposed||!ReferenceEquals(native.Original,original))throw new InvalidOperationException("Original visibility award projection is already bound differently.");
                return native;
            }
            if(original==null)return null;
            var value=new NativeMap14VisibilityAwardItemData(shared,original);
            try{view.BindAdditionalComponent<VisibilityAwardItemData>(value);return value;}
            catch{value.Dispose();throw;}
        }
        public void Dispose(){if(disposed)return;subscription.Dispose();disposed=true;}
    }
}
