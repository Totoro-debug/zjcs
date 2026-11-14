using System;

namespace Zjcs.Official.HomeEnvironmentTime
{
    // Original AllDayStageHandler; global player/condition lookups are explicit ports.
    public sealed class AllDayStageHandler : IHomeDayStageHandler, IDisposable
    {
        readonly IHomeNaturalTimeLimit limit;
        bool mIsMet, mRegistered;
        public AllDayStageHandler(IHomeNaturalTimeLimit limit) { this.limit = limit; }
        public bool CanInterrupt => false;
        public HomeDayTime HomeDayTime { get; } = new HomeDayTime { Hour = 12, Minute = 0, Second = 0 };
        public float DeltaTime => -1f;
        public bool TryUpdate()
        {
            if (!mRegistered)
            {
                limit.Register(OnCondition);
                mRegistered = true;
            }
            return mIsMet;
        }
        void OnCondition()
        {
            mIsMet = !limit.IsLimited;
            if (mIsMet) DeregisterIfNeeded();
        }
        void DeregisterIfNeeded()
        {
            if (!mRegistered) return;
            limit.Deregister(OnCondition);
            mRegistered = false;
        }
        public void Dispose()
        {
            DeregisterIfNeeded();
            mIsMet = false;
            mRegistered = false;
        }
    }
    public interface IHomeNaturalTimeLimit
    {
        bool IsLimited { get; }
        void Register(Action callback);
        void Deregister(Action callback);
    }
}
