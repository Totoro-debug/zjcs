// Original client character view; see mainline character views source receipt.
using Zjcs.Official.EntityLifecycle;
using System;
using Object = UnityEngine.Object;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityClientComponents
{
public class MyCharacter : MyComponent<CharacterComponentData>
{
	public AnimationController myAnimationController;

	public GameObject PlayerOfflineObj;

	[Tooltip("中途加入战斗时播放的出场光效")]
	public GameObject PlayerShowUpPlayable;

	private IMyPlayableManager mMyPlayableManager { get; set; }

	private Func<bool> mJoinInMiddle;

    public void Configure(IMyPlayableManager playables, Func<bool> joinInMiddle)
    {
        mMyPlayableManager = playables ?? throw new ArgumentNullException(nameof(playables));
        mJoinInMiddle = joinInMiddle ?? throw new ArgumentNullException(nameof(joinInMiddle));
    }

	protected override void Awake()
	{
		base.Awake();
		if ((Object)(object)myAnimationController == (Object)null)
		{
			myAnimationController = ((Component)this).GetComponent<AnimationController>();
		}
	}

	public override void OnStart(bool newEntity)
	{
		Component.OnPlayerOnlineStateUpdate += OnCharacterOnlineStateUpdate;
		OnCharacterOnlineStateUpdate(online: true);
		base.OnStart(newEntity);
		if (!((Object)(object)PlayerShowUpPlayable == (Object)null) && mJoinInMiddle())
		{
			mMyPlayableManager.Play(PlayerShowUpPlayable, ((Component)this).gameObject, ((Component)this).gameObject, 1f, (GameObject)null, (Dictionary<string, object>)null, (IMyPlayableNeedStop)null);
		}
	}

	public override void OnEnd(object userData)
	{
		base.OnEnd(userData);
		if (Component != null)
		{
			Component.OnPlayerOnlineStateUpdate -= OnCharacterOnlineStateUpdate;
		}
	}

	private void OnCharacterOnlineStateUpdate(bool online)
	{
		if ((Object)(object)PlayerOfflineObj != (Object)null)
		{
			PlayerOfflineObj.SetActive(!Component.Online);
		}
	}
}

}
