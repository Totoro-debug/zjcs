// Migrated official client source; see entity world-stage receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using System;
using UnityEngine;

namespace Zjcs.Official.EntityWorldStages
{
public class ECPlayableWorldStageHandler : ECPlayableBaseHandler
{
	[Tooltip("是否在播放StartPlayable时屏蔽视野剔除")]
	[SerializeField]
	private bool mIsNotCutOffInStartPlayable;

	private IECViewMapRootLifetime mMapRootManagerProxy;

	private ECWorldStageView mECWorldStageView;

	private Action<ECWorldStageStateData, int, object> mOnStateUpdate;

	private MyPlayable mStatePlayable;

	private bool mIsVisible;

	private bool mIsPlayStart;

	public override void Init(IECPlayableSupporter playableSupporterView, IServiceProvider serviceProvider)
	{
		base.Init(playableSupporterView, serviceProvider);
		mMapRootManagerProxy = (IECViewMapRootLifetime)ServiceProvider.GetService(typeof(IECViewMapRootLifetime));
		mECWorldStageView = playableSupporterView.GetECView<ECWorldStageView>();
		if (mOnStateUpdate == null)
		{
			mOnStateUpdate = OnStateUpdate;
		}
		mECWorldStageView.OnStateUpdate += mOnStateUpdate;
		StopPlayable();
		mIsPlayStart = false;
		mIsVisible = false;
	}

	public override void Visible()
	{
		base.Visible();
		if (!mIsVisible)
		{
			mIsVisible = true;
			PlayLoopPlayable();
		}
	}

	public override void Invisible()
	{
		if (!mMapRootManagerProxy.Destroying)
		{
			StopPlayable();
		}
		mIsVisible = false;
		base.Invisible();
	}

	public override void Release()
	{
		mECWorldStageView.OnStateUpdate -= mOnStateUpdate;
		if (!mMapRootManagerProxy.Destroying)
		{
			StopPlayable();
		}
		else
		{
			DetachPlayable();
		}
		mIsVisible = false;
		base.Release();
	}

	private void OnStateUpdate(ECWorldStageStateData worldStageComponent, int preState, object data)
	{
		if (!mIsVisible)
		{
			return;
		}
		StopPlayable();
		WorldStageStateCfg curStateCfg = mECWorldStageView.GetCurStateCfg();
		if (curStateCfg == null)
		{
			return;
		}
		if ((Object)(object)curStateCfg.myStartPlayable != (Object)null)
		{
			if (mIsNotCutOffInStartPlayable)
			{
				mECPlayableSupporterView.ClientVisibilitySupporter.SetForceVisible(forceVisible: true);
			}
			GameObject source = mECPlayableSupporterView.Source;
			mStatePlayable = mECPlayableSupporterView.PlayPlayable(curStateCfg.myStartPlayable, source, source);
			if ((Object)(object)mStatePlayable != (Object)null)
			{
				mIsPlayStart = true;
				mStatePlayable.OnStop += new Action<MyPlayable>(OnStartPlayableStop);
			}
		}
		else
		{
			PlayLoopPlayable(curStateCfg);
		}
	}

	private void OnStartPlayableStop(MyPlayable playable)
	{
		if (mIsNotCutOffInStartPlayable)
		{
			mECPlayableSupporterView.ClientVisibilitySupporter.SetForceVisible(forceVisible: false);
		}
		mIsPlayStart = false;
		playable.OnStop -= new Action<MyPlayable>(OnStartPlayableStop);
		PlayLoopPlayable();
	}

	private void PlayLoopPlayable()
	{
		WorldStageStateCfg curStateCfg = mECWorldStageView.GetCurStateCfg();
		if (curStateCfg == null)
		{
			StopPlayable();
		}
		else
		{
			PlayLoopPlayable(curStateCfg);
		}
	}

	private void StopPlayable()
	{
		MyPlayable myPlayable = mStatePlayable;
		DetachPlayable();
		myPlayable?.Stop();
	}

	private void DetachPlayable()
	{
		if (!((Object)(object)mStatePlayable == (Object)null))
		{
			if (mIsPlayStart)
			{
				mStatePlayable.OnStop -= new Action<MyPlayable>(OnStartPlayableStop);
				mIsPlayStart = false;
			}
			mStatePlayable = null;
		}
	}

	private void PlayLoopPlayable(WorldStageStateCfg cfg)
	{
		StopPlayable();
		if ((Object)(object)cfg.myLoopPlayable != (Object)null)
		{
			GameObject source = mECPlayableSupporterView.Source;
			mStatePlayable = mECPlayableSupporterView.PlayPlayable(cfg.myLoopPlayable, source, source);
		}
	}
}

}
