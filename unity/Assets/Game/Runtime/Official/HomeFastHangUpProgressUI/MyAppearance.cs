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


public class MyAppearance : MonoBehaviour
{
    public IHomeFastHangUpProgressServices Services { protected get; set; }
	private PlayerAppearance mPlayerAppearance;

	private void Awake()
	{
		Init();
	}

	public void Init()
	{
		if (mPlayerAppearance)
		{
			return;
		}
		mPlayerAppearance = ((Component)this).GetComponent<PlayerAppearance>();
		if (mPlayerAppearance)
		{
			PlayerAppearanceOverride playerAppearanceOverride = default;
			if (((Component)this).TryGetComponent<PlayerAppearanceOverride>(out playerAppearanceOverride))
			{
				mPlayerAppearance.SetRuntimeDataOverride(playerAppearanceOverride.AppearanceOverride);
			}
			mPlayerAppearance.Init(Services.MainPlayerId);
		}
	}
}

}
