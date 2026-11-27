// Original Player treasure transitions; owner defers notification delivery until durable commit.
using System;
namespace Zjcs.Official.TreasureCultivation {
public sealed class OfficialTreasureStateTransitions {
private readonly ITreasureTransitionNotifications notifications;
public OfficialTreasureStateTransitions(ITreasureTransitionNotifications notifications) { this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications)); }
public void UpgradeTreasureStar(TreasureItem data, int addCount = 1)
	{
		TreasureState itemParamTreasure = data.GetItemParamTreasure();
		_ = itemParamTreasure.Star;
		SetTreasureStar(data, itemParamTreasure.Star + addCount);
	}

public void SetTreasureStar(TreasureItem data, int star)
	{
		TreasureState itemParamTreasure = data.GetItemParamTreasure();
		int star2 = itemParamTreasure.Star;
		itemParamTreasure.Star = star;
		notifications.UpgradeStar(data, star2, itemParamTreasure.Star);
		FireOnTreasureRefresh(data, isNew: false);
	}

public void UpgradeTreasureStarWithoutNortify(TreasureItem data, int addCount = 1)
	{
		TreasureState itemParamTreasure = data.GetItemParamTreasure();
		_ = itemParamTreasure.Star;
		itemParamTreasure.Star += addCount;
	}

public void TreasureActivate(TreasureItem itemData)
	{
		_ = itemData.Info.PartInfo;
		itemData.GetItemParamTreasure().Active = true;
		FireOnTreasureActive(itemData);
		FireOnTreasureRefresh(itemData, isNew: false);
	}

public void TreasureActivateWithoutNortify(TreasureItem itemData)
	{
		_ = itemData.Info.PartInfo;
		itemData.GetItemParamTreasure().Active = true;
	}

public void FireOnTreasureRefresh(TreasureItem data, bool isNew)
	{
		notifications.Refresh(data, isNew);
	}

public void FireOnTreasureActive(TreasureItem data)
	{
		notifications.Active(data);
	}
}}
