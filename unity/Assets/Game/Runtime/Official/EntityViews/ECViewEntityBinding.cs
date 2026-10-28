using System;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.EntityViews
{
    // Required source-entity consumption boundary; no entity rules or event
    // dispatch implementation are inferred by this migration.
    public abstract class ECViewEntityData
    {
        public abstract int ClassId { get; }
        public abstract EcEntityIdentifier Id { get; }
        public abstract T GetComponent<T>();
    }
    public abstract class ECViewEntityBinding : MonoBehaviour
    {
        public abstract ECViewEntityData Entity { get; }
        public abstract void SetClientVisibilitySupporter(IClientVisibilitySupporter value);
        public abstract void AddStartReceiver(IMyEntityStartEventReceiver value);
        public abstract void RemoveStartReceiver(IMyEntityStartEventReceiver value);
        public abstract void AddRemoveReceiver(IMyEntityRemoveEventReceiver value);
        public abstract void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value);
        public abstract void AddStartPostReceiver(IMyEntityStartPostEventReceiver value);
        public abstract void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value);
        public abstract void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value);
        public abstract void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value);
    }
    // The official base subscribes to EveryUpdate and PreLateUpdate. Its host
    // supplies those two frame streams and each independent disposable lease.
    public readonly struct ECViewFrameUnit { }
    public interface IECViewFrameSource
    {
        IDisposable SubscribeUpdate(Action<ECViewFrameUnit> callback);
        IDisposable SubscribePreLateUpdate(Action<ECViewFrameUnit> callback);
    }
    public interface IECViewInjection { void Inject(object value); }
    public interface IECViewMapRootLifetime { bool Destroying { get; } }
    internal static class ECViewServices
    {
        public static void Inject(IServiceProvider provider, object value)
        {
            if (!(provider is IECViewInjection injection))
                throw new InvalidOperationException("Original EC view dependency injection is not bound.");
            injection.Inject(value);
        }
        public static IECViewFrameSource Frames(IServiceProvider provider) =>
            provider.GetService(typeof(IECViewFrameSource)) as IECViewFrameSource ??
            throw new InvalidOperationException("Original EC view frame streams are not bound.");
    }
}
