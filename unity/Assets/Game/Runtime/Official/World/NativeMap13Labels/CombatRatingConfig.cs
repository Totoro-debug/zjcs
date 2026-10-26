// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatRatingConfig : ScriptableObject
{
	[Serializable]
	private class LevelColorThemeConfig
	{
		public string Memo;

		public MonoDictionary<int, Color> LevelColors;
	}

	[SerializeField]
	private MonoDictionary<CombatRateingColorType, int> mTypeToThemes;

	[SerializeField]
	private List<LevelColorThemeConfig> mColorTemplates;

	public bool TryGetLevelColor(int index, int level, out Color color)
	{

		color = default;
		if (index < 0 || index >= mColorTemplates.Count)
		{
			UnityEngine.Debug.LogError("Original combat rating color index or level is absent.");
			return false;
		}
		if (!mColorTemplates[index].LevelColors.TryGetValue(level, out color))
		{
			UnityEngine.Debug.LogError("Original combat rating color index or level is absent.");
			return false;
		}
		return true;
	}

	public bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color)
	{

		color = default;
		if (!mTypeToThemes.TryGetValue(type, out var value))
		{
			UnityEngine.Debug.LogError("Original combat rating color type is absent.");
			return false;
		}
		return TryGetLevelColor(value, level, out color);
	}
}

}
