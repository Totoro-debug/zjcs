// Original client component; see first-promotion status runtime source receipt.
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
namespace Zjcs.Official.BattleGridItemEntities
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

public class MyFightStatusShieldPersistent : MyFightStatusShieldBase<BattleStatusShieldViewData, object>
{
	public IMyPlayableManager mMyPlayableManager { get; set; }

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		Component.OnUpdateRemainData += new Action<EcEntityIdentifier, long, long>(OnUpdateRemainData);
	}

	public override void OnEnd(object userData)
	{
		Component.OnUpdateRemainData -= new Action<EcEntityIdentifier, long, long>(OnUpdateRemainData);
		base.OnEnd(userData);
	}

	private void OnUpdateRemainData(EcEntityIdentifier shieldId, long maxValue, long remainValue)
	{
		if ((Object)(object)RunOutPlayable != (Object)null && remainValue == 0L)
		{
			GameObject gameObject = ApplyEntity;
			GameObject gameObject2 = ApplyEntity;
			mRunOutPlayable = mMyPlayableManager.Play(RunOutPlayable, gameObject, gameObject2);
			mRunOutPlayable.OnStop += new Action<MyPlayable>(OnRunOutPlayableStop);
		}
	}
}
}
