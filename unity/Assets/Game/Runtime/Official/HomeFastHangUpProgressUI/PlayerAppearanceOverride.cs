// Selected original client; local ownership and native animation endpoints.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.WorldUI;
using PlayerAppearance = Zjcs.Official.CombatAppearance.PlayerAppearance;
using AppearanceUI = Zjcs.Official.CombatAppearance.AppearanceUI;
using ItemInfo = Zjcs.Official.ProfessionUI.IProfessionItemInfo;
namespace Zjcs.Official.HomeFastHangUpProgressUI
{


public class PlayerAppearanceOverride : MonoBehaviour
{
    public IHomeFastHangUpProgressServices Services { protected get; set; }
	public AppearanceOverrideType OverrideType;

	private AppearanceRuntimeDataOverride mAppearanceOverride;

	public AppearanceRuntimeDataOverride AppearanceOverride
	{
		get
		{
			if (mAppearanceOverride == null)
			{
				AppearanceOverrideConfig overrideConfig = Services.AppearanceConfig.GetOverrideConfig(OverrideType);
				mAppearanceOverride = AppearanceRuntimeDataOverride.CreateAppearanceOverride(Services, overrideConfig.MaleOverrideSkinIdList, overrideConfig.FemaleOverrideSkinIdList);
			}
			return mAppearanceOverride;
		}
	}
}

}
