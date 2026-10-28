// Migrated official application source; see EC view lifecycle receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StorySourceFade;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zjcs.Official.EntityViews
{
[DefaultExecutionOrder(-1)]
public class ECViewDriver : MonoBehaviour, IECViewDriver, IMyEntityStartEventReceiver, IEventReceiver, IMyEntityRemoveEventReceiver, IECViewBridge, IEntityFadeAnimationBridge
{
	private static Comparison<IECView> sSorter = (IECView x, IECView y) => x.Order.CompareTo(y.Order);

	[SerializeReference]
	private List<IECView> mECViews;

	private Dictionary<long, CancellationTokenSource> mViewCoroutines;

	private IClientVisibilitySupporter mECViewClientVisibilitySupporter;

	CancellationToken IECViewBridge.DestroyCancellationToken => ((MonoBehaviour)this).destroyCancellationToken;

	GameObject IECViewBridge.GameObject => ((Component)this).gameObject;

	IServiceProvider IECViewBridge.ServiceProvider => ServiceProvider;

	IClientVisibilitySupporter IECViewBridge.ClientVisibilitySupporter => mECViewClientVisibilitySupporter;

	IEntityFadeAnimationBridge IECViewBridge.EntityFadeAnimationBridge => this;

	int IECViewDriver.InstanceID => ((Object)this).GetInstanceID();

	public IServiceProvider ServiceProvider { get; set; }

	public IClientVisibilitySupporterFactory ClientVisibilitySupporterFactory { get; set; }

	private ECViewEntityBinding RegisterEntity { get; set; }

	public IClientVisibilitySupporter ECViewClientVisibilitySupporter => mECViewClientVisibilitySupporter;

	public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter => mECViewClientVisibilitySupporter.GetFadeAnimationSupporter();

	private bool Registered => (Object)(object)RegisterEntity != (Object)null;

	long IECViewBridge.StartCoroutine(IEnumerator execution)
	{
		if (!Registered)
		{
			UnityEngine.Debug.LogError("Official playable diagnostic");
			return 0L;
		}
		CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
		long num = ((long)cancellationTokenSource.Token.GetHashCode() << 32) | (uint)((Object)this).GetInstanceID();
		mViewCoroutines.Add(num, cancellationTokenSource);
		UniTask val = RunTask(num, execution, cancellationTokenSource.Token);
		UniTaskExtensions.Forget(val);
		if (UniTaskStatusExtensions.IsCompleted(val.Status))
		{
			return 0L;
		}
		return num;
	}

	void IECViewBridge.StopCoroutine(long coroutineId)
	{
		CancellationTokenSource cancellationTokenSource = default;
		if (mViewCoroutines.Remove(coroutineId, out cancellationTokenSource))
		{
			cancellationTokenSource.Cancel();
			cancellationTokenSource.Dispose();
		}
	}

	void IMyEntityStartEventReceiver.MyEntity_OnStart(ECViewEntityBinding entity, bool newEntity)
	{
		foreach (IECView mECView in mECViews)
		{
			mECView?.Load(entity.Entity, !newEntity);
		}
	}

	void IMyEntityRemoveEventReceiver.MyEntity_OnRemove(ECViewEntityBinding entity)
	{
		foreach (IECView mECView in mECViews)
		{
			mECView?.Unload();
		}
		Unregister();
	}

	private void Awake()
	{
		mViewCoroutines = new Dictionary<long, CancellationTokenSource>();
		mECViewClientVisibilitySupporter = ClientVisibilitySupporterFactory.Create(((Component)this).GetComponentInChildren<SpriteRendererList>(true), GetView<ECVisibleControlerViewBase>(), ((Component)this).transform);
		mECViewClientVisibilitySupporter.OnShow += OnShow;
		mECViewClientVisibilitySupporter.OnHide += OnHide;
		int num = 0;
		while (mECViews.Count - num > 0)
		{
			IECView iECView = mECViews[num++];
			if (iECView == null)
			{
				continue;
			}
			if (iECView.Append != null)
			{
				mECViews.Add(iECView.Append);
			}
			if (iECView.AppendMulti == null)
			{
				continue;
			}
			foreach (IECView item in iECView.AppendMulti)
			{
				if (item != null)
				{
					mECViews.Add(item);
				}
			}
		}
		mECViews.Sort(sSorter);
		foreach (IECView mECView in mECViews)
		{
			if (mECView != null)
			{
				ECViewServices.Inject(ServiceProvider, (object)mECView);
				mECView.Init(this);
			}
		}
	}

	private void OnShow()
	{
		if (!Registered)
		{
			return;
		}
		foreach (IECView mECView in mECViews)
		{
			mECView?.OnShow();
		}
	}

	private void OnHide()
	{
		if (!Registered)
		{
			return;
		}
		foreach (IECView mECView in mECViews)
		{
			mECView.OnHide();
		}
	}

	private void OnEnable()
	{
		if (!Registered)
		{
			ECViewEntityBinding componentInParent = ((Component)this).GetComponentInParent<ECViewEntityBinding>();
			if (!((Object)(object)componentInParent == (Object)null))
			{
				Register(componentInParent);
			}
		}
	}

	private void OnApplicationPause(bool pause)
	{
		if (!Registered)
		{
			return;
		}
		foreach (IECView mECView in mECViews)
		{
			mECView.OnApplicationPause(pause);
		}
	}

	private void OnDestroy()
	{
		Unregister();
	}

	private void Register(ECViewEntityBinding myEntity)
	{
		if (!Registered)
		{
			mECViewClientVisibilitySupporter.Register(myEntity);
			RegisterEntity = myEntity;
			RegisterEntity.AddStartReceiver((IMyEntityStartEventReceiver)this);
			RegisterEntity.AddRemoveReceiver((IMyEntityRemoveEventReceiver)this);
		}
	}

	private void Unregister()
	{
		if (!Registered)
		{
			return;
		}
		foreach (CancellationTokenSource value in mViewCoroutines.Values)
		{
			value.Cancel();
			value.Dispose();
		}
		mViewCoroutines.Clear();
		if ((Object)(object)RegisterEntity != (Object)null)
		{
			RegisterEntity.RemoveStartReceiver((IMyEntityStartEventReceiver)this);
			RegisterEntity.RemoveRemoveReceiver((IMyEntityRemoveEventReceiver)this);
			RegisterEntity = null;
		}
		mECViewClientVisibilitySupporter.Unregister();
	}

	private async UniTask RunTask(long signature, IEnumerator execution, CancellationToken cancellationToken)
    {
        try { await execution.WithCancellation(cancellationToken).SuppressCancellationThrow(); }
        catch (Exception exception) { UnityEngine.Debug.LogError("ECViewDriver RunTask: " + exception); }
        ((IECViewBridge)this).StopCoroutine(signature);
    }

	public T GetView<T>() where T : class, IECView
	{
		foreach (IECView mECView in mECViews)
		{
			if (mECView is T result)
			{
				return result;
			}
		}
		return null;
	}
}

}
