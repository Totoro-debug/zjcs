// Complete original client lifecycle class; see entity lifecycle source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.EntityLifecycle
{
public class MyEntity : MonoBehaviour, IGameObjectPoolItem
{
	public Func<bool> FadeOutAction;

	private MyEntityBindPreEventDelegate mBindPreEvent;

	private MyEntityBindEventDelegate mBindEvent;

	private MyEntityStartEventDelegate mStartEvent;

	private MyEntityStartPostEventDelegate mStartPostEvent;

	private MyEntityRemoveEventDelegate mRemoveEvent;

	private MyEntityRemovePreEventDelegate mRemovePreEvent;

	private MyEntityPrepareReplaceEventDelegate mPrepareReplaceEvent;

	private MyEntityReplaceEventDelegate mReplaceEvent;

	public MyContainer myContainer;

	public ECViewEntityData Entity;

	private MyTransform _Transform;

	private MyGridTransform _GridTransform;

	private IMyVisibility mVisibility;

	private ILogger mLogger;

	private IClientVisibilitySupporter mClientVisibilitySupporter;

	private MyComponent[] mComponents;

	public MyEntityBindPreEventDelegate BindPreEvent
	{
		get
		{
			return mBindPreEvent ?? (mBindPreEvent = new MyEntityBindPreEventDelegate());
		}
		set
		{
			mBindPreEvent = value;
		}
	}

	public MyEntityBindEventDelegate BindEvent
	{
		get
		{
			return mBindEvent ?? (mBindEvent = new MyEntityBindEventDelegate());
		}
		set
		{
			mBindEvent = value;
		}
	}

	public MyEntityStartEventDelegate StartEvent
	{
		get
		{
			return mStartEvent ?? (mStartEvent = new MyEntityStartEventDelegate());
		}
		set
		{
			mStartEvent = value;
		}
	}

	public MyEntityStartPostEventDelegate StartPostEvent
	{
		get
		{
			return mStartPostEvent ?? (mStartPostEvent = new MyEntityStartPostEventDelegate());
		}
		set
		{
			mStartPostEvent = value;
		}
	}

	public MyEntityRemovePreEventDelegate RemovePreEvent
	{
		get
		{
			return mRemovePreEvent ?? (mRemovePreEvent = new MyEntityRemovePreEventDelegate());
		}
		set
		{
			mRemovePreEvent = value;
		}
	}

	public MyEntityRemoveEventDelegate RemoveEvent
	{
		get
		{
			return mRemoveEvent ?? (mRemoveEvent = new MyEntityRemoveEventDelegate());
		}
		set
		{
			mRemoveEvent = value;
		}
	}

	public MyEntityPrepareReplaceEventDelegate PrepareReplaceEvent
	{
		get
		{
			return mPrepareReplaceEvent ?? (mPrepareReplaceEvent = new MyEntityPrepareReplaceEventDelegate());
		}
		set
		{
			mPrepareReplaceEvent = value;
		}
	}

	public MyEntityReplaceEventDelegate ReplaceEvent
	{
		get
		{
			return mReplaceEvent ?? (mReplaceEvent = new MyEntityReplaceEventDelegate());
		}
		set
		{
			mReplaceEvent = value;
		}
	}

	public bool myStarted { get; set; }

	public bool myNewEntity { get; set; }

	public MyTransform myTransform
	{
		get
		{
			if ((Object)(object)_Transform == (Object)null)
			{
				((Component)this).TryGetComponent<MyTransform>(out _Transform);
			}
			return _Transform;
		}
	}

	public MyGridTransform myGridTransform
	{
		get
		{
			if ((Object)(object)_GridTransform == (Object)null)
			{
				((Component)this).TryGetComponent<MyGridTransform>(out _GridTransform);
			}
			return _GridTransform;
		}
	}

	public IMyVisibility Visibility
	{
		get
		{
			if (mVisibility == null)
			{
				mVisibility = ((Component)this).GetComponent<IMyVisibility>();
			}
			return mVisibility;
		}
	}

	public ILogger Logger => mLogger ?? (mLogger = (((Object)(object)myContainer != (Object)null) ? myContainer.Logger : MyContainer.CreateLogger("MyEntityWithoutContainer_" + ((Object)this).name)));

	public IClientVisibilitySupporter ClientVisibilitySupporter => mClientVisibilitySupporter;

	public EcEntityIdentifier Id => Entity.Id;

	public int ClassId => Entity.ClassId;

	public IReadOnlyCollection<MyComponent> Components
	{
		get
		{
			if (mComponents == null)
			{
				mComponents = ((Component)this).GetComponents<MyComponent>();
			}
			return mComponents;
		}
	}

	public void SetClientVisibilitySupporter(IClientVisibilitySupporter clientVisibilitySupporter)
	{
		mClientVisibilitySupporter = clientVisibilitySupporter;
	}

	public void Bind(MyContainer container, ECViewEntityData entity, bool newEntity)
	{
		BindPreEvent.FireEvent(entity);
		myContainer = container;
		Entity = entity;
		myNewEntity = newEntity;
		foreach (MyComponent component in Components)
		{
			component.Bind();
		}
		BindEvent.FireEvent();
	}

	public void MyStart()
	{
		myStarted = true;
		foreach (MyComponent component in Components)
		{
			try
			{
				component.OnStart(myNewEntity);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		StartEvent.FireEvent(this, myNewEntity);
		StartPostEvent.FireEvent(this);
	}

	private void OnDestroy()
	{
		myContainer = null;
		BindPreEvent.Clear();
		BindEvent.Clear();
		StartEvent.Clear();
		StartPostEvent.Clear();
		RemoveEvent.Clear();
		PrepareReplaceEvent.Clear();
		ReplaceEvent.Clear();
	}

	public void End(object userData)
	{
		foreach (MyComponent component in Components)
		{
			try
			{
				component.OnEnd(userData);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}

	public bool IsNeedFadeOut()
	{
		if (myContainer.Container.Destroying)
		{
			return false;
		}
		return FadeOutAction != null;
	}

	public IEnumerator FadeOut()
	{
		if (FadeOutAction != null)
		{
			yield return (object)new WaitUntil(FadeOutAction);
		}
	}

	public void Remove(object userData)
	{
		foreach (MyComponent component in Components)
		{
			try
			{
				component.OnRemove(userData);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
		RemovePreEvent.FireEvent(this);
		RemoveEvent.FireEvent(this);
	}

	public void PrepareReplace()
	{
		PrepareReplaceEvent.FireEvent(this);
	}

	public void Replace(EcEntityIdentifier preEntId)
	{
		ReplaceEvent.FireEvent(this, preEntId);
	}

	public void ImmediateToEnd()
	{
		foreach (MyComponent component in Components)
		{
			try
			{
				component.OnImmediateToEnd();
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}

	public void OnGameObjectPoolReset()
	{
		myStarted = false;
		myNewEntity = false;
		myContainer = null;
		Entity = null;
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
