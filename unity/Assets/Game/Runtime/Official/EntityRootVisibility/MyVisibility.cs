// Complete original legacy entity visibility; see Map14 source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using IMyVisibility = Zjcs.Official.EntityLifecycle.IMyVisibility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityRootVisibility
{
public class MyVisibility<T> : MyVisibility<T, object>, IEmptyInfoMyComponent
{
}
public class MyVisibility<T, TInfo> : MyComponent<T, TInfo>, IMyVisibility, IEcEntityClientVisibilityUpdatedEventReceiver, IEventReceiver
{
	protected bool mVisible = true;

	private IEntityFadeAnimationBridge mEntityFadeAnimationBridge;

	private List<IClientVisibilityHandler> mHandlers;

	private ECClientVisibilityView mVisibilityView;

	private bool mIsStart;

	private Action<bool> mOnHandlerVisibleUpdate;

	private bool mIsNew;

	public bool IsVisible
	{
		get
		{
			if (!mIsStart)
			{
				return false;
			}
			if (mVisibilityView != null)
			{
				return true;
			}
			if (mEntityFadeAnimationBridge == null)
			{
				return mVisible;
			}
			if (mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.IsIn)
			{
				return mVisible;
			}
			return false;
		}
	}

	public IEntityClientVisibilityHandlerFactory EntityClientVisibilityHandlerFactory { get; set; }

	private ClientVisibilityType mRequestType => ((Map13VisibilityData)(object)Component).ClientVisibilityType;

	public event Action OnVisibilityChanged;

	protected override void Awake()
	{
		mOnHandlerVisibleUpdate = VisibleUpdate;
		mEntityFadeAnimationBridge = ((Component)this).GetComponent<IEntityFadeAnimationBridge>();
	}

	public override void OnStart(bool isNew)
	{
		base.OnStart(isNew);
		mIsStart = true;
		ECViewDriver componentInChildren = ((Component)this).GetComponentInChildren<ECViewDriver>(true);
		if ((Object)(object)componentInChildren != (Object)null)
		{
			mVisibilityView = componentInChildren.GetView<ECClientVisibilityView>();
		}
		if (mVisibilityView == null)
		{
			if (mEntityFadeAnimationBridge != null)
			{
				mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.AddOnFadeOutStartEvent(FadeInOutChanged);
				mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.AddOnFadeInCompletedEvent(FadeInOutChanged);
			}
			((Map13VisibilityData)(object)Component).OnClientVisibilityUpdate += (IEcEntityClientVisibilityUpdatedEventReceiver)this;
			mIsNew = isNew;
			HandleVisibilityRequest();
		}
	}

	public void EntityClientVisibilityComponent_Updated(Map13VisibilityData visibilityComponent)
	{
		HandleVisibilityRequest();
	}

	private void HandleVisibilityRequest()
	{
		ClearHandlers();
		CreateHanlders();
		VisibleUpdate(CheckIsVisible());
	}

	private bool CheckIsVisible()
	{
		if (mHandlers == null || mHandlers.Count == 0)
		{
			return true;
		}
		foreach (IClientVisibilityHandler mHandler in mHandlers)
		{
			if (!mHandler.Visible)
			{
				return false;
			}
		}
		return true;
	}

	private void CreateHanlders()
	{
		ClientVisibilityType clientVisibilityType = mRequestType;
		if (clientVisibilityType == ClientVisibilityType.None)
		{
			return;
		}
		if (mHandlers == null)
		{
			mHandlers = new List<IClientVisibilityHandler>();
		}
		EntityClientVisibilityHandlerFactory.GetHandlers(clientVisibilityType, ref mHandlers);
		foreach (IClientVisibilityHandler mHandler in mHandlers)
		{
			mHandler.Init(myEntity.Entity, mIsNew);
			mHandler.OnVisibleChanged += mOnHandlerVisibleUpdate;
		}
	}

	private void ClearHandlers()
	{
		if (mHandlers == null || mHandlers.Count == 0)
		{
			return;
		}
		foreach (IClientVisibilityHandler mHandler in mHandlers)
		{
			mHandler.OnVisibleChanged -= mOnHandlerVisibleUpdate;
			mHandler.Release();
			EntityClientVisibilityHandlerFactory.ReturnHanlder(mHandler);
		}
		mHandlers.Clear();
	}

	private void VisibleUpdate(bool visible)
	{
		if (mVisible != visible)
		{
			mVisible = visible;
			myEntity.ClientVisibilitySupporter?.SetEntityVisible(((Object)this).GetInstanceID(), mVisible);
			if (mVisible && mEntityFadeAnimationBridge != null && ((Map13VisibilityData)(object)Component).IsShowNeedFadeIn)
			{
				mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.FadeIn(-1f, hasEvent: true, 1);
			}
			OnVisibilityChanged?.Invoke();
		}
	}

	private void FadeInOutChanged()
	{
		OnVisibilityChanged?.Invoke();
	}

	public override void OnEnd(object userData)
	{
		mIsStart = false;
		if (mVisibilityView == null)
		{
			if (mEntityFadeAnimationBridge != null)
			{
				mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.RemoveOnFadeOutStartEvent(FadeInOutChanged);
				mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.RemoveOnFadeInCompletedEvent(FadeInOutChanged);
			}
			((Map13VisibilityData)(object)Component).OnClientVisibilityUpdate -= (IEcEntityClientVisibilityUpdatedEventReceiver)this;
			mVisible = true;
			ClearHandlers();
		}
		base.OnEnd(userData);
	}
}

}
