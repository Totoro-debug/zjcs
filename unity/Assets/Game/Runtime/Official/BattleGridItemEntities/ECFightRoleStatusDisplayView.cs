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
[System.Serializable]
public class ECFightRoleStatusDisplayView : ECViewBase
{
	[Header("状态栏设置")]
	[SerializeField]
	private Object mStatusItem;

	[SerializeField]
	private Transform mStatusItemSlot;

	[SerializeField]

	[Range(1f, 20f)]
	private int mMaxCountOfRow = 1;

	[SerializeField]

	[Range(-1f, 10f)]
	[Tooltip("-1无限")]
	private int mMaxRowCount = 1;

	private IBattleStatusDisplayController mStatusController;


	public IBattleGridRoleViewRoot mFightRoot { get; set; }

	public IBattleStatusDisplayFactory StatusDisplayFactory { get; set; }

	protected override void Load()
	{
		base.Load();
		MyFightStatusAgent componentInParent = ((Component)mOwner.transform).GetComponentInParent<MyFightStatusAgent>(true);
		if (!((Object)(object)componentInParent == (Object)null))
		{
			StartStatusDisplay(componentInParent);
			mEntity.GetComponent<IBattleEntityEndEvents>().Ended += Entity_OnEnd;
		}
	}

	private void Entity_OnEnd(ECViewEntityData entity, object mySerializable)
	{
		EndStatusDisplay();
	}

	protected override void Unload()
	{
		EndStatusDisplay();
		if (mEntity != null)
		{
			mEntity.GetComponent<IBattleEntityEndEvents>().Ended -= Entity_OnEnd;
		}
		base.Unload();
	}

	private void StartStatusDisplay(MyFightStatusAgent agent)
	{
		if (!((Object)(object)mStatusItem == (Object)null) && !((Object)(object)mStatusItemSlot == (Object)null))
		{
			mStatusController = StatusDisplayFactory.Create(agent, mStatusItem, mStatusItemSlot, mFightRoot, mMaxCountOfRow, mMaxRowCount);
			mStatusController.Start();
		}
	}

	private void EndStatusDisplay()
	{
		if (mStatusController != null)
		{
			mStatusController.End();
			mStatusController = null;
		}
	}
}
}
