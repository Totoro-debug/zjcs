// Selected official game implementation; see partner-tipdot-runtime-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.PartnersUI
{
public class TipDotBanManager : ITipDotBanManager
{
	private readonly ITipDotBanStrategy[] mStrategies;

	public TipDotBanManager(IEnumerable<ITipDotBanStrategy> strategies)
	{
		if (strategies == null)
		{
			mStrategies = Array.Empty<ITipDotBanStrategy>();
			return;
		}
		List<ITipDotBanStrategy> list = new List<ITipDotBanStrategy>();
		foreach (ITipDotBanStrategy strategy in strategies)
		{
			if (strategy != null)
			{
				list.Add(strategy);
			}
		}
		mStrategies = ((list.Count > 0) ? list.ToArray() : Array.Empty<ITipDotBanStrategy>());
	}

	public bool IsTipDotBan(TipDotNodeKey nodeKey)
	{
		ITipDotBanStrategy[] array = mStrategies;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].IsTipDotBan(nodeKey))
			{
				return true;
			}
		}
		return false;
	}

	public void ClearTipDotBan()
	{
		ITipDotBanStrategy[] array = mStrategies;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].ClearTipDotBan();
		}
	}
}
}
