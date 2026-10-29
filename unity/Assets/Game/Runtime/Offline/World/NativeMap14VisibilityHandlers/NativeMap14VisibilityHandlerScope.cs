using System;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityVisibilityHandlers;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Offline.World.NativeMap14VisibilityHandlers
{
    // Creates fresh original handlers; the original factory owns all pooling.
    public sealed class NativeMap14VisibilityHandlerScope:IServiceProvider,IVisibilityMapRoot,IVisibilityHandlerConstruction
    {
        public MyContainer MyContainer { get; }
        public EntityClientVisibilityHandlerFactory Factory { get; }
        public NativeMap14VisibilityHandlerScope(MyContainer container)
        {
            MyContainer=container?container:throw new ArgumentNullException(nameof(container));
            Factory=new EntityClientVisibilityHandlerFactory{ServiceProvider=this};
        }
        public object GetService(Type type)=>type.IsInstanceOfType(this)?this:null;
        public T CreateInstance<T>() where T:IClientVisibilityHandler
        {
            var handler=Activator.CreateInstance<T>();
            if(handler is ClientVisibilityWaitActionHandler wait)wait.MapRootManagerProxy=this;
            else if(handler is ClientVisibilityHideHanlder hide)hide.MapRootManagerProxy=this;
            return handler;
        }
    }
}
