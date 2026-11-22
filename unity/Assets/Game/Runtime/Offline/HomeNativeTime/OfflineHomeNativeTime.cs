using System;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using Zjcs.Official.HomeNativeTime;

namespace Zjcs.Offline.HomeNativeTime
{
    // Synchronous local boundary for the original MessagePipe registrations.
    public sealed class OfflineHomeTimeDriveEvents : IHomeTimeDriveEvents
    {
        sealed class Subscription : IDisposable
        {
            Action release;
            internal Subscription(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        event Action<HomeTimeDriveSkeletonData> skeletons;
        event Action<HomeTimeDriveAnimationData> animations;
        event Action<HomeTimeDriveAnimatorData> animators;
        public IDisposable Subscribe(Action<HomeTimeDriveSkeletonData> handler)
        { skeletons += handler; return new Subscription(() => skeletons -= handler); }
        public IDisposable Subscribe(Action<HomeTimeDriveAnimationData> handler)
        { animations += handler; return new Subscription(() => animations -= handler); }
        public IDisposable Subscribe(Action<HomeTimeDriveAnimatorData> handler)
        { animators += handler; return new Subscription(() => animators -= handler); }
        public void Publish(HomeTimeDriveSkeletonData data) => skeletons?.Invoke(data);
        public void Publish(HomeTimeDriveAnimationData data) => animations?.Invoke(data);
        public void Publish(HomeTimeDriveAnimatorData data) => animators?.Invoke(data);
    }

    public sealed class OfflineHomeNativeTime : IHomeNativeTimeManager, IDisposable
    {
        sealed class DriverImpl : BaseTimeDriver { }
        readonly Func<float> delta, scale;
        bool active, disposed;
        public OfflineHomeTimeDriveEvents Events { get; } = new OfflineHomeTimeDriveEvents();
        public BaseTimeDriver Driver { get; }
        public float PlayableDeltaTime => delta();
        public float PlayableTimeScale => scale();

        public OfflineHomeNativeTime(Func<float> delta, Func<float> scale)
        {
            this.delta = delta ?? throw new ArgumentNullException(nameof(delta));
            this.scale = scale ?? throw new ArgumentNullException(nameof(scale));
            Driver = new DriverImpl {
                SkeletonAnimations = new List<SkeletonAnimation>(), Animations = new List<Animation>(),
                HomeTimeDriveSkeletonSubscriber = Events, HomeTimeDriveAnimationSubscriber = Events,
                HomeTimeDriveAnimatorSubscriber = Events };
            Driver.Init(this);
        }
        public void Enable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeNativeTime));
            active = true; Driver.OnEnable();
        }
        public void Disable()
        {
            if (disposed) return;
            active = false; Driver.OnDisable();
        }
        public void Tick()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeNativeTime));
            if (active) Driver.Update();
        }
        public void Dispose()
        {
            if (disposed) return;
            Disable(); Driver.OnDestroy(); disposed = true;
        }
    }
}
