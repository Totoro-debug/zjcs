// Original client source; see status round-start view source receipt.
using System;

namespace Zjcs.Official.BattleGridItemEntities
{
public class MyFightStatusRoundStart : MyFightStatusBase<BattleStatusRoundStartViewData>
{
	private Action mUnsubscribe;

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		Component.OnRemainCountUpdate += OnRemainCountUpdate;
		mUnsubscribe = () => Component.OnRemainCountUpdate -= OnRemainCountUpdate;
		OnRemainCountUpdate(Component);
	}

	public override void OnEnd(object userData)
	{
		mUnsubscribe?.Invoke();
		mUnsubscribe = null;
		base.OnEnd(userData);
	}

	private void OnRemainCountUpdate(BattleStatusRoundStartViewData component)
	{
		if (myStatus.IsIconShowCustomCount)
		{
			myStatus.IconShowCustomCount = Component.GetRemainCount();
		}
	}
}

}
