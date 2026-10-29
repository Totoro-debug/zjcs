// Migrated original application source; see Map14 visibility-handlers receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using IEventReceiver=Zjcs.Official.World.Map13EntityEvents.IEventReceiver;
using Awaiter=Cysharp.Threading.Tasks.UniTask.Awaiter;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityContainers;
using Zjcs.Official.EntityActions;
using Zjcs.Official.StorySourceFade;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using UnityEngine;

namespace Zjcs.Official.EntityVisibilityHandlers
{
public class ClientVisibilityWaitActionHandler : ClientVisibilityBaseHandler, IMyContainerAddEntityEventReceiver, IEventReceiver
{
	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _003CSafeWait_003Ed__11 : IAsyncStateMachine
	{
		public int _003C_003E1__state;

		public AsyncUniTaskMethodBuilder _003C_003Et__builder;

		public CancellationToken cancellationToken;

		public ClientVisibilityWaitActionHandler _003C_003E4__this;

		private Awaiter _003C_003Eu__1;

		private void MoveNext()
		{
			//IL_0057: Unknown result type (might be due to invalid IL or missing references)
			//IL_005c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0063: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			int num = _003C_003E1__state;
			ClientVisibilityWaitActionHandler clientVisibilityWaitActionHandler = _003C_003E4__this;
			try
			{
				Awaiter val2;
				if (num != 0)
				{
					UniTask val = UniTask.WaitForSeconds(1f, false, (PlayerLoopTiming)8, cancellationToken, false);
					val2 = val.GetAwaiter();
					if (!val2.IsCompleted)
					{
						num = (_003C_003E1__state = 0);
						_003C_003Eu__1 = val2;
						_003C_003Et__builder.AwaitUnsafeOnCompleted<Awaiter, _003CSafeWait_003Ed__11>(ref val2, ref this);
						return;
					}
				}
				else
				{
					val2 = _003C_003Eu__1;
					_003C_003Eu__1 = default;
					num = (_003C_003E1__state = -1);
				}
				val2.GetResult();
				clientVisibilityWaitActionHandler.Visible = true;
				clientVisibilityWaitActionHandler.ClearToken();
			}
			catch (Exception exception)
			{
				_003C_003E1__state = -2;
				_003C_003Et__builder.SetException(exception);
				return;
			}
			_003C_003E1__state = -2;
			_003C_003Et__builder.SetResult();
		}

		void IAsyncStateMachine.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			this.MoveNext();
		}

		[DebuggerHidden]
		private void SetStateMachine(IAsyncStateMachine stateMachine)
		{
			_003C_003Et__builder.SetStateMachine(stateMachine);
		}

		void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
		{
			//ILSpy generated this explicit interface implementation from .override directive in SetStateMachine
			this.SetStateMachine(stateMachine);
		}
	}

	private CancellationTokenSource mCancellationTokenSource;

	public IVisibilityMapRoot MapRootManagerProxy { get; set; }

	public override ClientVisibilityType VisibilityType => ClientVisibilityType.WaitRefreshAction;

	public override void Init(ECViewEntityData ent, bool isNew)
	{
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		base.Init(ent, isNew);
		if (!isNew)
		{
			Visible = true;
			return;
		}
		Visible = false;
		MapRootManagerProxy.MyContainer.EntityAddEvent += (IMyContainerAddEntityEventReceiver)this;
		ClearToken();
		mCancellationTokenSource = new CancellationTokenSource();
		UniTaskExtensions.Forget(SafeWait(mCancellationTokenSource.Token));
	}

	void IMyContainerAddEntityEventReceiver.MyContainer_OnEntityAdd(MyEntity entity)
	{
		MyAction component = ((Component)entity).GetComponent<MyAction>();
		if (!((Object)(object)component == (Object)null) && !(component.Component.ApplyId != Entity.Id))
		{
			ClearToken();
			Visible = true;
		}
	}

	public override void Release()
	{
		if ((Object)(object)MapRootManagerProxy.MyContainer != (Object)null)
		{
			MapRootManagerProxy.MyContainer.EntityAddEvent -= (IMyContainerAddEntityEventReceiver)this;
		}
		ClearToken();
		base.Release();
	}

	private void ClearToken()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	[AsyncStateMachine(typeof(_003CSafeWait_003Ed__11))]
	private UniTask SafeWait(CancellationToken cancellationToken)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		_003CSafeWait_003Ed__11 obj = default;
		obj._003C_003Et__builder = AsyncUniTaskMethodBuilder.Create();
		obj._003C_003E4__this = this;
		obj.cancellationToken = cancellationToken;
		obj._003C_003E1__state = -1;
		obj._003C_003Et__builder.Start<_003CSafeWait_003Ed__11>(ref obj);
		return obj._003C_003Et__builder.Task;
	}
}

}
