using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
namespace Zjcs.Official.EntityLifecycle
{
    // Consumed original container/component metadata. A formal host must bind
    // original shared rules and the root entity; these types implement none.
    public abstract class MyContainer : MonoBehaviour
    {
        public abstract IECViewMapRootLifetime Container { get; }
        public abstract MyEntity MyRootEntity { get; }
        public abstract ILogger Logger { get; }
        public static ILogger CreateLogger(string category) => Debug.unityLogger;
    }
    public abstract class EntityGridGroundData { }
    public interface IEntityComponentInfoSource { object ComponentInfo { get; } }
    public static class EntityLifecycleServices
    {
        public static object Info(object component)
        {
            if (component is ECGridTransformData grid) return grid.Info;
            return (component as IEntityComponentInfoSource ??
                throw new InvalidOperationException("Original shared component info is not bound.")).ComponentInfo;
        }
    }
}
