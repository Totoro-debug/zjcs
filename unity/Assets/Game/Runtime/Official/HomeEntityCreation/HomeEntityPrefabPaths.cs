// Selected original home entity creation implementation; see its source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityCreation
{
using Object = UnityEngine.Object;
public sealed class HomeEntityPrefabPaths
{
public IHomeEntityPrefabTables Tables { get; }
public HomeEntityPrefabPaths(IHomeEntityPrefabTables tables) { Tables = tables ?? throw new ArgumentNullException(nameof(tables)); }
public string GetFurniturePrefabPath(int skinItemId)
	{
		FurnitureSkinInfo furnitureSkinInfo = Tables.GetFurnitureSkin(skinItemId);
		if (furnitureSkinInfo != null)
		{
			return GetFurniturePrefabPath(skinItemId, furnitureSkinInfo.PrefabName);
		}
		return string.Empty;
	}
public string GetFurniturePrefabPath(int skinItemId, string prefabName)
	{
		FurnitureSkinInfo furnitureSkinInfo = Tables.GetFurnitureSkin(skinItemId);
		if (furnitureSkinInfo == null)
		{
			return string.Empty;
		}
		return $"Home/Dynamic/Furniture/{furnitureSkinInfo.FurnitureType}/{prefabName}";
	}
public string GetThemePrefabPath(int skinItemId)
	{
		HomeThemeSkinInfo homeThemeSkinInfo = Tables.GetThemeSkin(skinItemId);
		if (homeThemeSkinInfo != null)
		{
			return GetThemePrefabPath(skinItemId, homeThemeSkinInfo.PrefabName);
		}
		return string.Empty;
	}
public string GetThemePrefabPath(int skinItemId, string prefabName)
	{
		HomeThemeSkinInfo homeThemeSkinInfo = Tables.GetThemeSkin(skinItemId);
		if (homeThemeSkinInfo == null)
		{
			return string.Empty;
		}
		return $"Home/Dynamic/Theme/{homeThemeSkinInfo.ThemeType}/{prefabName}";
	}
}
}
