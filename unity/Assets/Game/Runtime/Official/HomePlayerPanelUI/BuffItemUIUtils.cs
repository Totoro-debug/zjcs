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
using System.Collections.Generic;

public static class BuffItemUIUtils
{
	public static HomeEntryDetailData CreateBuffItemEntryDetailData(IHomePlayerPanelBindings PanelBindings, IHomePanelStatusEffect statusEffectData)
	{
		IHomePanelStatusInfo info = statusEffectData.Info;
		int entryId = info.EntryId;
		HomePanelEntryInfo entryInfo = PanelBindings.GetEntry(entryId);
		string icon = entryInfo.Icon;
		int displayRemainTimes = statusEffectData.GetDisplayRemainTimes();
		CultureTextArg cultureTextArg = new CultureTextArg(info.IsReduceOncePerPlay ? "ui_cooking_buff_remain_times_continuous" : "ui_cooking_buff_remain_times", displayRemainTimes);
		HomePanelTextKeyArgWrap nameStr = ((displayRemainTimes <= 0) ? new HomePanelTextKeyArgWrap("{0}", new CultureTextArg(PanelBindings.GetEntryNameKey(entryId))) : new HomePanelTextKeyArgWrap("{0}{1}", new CultureTextArg(PanelBindings.GetEntryNameKey(entryId)), cultureTextArg));
		string textFormated = PanelBindings.TextFormatted(PanelBindings.GetEntryDescKey(entryId), PanelBindings.GetStatusEffectVaildShowScopesName(PanelBindings.MainPlayer, info.ClassId));
		HomePanelTextKeyArgWrap descStr = new HomePanelTextKeyArgWrap("{0}", textFormated);
		Dictionary<EntryDetailAdditionInfoType, EntryDetailAdditionBaseInfo> statusEffectDataAdditionInfo = GetStatusEffectDataAdditionInfo(PanelBindings, statusEffectData);
		return new HomeEntryDetailData(icon, nameStr, descStr, entryInfo.Bg, statusEffectDataAdditionInfo);
	}

	public static HomeEntryDetailData CreateBuffItemEntryDetailDataById(IHomePlayerPanelBindings PanelBindings, int entryId, int stackCount = 0)
	{
		HomePanelEntryInfo entryInfo = PanelBindings.GetEntry(entryId);
		if (entryInfo == null)
		{
			return default;
		}
		string icon = entryInfo.Icon;
		HomePanelTextKeyArgWrap nameStr = new HomePanelTextKeyArgWrap("{0}", new CultureTextArg(PanelBindings.GetEntryNameKey(entryId)));
		HomePanelTextKeyArgWrap descStr = ((!PanelBindings.TryGetCookingBuffEntryDesc(PanelBindings.MainPlayer, entryId, out var desc)) ? new HomePanelTextKeyArgWrap("{0}", new CultureTextArg(PanelBindings.GetEntryDescKey(entryId))) : new HomePanelTextKeyArgWrap("{0}", PanelBindings.TextFormatted(PanelBindings.GetEntryDescKey(entryId), desc)));
		return new HomeEntryDetailData(icon, nameStr, descStr, entryInfo.Bg, null, stackCount);
	}

	public static Dictionary<EntryDetailAdditionInfoType, EntryDetailAdditionBaseInfo> GetStatusEffectDataAdditionInfo(IHomePlayerPanelBindings PanelBindings, IHomePanelStatusEffect statusEffectData)
	{
		switch (statusEffectData.Info.IconType)
		{
		case StatusEffectIconType.Normal:
			return null;
		case StatusEffectIconType.Duration:
			return GetDurationStatusEffectDataAdditionInfo(PanelBindings, statusEffectData);
		default:
		{
			UnityEngine.Debug.LogError("Unknown original status icon type: " + statusEffectData.Info.IconType);
			return null;
		}
		}
	}

	private static Dictionary<EntryDetailAdditionInfoType, EntryDetailAdditionBaseInfo> GetDurationStatusEffectDataAdditionInfo(IHomePlayerPanelBindings PanelBindings, IHomePanelStatusEffect statusEffectData)
	{
		return new Dictionary<EntryDetailAdditionInfoType, EntryDetailAdditionBaseInfo> {
		{
			EntryDetailAdditionInfoType.EndTime,
			new EntryDetailAdditionEndTimeInfo
			{
				EndTime = statusEffectData.LastGainedTime.AddSeconds(statusEffectData.Info.Duration)
			}
		} };
	}
}

}
