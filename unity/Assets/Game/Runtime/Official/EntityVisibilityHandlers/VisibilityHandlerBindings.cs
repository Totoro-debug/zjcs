using System;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Official.EntityVisibilityHandlers
{
    public interface IVisibilityMapRoot { MyContainer MyContainer { get; } }
    public interface IVisibilityHandlerConstruction
    { T CreateInstance<T>() where T:IClientVisibilityHandler; }
    public static class VisibilityHandlerConstruction
    {
        public static T CreateInstance<T>(IServiceProvider provider) where T:IClientVisibilityHandler =>
            (provider as IVisibilityHandlerConstruction??throw new InvalidOperationException("Original visibility handler construction is not bound.")).CreateInstance<T>();
    }
    // Original result data and event ABI are supplied by a native projection.
    public abstract class VisibilityAwardItemData
    {
        public EcEntityStageResultItemUpdatedEventDelegate OnStageResultAwardItemUpdated=new EcEntityStageResultItemUpdatedEventDelegate();
        public abstract bool TryGetAwardItemInfo(int compIndex,out object result);
    }
}
