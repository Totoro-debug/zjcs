// Original AirshipActivityBannerItem with the existing local language component.
using System;
using UnityEngine;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeAirship
{
    public sealed class AirshipActivityBannerItem : MonoBehaviour
    {
        [SerializeField] private WorldMultiLanguageText mTitleTxt;

        public void Init(AirshipActivityEntry data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            mTitleTxt.SetKey($"activity_entry_{(ActivityEntryType)data.EntryType}_{data.ActivityId}_mainTitle");
        }
    }
}
