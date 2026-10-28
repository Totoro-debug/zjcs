namespace Zjcs.Official.ProfessionUI
{
using System;
using System.Collections.Generic;

[Serializable]
public class AppearancePartConfig
{
	public SkinType SkinType;

	public List<PartAnimConfig> AnimConfigList;

	private Dictionary<string, PartAnimConfig> mSourceKeyAnimDict;

	private Dictionary<string, List<PartAnimConfig>> mTargetKeyAnimDict;

	public Dictionary<string, PartAnimConfig> SourceKeyAnimDict
	{
		get
		{
			if (mSourceKeyAnimDict == null && AnimConfigList != null)
			{
				mSourceKeyAnimDict = new Dictionary<string, PartAnimConfig>(AnimConfigList.Count);
				foreach (PartAnimConfig animConfig in AnimConfigList)
				{
					mSourceKeyAnimDict.Add(animConfig.SourceAnim, animConfig);
				}
			}
			return mSourceKeyAnimDict;
		}
	}

	public Dictionary<string, List<PartAnimConfig>> TargetKeyAnimDict
	{
		get
		{
			if (mTargetKeyAnimDict == null && AnimConfigList != null)
			{
				mTargetKeyAnimDict = new Dictionary<string, List<PartAnimConfig>>(AnimConfigList.Count);
				foreach (PartAnimConfig animConfig in AnimConfigList)
				{
					if (!mTargetKeyAnimDict.TryGetValue(animConfig.TargetAnim, out var value))
					{
						value = new List<PartAnimConfig>(1);
						mTargetKeyAnimDict.Add(animConfig.TargetAnim, value);
					}
					value.Add(animConfig);
				}
			}
			return mTargetKeyAnimDict;
		}
	}
}

}
