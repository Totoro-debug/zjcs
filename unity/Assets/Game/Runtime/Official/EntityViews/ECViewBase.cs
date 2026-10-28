// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
public abstract class ECViewBase : IECView
{
	protected ECViewEntityData mEntity;

	private IUpdatable mUpdatable;

	private ILateUpdatable mLateUpdatable;

	private IDisposable mUpdateDisposable;

	private IDisposable mLateUpdateDisposable;

	private IECViewBridge mBridge;

	private Action<ECViewFrameUnit> mUpdateProxyCache;

	private Action<ECViewFrameUnit> mLateUpdateProxyCache;

	int IECView.Order => mOrder;

	IECView IECView.Append => mAppend;

	IEnumerable<IECView> IECView.AppendMulti => mAppendMulti;

	protected virtual int mOrder => int.MaxValue;

	protected virtual IECView mAppend => null;

	protected virtual IEnumerable<IECView> mAppendMulti => null;

	protected CancellationToken mReleaseCancellationToken => mBridge.DestroyCancellationToken;

	protected GameObject mOwner => mBridge.GameObject;

	protected bool mIsStatic { get; private set; }

	protected int mClassId => mEntity.ClassId;

	protected IServiceProvider mServiceProvider => mBridge.ServiceProvider;

	protected EcEntityIdentifier mEntityIdentifier => mEntity.Id;

	protected IClientVisibilitySupporter mClientVisibilitySupporter => mBridge.ClientVisibilitySupporter;

	protected IEntityFadeAnimationBridge mEntityFadeAnimationBridge => mBridge.EntityFadeAnimationBridge;

	void IECView.Init(IECViewBridge bridge)
	{
		mBridge = bridge;
	}

	void IECView.Load(ECViewEntityData entity, bool isStatic)
	{
		RegisterLoop();
		mEntity = entity;
		mIsStatic = isStatic;
		try
		{
			Load();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	void IECView.OnShow()
	{
		try
		{
			OnShow();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	void IECView.OnHide()
	{
		try
		{
			OnHide();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	void IECView.Unload()
	{
		try
		{
			Unload();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		mEntity = null;
		ClearLoopDisposable();
	}

	void IECView.OnApplicationPause(bool pause)
	{
		try
		{
			OnApplicationPause(pause);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	protected virtual void Load()
	{
	}

	protected virtual void OnShow()
	{
	}

	protected virtual void OnHide()
	{
	}

	protected virtual void Unload()
	{
	}

	protected virtual void OnApplicationPause(bool pause)
	{
	}

	protected T GetComponent<T>()
	{
		if (mEntity == null)
		{
			return default;
		}
		return mEntity.GetComponent<T>();
	}

	protected T GetView<T>() where T : ECViewBase
	{
		return mBridge.GetView<T>();
	}

	protected long StartCoroutine(IEnumerator execution)
	{
		return mBridge.StartCoroutine(execution);
	}

	protected void StopCoroutine(long coroutineId)
	{
		mBridge.StopCoroutine(coroutineId);
	}

	private void RegisterLoop()
	{
		ClearLoopDisposable();
		if (this is IUpdatable updatable)
		{
			mUpdatable = updatable;
			mUpdateDisposable = ECViewServices.Frames(mServiceProvider).SubscribeUpdate(UpdateProxy);
		}
		if (this is ILateUpdatable lateUpdatable)
		{
			mLateUpdatable = lateUpdatable;
			mLateUpdateDisposable = ECViewServices.Frames(mServiceProvider).SubscribePreLateUpdate(LateUpdateProxy);
		}
	}

	private void UpdateProxy(ECViewFrameUnit _)
	{
		mUpdatable.Update();
	}

	private void LateUpdateProxy(ECViewFrameUnit _)
	{
		mLateUpdatable.LateUpdate();
	}

	private void ClearLoopDisposable()
	{
		mUpdatable = null;
		mLateUpdatable = null;
		if (mUpdateDisposable != null)
		{
			mUpdateDisposable.Dispose();
			mUpdateDisposable = null;
		}
		if (mLateUpdateDisposable != null)
		{
			mLateUpdateDisposable.Dispose();
			mLateUpdateDisposable = null;
		}
	}
}

}
