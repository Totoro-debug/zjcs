// Migrated official client source; see entity world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityWorldStages
{
public class ECWorldStageCfgNormalProvider : IECWorldStageStateCfgBaseProvider
{
	[SerializeField]
	private List<WorldStageStateCfg> mStateCfgs;

	public WorldStageStateCfg GetStageStateCfg(IServiceProvider serviceProvider, ECWorldStageStateData worldStageComp)
	{
		int state = worldStageComp.State;
		if (state >= mStateCfgs.Count || state < 0)
		{
			return null;
		}
		return mStateCfgs[state];
	}

	public void Release()
	{
		foreach (WorldStageStateCfg mStateCfg in mStateCfgs)
		{
			mStateCfg.InteractionOperation.InteractionOperation?.Release();
		}
	}
}

}
