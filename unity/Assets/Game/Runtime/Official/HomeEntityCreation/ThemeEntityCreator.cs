// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public class ThemeEntityCreator : BaseHomeEntityCreator<HomeThemeType>
{
	public ThemeEntityCreator(Dictionary<HomeThemeType, Transform> slotDict)
		: base(slotDict)
	{
	}

	protected override int GetCurrentSkinId(HomeThemeType key, IHomeEntityManagerContext context)
	{
		return context.PlayerData.GetThemeSkinId(key);
	}

	protected override string GetPrefabPath(int id, IHomeEntityManagerContext context)
	{
		if (TryGetSkinOverridePrefabName(id, context, out var prefabName))
		{
			return context.PrefabPaths.GetThemePrefabPath(id, prefabName);
		}
		return context.PrefabPaths.GetThemePrefabPath(id);
	}

	protected override string GetEntityTypeName()
	{
		return "主题";
	}
}
}
