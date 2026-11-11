// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;

public interface IPlayerStatusProvider
{
	void Start();

	void End();

	void RegisterUpdateStatusCallBack(Action callBack);

	void UnRegisterUpdateStatusCallBack(Action callBack);

	List<HomeEntryDetailData> GetEntryDatas();

	bool HasEntryData();
}

}
