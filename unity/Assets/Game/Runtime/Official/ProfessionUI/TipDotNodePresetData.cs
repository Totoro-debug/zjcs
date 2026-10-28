// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public class TipDotNodePresetData
{


	[SerializeField]
	public int Priority = 1;


	[SerializeField]
	public ETipDotLevelType PresetTipLevel = ETipDotLevelType.Important;

	[SerializeField]
	public bool NeedAllShow;

	[SerializeField]
	public bool IsRoot;

	[SerializeField]
	public List<ETipDotLevelType> IgnoreLevels;

	[SerializeField]
	public bool IsReverse;

	public virtual TipDotNodePresetData Clone()
	{
		TipDotNodePresetData tipDotNodePresetData = new TipDotNodePresetData();
		tipDotNodePresetData.Fill(this);
		return tipDotNodePresetData;
	}

	public virtual void Fill(TipDotNodePresetData obj)
	{
		Priority = obj.Priority;
		PresetTipLevel = obj.PresetTipLevel;
		NeedAllShow = obj.NeedAllShow;
		IsRoot = obj.IsRoot;
		IgnoreLevels = obj.IgnoreLevels == null ? null : new List<ETipDotLevelType>(obj.IgnoreLevels);
		IsReverse = obj.IsReverse;
	}
}
}
