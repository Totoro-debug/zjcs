// Original client component; see grid entity runtime source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.BattleStatusPlayables;
using UnityEngine;


namespace Zjcs.Official.BattleGridItemEntities
{
public class MyFightRound : MyComponent<IBattleGridComponentViewData>
{
	[SerializeField]
	[Tooltip("是否在回合进度条上显示")]
	private bool mIsShowOnRoundProgress = true;

	public bool IsShowOnRoundProgress => mIsShowOnRoundProgress;
}
}
