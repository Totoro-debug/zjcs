using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>ChatQuickMsgEntryPanel.Refresh / Update cooldown mask.</summary>
    public sealed class ChatQuickMsgEntryPanel : MonoBehaviour
    {
        public Image OpenBtnCDMask;
        private double mRemainTime, interval;
        public void Refresh(double remainingTime, double chatQuickMsgInterval)
        {
            mRemainTime = remainingTime;
            interval = chatQuickMsgInterval;
            OpenBtnCDMask.gameObject.SetActive(mRemainTime > 0);
        }
        private void Update()
        {
            if (mRemainTime <= 0) return;
            OpenBtnCDMask.fillAmount = (float)(mRemainTime / interval);
            mRemainTime -= Time.unscaledDeltaTime;
            if (mRemainTime <= 0) OpenBtnCDMask.gameObject.SetActive(false);
        }
    }
}
