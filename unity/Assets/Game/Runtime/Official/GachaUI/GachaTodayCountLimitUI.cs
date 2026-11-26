using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.GachaUI
{
    // Original daily-count display, including deferred refresh during results.
    public class GachaTodayCountLimitUI : MonoBehaviour
    {
        [SerializeField] private WorldMultiLanguageText mTodayGachaCountText;
        [SerializeField] private GachaClassType mGachaClassType;
        [SerializeField] private bool mIsInstantRefresh;
        public IGachaDailyCountServices Services { private get; set; }

        public void SetGachaClassType(GachaClassType type)
        {
            mGachaClassType = type;
            RefreshTodayGachaCount();
        }

        private void OnEnable()
        {
            RefreshTodayGachaCount();
            Services.OnGacha += MainPlayer_OnGacha;
        }

        private void OnDisable()
        {
            if (Services != null) Services.OnGacha -= MainPlayer_OnGacha;
        }

        public void RefreshTodayGachaCount()
        {
            mTodayGachaCountText.Format(Services.GetGachaDailyCount(mGachaClassType),
                Services.GetDailyGachaMaxCount(mGachaClassType));
        }

        private void MainPlayer_OnGacha(GachaType gachaType, int count)
        {
            if (mIsInstantRefresh) RefreshTodayGachaCount();
        }
    }
}
