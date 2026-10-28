// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public abstract class ClientVisibilitySupporterBase : IClientVisibilitySupporter, IMyPlayableNeedStop, IMyEntityStartPostEventReceiver, IEventReceiver, IMyEntityRemovePreEventReceiver
{
	private IMyEntityViewModelControler mMyEntityViewModelControler;

	private HashSet<int> mModelHidenRequestSet = new HashSet<int>();

	private EntityFadeAnimationSupporter mFadeAnimationSupporter;

	private bool mIsVisible = true;

	private bool mIsStart;

	private bool mIsForceVisible;

	private ECViewEntityBinding mOwner;

	private Transform mTransform;

	public bool IsVisible
	{
		get
		{
			if (mIsVisible)
			{
				return mIsStart;
			}
			return false;
		}
	}

	protected virtual bool OwnerRootDestroying => false;

	public event Action OnVisibleUpdate;

	public event Action OnShow;

	public event Action OnHide;

	protected ClientVisibilitySupporterBase(SpriteRendererList spriteRendererList, IMyEntityViewModelControler myEntityViewModelControler, Transform transform)
	{
		mMyEntityViewModelControler = myEntityViewModelControler;
		mTransform = transform;
		mFadeAnimationSupporter = new EntityFadeAnimationSupporter(spriteRendererList);
	}

	public void Register(ECViewEntityBinding owner)
	{
		mOwner = owner;
		owner.SetClientVisibilitySupporter(this);
		mOwner.AddStartPostReceiver((IMyEntityStartPostEventReceiver)this);
		mOwner.AddRemovePreReceiver((IMyEntityRemovePreEventReceiver)this);
	}

	public void Unregister()
	{
		ResetVisibility();
		mFadeAnimationSupporter.ResetFadeAnimation();
		if ((Object)(object)mOwner != (Object)null)
		{
			mOwner.RemoveStartPostReceiver((IMyEntityStartPostEventReceiver)this);
			mOwner.RemoveRemovePreReceiver((IMyEntityRemovePreEventReceiver)this);
			mOwner = null;
		}
	}

	public virtual bool NeedStop()
	{
		if (!IsVisible)
		{
			return !OwnerRootDestroying;
		}
		return false;
	}

	void IMyEntityStartPostEventReceiver.MyEntity_OnStartPost(ECViewEntityBinding entity)
	{
		mIsStart = true;
		if (mIsVisible)
		{
			OnShow?.Invoke();
			OnVisibleUpdate?.Invoke();
		}
	}

	void IMyEntityRemovePreEventReceiver.MyEntity_RemovePre(ECViewEntityBinding entity)
	{
		mIsStart = false;
		if (mIsVisible)
		{
			OnHide?.Invoke();
			OnVisibleUpdate?.Invoke();
		}
	}

	public virtual bool SetEntityVisible(int key, bool isVisible)
	{
		if ((Object)(object)mOwner == (Object)null)
		{
			return false;
		}
		if (isVisible)
		{
			mModelHidenRequestSet.Remove(key);
		}
		else
		{
			mModelHidenRequestSet.Add(key);
		}
		if (mIsForceVisible)
		{
			return false;
		}
		return UpdateVisible();
	}

	public virtual void SetForceVisible(bool forceVisible)
	{
		mIsForceVisible = forceVisible;
		UpdateVisible();
	}

	public void ResetVisibility()
	{
		mModelHidenRequestSet.Clear();
		mIsStart = false;
		mIsForceVisible = false;
		UpdateVisible();
	}

	private bool UpdateVisible()
	{
		bool flag = mIsVisible;
		mIsVisible = mIsForceVisible || mModelHidenRequestSet.Count == 0;
		if (flag == mIsVisible)
		{
			return false;
		}
		bool result = UpdateViewModelOperation();
		if (mIsStart)
		{
			if (mIsVisible)
			{
				OnShow?.Invoke();
			}
			else
			{
				OnHide?.Invoke();
			}
			Action action = OnVisibleUpdate;
			if (action == null)
			{
				return result;
			}
			action();
		}
		return result;
	}

	private bool UpdateViewModelOperation()
	{
		if (OwnerRootDestroying)
		{
			return false;
		}
		if (mMyEntityViewModelControler == null)
		{
			return MyEntityViewModelControlerUtils.DefaultUpdateViewModelNodeShow(mTransform, mIsVisible);
		}
		return mMyEntityViewModelControler.UpdateViewModelNodeShow(mIsVisible);
	}

	public IEntityFadeAnimationSupporter GetFadeAnimationSupporter()
	{
		return mFadeAnimationSupporter;
	}
}

}
