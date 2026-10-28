// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class AchievementTypeAndParamItem
{

	public int AchievementType;

	public string JsonParam;

	public virtual AchievementTypeAndParamItem Clone()
	{
		AchievementTypeAndParamItem achievementTypeAndParamItem = new AchievementTypeAndParamItem();
		achievementTypeAndParamItem.Fill(this);
		return achievementTypeAndParamItem;
	}

	public virtual void Fill(AchievementTypeAndParamItem obj)
	{
		AchievementType = obj.AchievementType;
		JsonParam = obj.JsonParam;
	}
}
}
