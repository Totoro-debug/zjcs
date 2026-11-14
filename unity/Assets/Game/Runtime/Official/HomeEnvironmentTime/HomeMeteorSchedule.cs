using System;

namespace Zjcs.Official.HomeEnvironmentTime
{
    // Original HomeMeteor timing, wrap and probability branches. The existing
    // Windows template owner handles loading/reuse through the explicit Play port.
    public sealed class HomeMeteorSchedule
    {
        readonly HomeEnvironmentTimeState mManager;
        readonly Func<int, int, int> range;
        readonly Action<int> play;
        HomeDayTime mHomeDayTime;
        HomeDayTime mPlayHomeDayTime;
        bool mActive;

        public HomeMeteorSchedule(HomeEnvironmentTimeState manager, Func<int, int, int> range, Action<int> play)
        {
            mManager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.range = range ?? throw new ArgumentNullException(nameof(range));
            this.play = play ?? throw new ArgumentNullException(nameof(play));
            bool flag = mManager.Config.Value.MeteorConfig.InActiveTime(mManager.StageHomeDayTime);
            if (flag != mActive)
            {
                mActive = flag;
                if (flag) RandomNext();
            }
        }

        public void Update()
        {
            HomeDayTime stageHomeDayTime = mManager.StageHomeDayTime;
            HomeMeteorConfig meteorConfig = mManager.Config.Value.MeteorConfig;
            if (!mActive)
            {
                if (meteorConfig.InActiveTime(stageHomeDayTime))
                {
                    mActive = true;
                    RandomNext();
                }
            }
            else if (!meteorConfig.InActiveTime(stageHomeDayTime)) mActive = false;
            if (!mActive) return;
            if (mHomeDayTime > stageHomeDayTime)
            {
                if (mPlayHomeDayTime > mHomeDayTime) TryPlay();
            }
            else if (stageHomeDayTime >= mPlayHomeDayTime) TryPlay();
            mHomeDayTime = stageHomeDayTime;
        }

        void TryPlay()
        {
            var meteorConfig = mManager.Config.Value.MeteorConfig;
            if (range(0, 100) / 100f > meteorConfig.Probability) return;
            int num = range(0, meteorConfig.Templates.Length);
            play(num);
            RandomNext();
        }

        void RandomNext()
        {
            var value = mManager.Config.Value;
            mPlayHomeDayTime = mManager.HomeDateTime.GetHomeDayTime().AddSeconds(value, value.MeteorConfig.Interval);
        }
    }
}
