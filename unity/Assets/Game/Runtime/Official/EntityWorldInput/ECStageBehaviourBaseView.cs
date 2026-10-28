// Original client/shared input source; see world input source receipt.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityStageOperations;
using Zjcs.Official.EntityTipDotViews;
using Zjcs.Official.LegacyWorldStages;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using WorldStageComponent = Zjcs.Official.LegacyWorldStages.LegacyWorldStageComponentData;
using EntityOwnershipType = Zjcs.Official.EntityTipDotViews.EntityOwnershipType;
using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;
namespace Zjcs.Official.EntityWorldInput
{

public abstract class ECStageBehaviourBaseView : ECViewBase, IMyStageBehaviour
{
	[SerializeField]
	private GameObject mAllowTeamNode;

	public abstract AwardDialogType AwardDialogType { get; }

	public abstract float DelayTime { get; }

	public abstract int GetMonsterId();

	public abstract object GetMonsterList();

	public abstract int GetCurrentStageId();

	public IWorldStageBehaviourQueries StageQueries { get; set; }
	protected virtual void Init()
	{
		if ((Object)(object)mAllowTeamNode != (Object)null)
		{
			int value = (GetComponent<IStageBehStage>()?.GetStageId(0)).Value;
			WorldInputStageInfo stageInfo = StageQueries.GetStageInfo(value);
			if (stageInfo != null)
			{
				mAllowTeamNode.SetActive(stageInfo.SupportTeam);
			}
			else
			{
				mAllowTeamNode.SetActive(false);
			}
		}
	}
}

}
