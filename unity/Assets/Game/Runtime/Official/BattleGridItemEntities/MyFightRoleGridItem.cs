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
using System.Collections.Generic;
using UnityEngine;


namespace Zjcs.Official.BattleGridItemEntities
{
public class MyFightRoleGridItem : MyComponent<BattleGridItemViewData>, IFightGetUsePlayable
{
	public GameObject GridFriendEffectPlayable;

	public GameObject GridEnemyEffectPlayable;

	public GameObject Playable;

	private MyPlayable mGridFriendEffectPlayable;

	private MyPlayable mGridEnemyEffectPlayable;

	private MyPlayable mPlayable;


	public IMyPlayableManager mMyPlayableManager { get; set; }


	public IBattleGridRoleViewRoot mFightRoot { get; set; }

	public override void OnStart(bool newEntity)
	{
		base.OnStart(newEntity);
		if (mFightRoot.IsPvpStage && (Object)(object)mFightRoot.Character != (Object)null)
		{
			bool flag = false;
			if ((bool)((Object)(object)mFightRoot.Character))
			{
				flag = Component.OwnerEntId == mFightRoot.Character.myEntity.Id;
			}
			if (!flag)
			{
				int? component = mFightRoot.FactionOf(mFightRoot.Character.myEntity);
				int num = 0;
				if (component.HasValue)
				{
					num = component.Value;
				}
				foreach (MyFightRoleCharacter myCharacter in mFightRoot.Characters)
				{
					if (!((Object)(object)myCharacter == (Object)null) && !(myCharacter.myEntity.Id != Component.OwnerEntId))
					{
						int? component2 = mFightRoot.FactionOf(myCharacter.myEntity);
						if (component2.HasValue && component2.Value == num)
						{
							flag = true;
							break;
						}
					}
				}
			}
			if ((bool)((Object)(object)GridFriendEffectPlayable) & flag)
			{
				mGridFriendEffectPlayable = mMyPlayableManager.Play(GridFriendEffectPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
			}
			if ((bool)((Object)(object)GridEnemyEffectPlayable) && !flag)
			{
				mGridEnemyEffectPlayable = mMyPlayableManager.Play(GridEnemyEffectPlayable, ((Component)this).gameObject, ((Component)this).gameObject);
			}
		}
		if ((Object)(object)Playable != (Object)null)
		{
			mPlayable = mMyPlayableManager.Play(Playable, ((Component)this).gameObject, ((Component)this).gameObject);
		}
	}

	public override void OnEnd(object userData)
	{
		if ((bool)((Object)(object)mGridFriendEffectPlayable))
		{
			mGridFriendEffectPlayable.Stop();
			mGridFriendEffectPlayable = null;
		}
		if ((bool)((Object)(object)mGridEnemyEffectPlayable))
		{
			mGridEnemyEffectPlayable.Stop();
			mGridEnemyEffectPlayable = null;
		}
		if ((Object)(object)mPlayable != (Object)null)
		{
			mPlayable.Stop();
		}
		base.OnEnd(userData);
	}

	public void TryGetPlayables(ref HashSet<GameObject> playables)
	{
		if (playables != null)
		{
			if ((Object)(object)GridFriendEffectPlayable != (Object)null)
			{
				playables.Add(GridFriendEffectPlayable);
			}
			if ((Object)(object)GridEnemyEffectPlayable != (Object)null)
			{
				playables.Add(GridEnemyEffectPlayable);
			}
		}
	}
}
}
