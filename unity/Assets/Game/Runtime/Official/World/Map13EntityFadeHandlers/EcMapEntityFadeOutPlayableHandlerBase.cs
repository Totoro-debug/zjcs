// Migrated original application fade handler; see Map13 source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityFadeHandlers
{
[System.Serializable]
public abstract class EcMapEntityFadeOutPlayableHandlerBase : EcEntityFadeOutEffectHandlerBase
{
	public GameObject FadeOutPlayable;

	[Tooltip("当地图完全显示出来的时候才播放")]
	public bool WhenMapFront = true;

	protected MyPlayable myPlayable;

	protected override bool PrePlayCheck()
	{
		if ((Object)(object)FadeOutPlayable == (Object)null)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
		}
		if (WhenMapFront)
		{
			return IsMapFrontAndShow();
		}
		return true;
	}

	protected override bool CheckEnd()
	{
		if (!((Object)(object)myPlayable == (Object)null))
		{
			return myPlayable.myStatus == MyPlayable.PlayableStatus.Stop;
		}
		return true;
	}

	protected override void PlayImpl()
	{
		PlayPlayable();
	}

	protected virtual void PlayPlayable()
	{
		if ((Object)(object)FadeOutPlayable != (Object)null)
		{
			myPlayable = mECPlayableSupporterView.PlayPlayable(FadeOutPlayable, mSrcGo, mSrcGo);
		}
	}

	protected override void StopImpl()
	{
		if ((Object)(object)myPlayable != (Object)null && myPlayable.myStatus != MyPlayable.PlayableStatus.Stop)
		{
			myPlayable.Stop();
		}
		myPlayable = null;
	}

	private bool IsMapFrontAndShow()
	{
		IMap13FadeMapState service = (IMap13FadeMapState)ServiceProvider.GetService(typeof(IMap13FadeMapState));
		if (!service.Initialized || service.LoadingPanel == null)
		{
			return false;
		}
		if (service.IsFront)
		{
			return service.LoadingPanel.SplashStatus == SplashStatus.Hide;
		}
		return false;
	}
}

}
