using System;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // LockMaskWithTime: preserve the original default, state order and 0.1s
    // refresh interval. The local clock replaces TimeManager's server clock.
    public sealed class WorldLockMaskWithTime : WorldInitializedUI
    {
        public WorldStateSwitcher<bool> mLockedStateSwitcher;
        public WorldShowRemainTimeInfo mShowRemainTimeInfo;
        public float mLockTimeDefault = 5f;
        private float lockTime;

        public void Init(float value = -1f) => InitWrapper(() =>
            lockTime = value == -1f ? mLockTimeDefault : value);

        protected override void OnEnableAndInited()
        {
            if (lockTime <= 0f) return;
            mLockedStateSwitcher.SetState(true);
            DateTime end = mShowRemainTimeInfo.Clock().AddSeconds(lockTime);
            mShowRemainTimeInfo.Init(end, .1f, null, WorldTimeSpanFormatType.Normal,
                () => mLockedStateSwitcher.SetState(false));
        }

        protected override void OnDisableAndInited()
        {
            mShowRemainTimeInfo.Stop();
            mLockedStateSwitcher.SetState(false);
        }
    }
}
