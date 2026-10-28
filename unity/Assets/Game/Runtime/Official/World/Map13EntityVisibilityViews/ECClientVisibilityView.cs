// Migrated original application/read-query code; see Map13 visibility-view receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.World.Map13EntityRendering;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace Zjcs.Official.World.Map13EntityVisibilityViews
{
[Serializable]
public class ECClientVisibilityView : ECViewBase, IEcEntityClientVisibilityUpdatedEventReceiver, IEventReceiver, IMyVisibility
{
	protected bool mVisible = true;

	private Map13VisibilityData mVisibilityComponent;

	private List<IClientVisibilityHandler> mHandlers;

	private bool mIsStart;

	private ECViewEntityBinding mMyEntity;

	private Action<bool> mOnHandlerVisibleUpdate;

	public bool IsVisible
	{
		get
		{
			if (!mIsStart)
			{
				return false;
			}
			if (mEntityFadeAnimationBridge == null)
			{
				return mVisible;
			}
			if (mClientVisibilitySupporter != null && !mClientVisibilitySupporter.IsVisible)
			{
				return false;
			}
			if (mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.IsIn)
			{
				return mVisible;
			}
			return false;
		}
	}

	private ClientVisibilityType mRequestType => mVisibilityComponent.ClientVisibilityType;

	public IEntityClientVisibilityHandlerFactory EntityClientVisibilityHandlerFactory { get; set; }

	public event Action OnVisibilityChanged;

	protected override void Load()
	{
		base.Load();
		if (mOnHandlerVisibleUpdate == null)
		{
			mOnHandlerVisibleUpdate = VisibleUpdate;
		}
		mIsStart = true;
		mMyEntity = mOwner.GetComponentInParent<ECViewEntityBinding>();
		mVisibilityComponent = GetComponent<Map13VisibilityData>();
		if (mEntityFadeAnimationBridge != null)
		{
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.AddOnFadeOutStartEvent(FadeInOutChanged);
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.AddOnFadeInCompletedEvent(FadeInOutChanged);
		}
		mVisibilityComponent.OnClientVisibilityUpdate += (IEcEntityClientVisibilityUpdatedEventReceiver)this;
		HandleVisibilityRequest();
	}

	public void EntityClientVisibilityComponent_Updated(Map13VisibilityData visibilityComponent)
	{
		HandleVisibilityRequest();
	}

	private void HandleVisibilityRequest()
	{
		ClearHandler();
		CreateHanlder();
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

	private void CreateHanlder()
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
		IClientVisibilityHandler component = mMyEntity.Entity.GetComponent<IClientVisibilityHandler>();
		if (component != null)
		{
			mHandlers.Add(component);
		}
		foreach (IClientVisibilityHandler mHandler in mHandlers)
		{
			mHandler.Init(mMyEntity.Entity, !mIsStatic);
			mHandler.OnVisibleChanged += mOnHandlerVisibleUpdate;
		}
	}

	private void ClearHandler()
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
		mVisible = visible;
		((IMap13EntityVisibilityBinding)mMyEntity).ClientVisibilitySupporter?.SetEntityVisible(((Object)mOwner).GetInstanceID(), mVisible);
		if (mVisible && mEntityFadeAnimationBridge != null && mVisibilityComponent.IsShowNeedFadeIn)
		{
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.FadeIn(-1f, hasEvent: true, 1);
		}
		OnVisibilityChanged?.Invoke();
	}

	private void FadeInOutChanged()
	{
		OnVisibilityChanged?.Invoke();
	}

	protected override void Unload()
	{
		mIsStart = false;
		mVisible = true;
		mVisibilityComponent.OnClientVisibilityUpdate -= (IEcEntityClientVisibilityUpdatedEventReceiver)this;
		ClearHandler();
		if (mEntityFadeAnimationBridge != null)
		{
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.RemoveOnFadeOutStartEvent(FadeInOutChanged);
			mEntityFadeAnimationBridge.EntityFadeAnimationSupporter.RemoveOnFadeInCompletedEvent(FadeInOutChanged);
		}
		base.Unload();
	}
}

}
