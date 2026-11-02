// Selected original HUD path; see mainline-minimap-hud-code-source-manifest.json.
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
namespace Zjcs.Official.World.Map13MiniMap
{
public class LanguageGroupConfig : ScriptableObject
{
	public MonoDictionary<LanguageGroup, List<LanguageType>> LanguageGroupMappingDict;

	private Dictionary<LanguageType, LanguageGroup> mLanguage2GroupDict;

	public LanguageGroup GetLanguageGroupByType(LanguageType type)
	{
		if (mLanguage2GroupDict == null)
		{
			InitLanguage2GroupDict();
		}
		if (mLanguage2GroupDict.TryGetValue(type, out var value))
		{
			return value;
		}
		return LanguageGroup.Zh;
	}

	private void InitLanguage2GroupDict()
	{
		mLanguage2GroupDict = new Dictionary<LanguageType, LanguageGroup>();
		foreach (KeyValuePair<LanguageGroup, List<LanguageType>> item in LanguageGroupMappingDict)
		{
			LanguageGroup key = item.Key;
			foreach (LanguageType item2 in item.Value)
			{
				mLanguage2GroupDict[item2] = key;
			}
		}
	}
}
}
