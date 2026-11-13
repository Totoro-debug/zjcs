// Original selected 884 bed/cart tip logic; explicit owner and scheduling boundaries.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneUiControls;
namespace Zjcs.Official.HomeHangUpTips
{
public sealed class HomeHangUpTipSettings
{
    [Serializable] public sealed class HangUpTipSetting
    {
        public List<float> TipLevelList;
        public List<EFurniture> FurnitureList;
    }
    private readonly List<HangUpTipSetting> mHangUpTipSettings;
    private Dictionary<EFurniture, HangUpTipSetting> mTipSettings;
    public float ExpForLevelUpPercentTip { get; }
    public HomeHangUpTipSettings(List<HangUpTipSetting> settings, float percent)
    { mHangUpTipSettings = settings ?? throw new ArgumentNullException(nameof(settings)); ExpForLevelUpPercentTip = percent; }
	public bool TryGetHangUpTipSetting(EFurniture furnitureType, out HangUpTipSetting setting)
	{
		if (mTipSettings == null)
		{
			mTipSettings = new Dictionary<EFurniture, HangUpTipSetting>();
			foreach (HangUpTipSetting mHangUpTipSetting in mHangUpTipSettings)
			{
				foreach (EFurniture furniture in mHangUpTipSetting.FurnitureList)
				{
					mTipSettings.Add(furniture, mHangUpTipSetting);
				}
			}
		}
		return mTipSettings.TryGetValue(furnitureType, out setting);
	}
}
}
