// Migrated original application fade handler; see Map13 source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using System;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityFadeHandlers
{
[System.Serializable]
public abstract class EcEntityFadeOutEffectHandlerBase : ECPlayableBaseHandler
{
	protected GameObject mSrcGo;

	protected IMap13EntityFadeOutBinding mMyEntity;

	protected bool mPlaying;

	protected bool mVisiable;

	public override void Init(IECPlayableSupporter ecPlayableSupporterView, IServiceProvider serviceProvider)
	{
		base.Init(ecPlayableSupporterView, serviceProvider);
		mSrcGo = ecPlayableSupporterView.Source;
		mMyEntity = (IMap13EntityFadeOutBinding)mSrcGo.GetComponent<ECViewEntityBinding>();
		IMap13EntityFadeOutBinding myEntity = mMyEntity;
		myEntity.FadeOutAction = (Func<bool>)Delegate.Combine(myEntity.FadeOutAction, new Func<bool>(FadeOut));
	}

	public override void Release()
	{
		IMap13EntityFadeOutBinding myEntity = mMyEntity;
		myEntity.FadeOutAction = (Func<bool>)Delegate.Remove(myEntity.FadeOutAction, new Func<bool>(FadeOut));
		mMyEntity = null;
		base.Release();
	}

	public override void Visible()
	{
		base.Visible();
		mVisiable = true;
	}

	public override void Invisible()
	{
		mVisiable = false;
		base.Invisible();
	}

	protected virtual void Play()
	{
		if (!mPlaying)
		{
			PlayImpl();
			mPlaying = true;
		}
	}

	protected virtual void Stop()
	{
		mPlaying = false;
		StopImpl();
	}

	protected abstract bool PrePlayCheck();

	protected abstract bool CheckEnd();

	protected abstract void PlayImpl();

	protected abstract void StopImpl();

	private bool FadeOut()
	{
		if (!PrePlayCheck())
		{
			return false;
		}
		if (!mVisiable)
		{
			Stop();
			return true;
		}
		Play();
		if (!CheckEnd())
		{
			return false;
		}
		Stop();
		return true;
	}
}

}
