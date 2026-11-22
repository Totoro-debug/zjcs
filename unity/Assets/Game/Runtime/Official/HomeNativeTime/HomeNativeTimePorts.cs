using System;
namespace Zjcs.Official.HomeNativeTime
{
    public interface IHomeNativeTimeManager
    {
        float PlayableDeltaTime { get; }
        float PlayableTimeScale { get; }
    }
    public interface IHomeTimeDriveEvents
    {
        IDisposable Subscribe(Action<HomeTimeDriveSkeletonData> handler);
        IDisposable Subscribe(Action<HomeTimeDriveAnimationData> handler);
        IDisposable Subscribe(Action<HomeTimeDriveAnimatorData> handler);
        void Publish(HomeTimeDriveSkeletonData data);
        void Publish(HomeTimeDriveAnimationData data);
        void Publish(HomeTimeDriveAnimatorData data);
    }
}
