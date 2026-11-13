namespace Zjcs.Official.BattleThemeUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public struct AnimationClipData
{
	public AnimationClip Clip;

	public int LoopTimes;

	public bool Valid
	{
		get
		{
			if ((Object)(object)Clip != (Object)null)
			{
				return !string.IsNullOrEmpty(((Object)Clip).name);
			}
			return false;
		}
	}
}

}
