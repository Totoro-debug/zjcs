using System;

namespace Zjcs.Official.HomeCountControlsUI
{
    public interface IUseCountSliderServices
    {
        double FastItemMaxCountMessageColdDown { get; }
        string TextFormatted(string key, params object[] args);
        void SystemMessage(string message);
    }

    // Original SetUseCountSlider, shared by item use and fast hang-up windows.
    public class SetUseCountSlider : SetCountSlider
    {
        public IUseCountSliderServices LimitServices { protected get; set; }
        private long mSingleMaxUseCount;
        private string mTipKey;
        private DateTime mLastShowTipTime = DateTime.MinValue;

        public void Init(long minValue, long maxValue, long currentValue,
            long singleMaxUseCount = -1, string tipKey = "")
        {
            mSingleMaxUseCount = singleMaxUseCount <= -1 ? maxValue : singleMaxUseCount;
            mTipKey = tipKey;
            base.Init(minValue, maxValue, currentValue, 1);
        }
        protected override void OnSliderValueChangedHandle(ref long value)
        {
            base.OnSliderValueChangedHandle(ref value);
            if (value > mSingleMaxUseCount)
            {
                if (!string.IsNullOrEmpty(mTipKey)) TryShowMsg();
                value = mSingleMaxUseCount;
                Slider.value = value;
            }
        }
        protected override long GetCurrentCount(long currentValue, long minValue)
            => Math.Max(minValue, Math.Min(mSingleMaxUseCount, currentValue));
        private void TryShowMsg()
        {
            if (!((DateTime.Now - mLastShowTipTime).TotalSeconds < LimitServices.FastItemMaxCountMessageColdDown))
            {
                LimitServices.SystemMessage(LimitServices.TextFormatted(mTipKey, mSingleMaxUseCount));
                mLastShowTipTime = DateTime.Now;
            }
        }
    }
}
