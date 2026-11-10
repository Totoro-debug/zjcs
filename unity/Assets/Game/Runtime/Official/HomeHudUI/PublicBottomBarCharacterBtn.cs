using System;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeHudUI
{
public class PublicBottomBarCharacterBtn : MonoBehaviour
{
	public IHomeBottomBarServices Services { get; set; }

	[SerializeField]
	private WorldStateSwitcher<Sex> mSexSwitcher;

	private void OnEnable()
	{
		Services.OnPlayerUpdate += new Action(SetSex);
		SetSex();
	}

	private void OnDisable()
	{
		if (Services != null)
		{
			Services.OnPlayerUpdate -= new Action(SetSex);
		}
	}

	public void SetSex()
	{
		if (Services.HasPlayer)
		{
			mSexSwitcher.SetState(Services.PlayerSex);
		}
	}
}

}
