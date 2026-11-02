// Original selected time refresh state machine; only explicit local service binding changes.
namespace Zjcs.Official.World.NativeMap14Labels
{
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Awaiter = Cysharp.Threading.Tasks.UniTask.Awaiter;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;

public class MapRegionLimitStatusRefreshForRankUnlockTime : IMapRegionLimitStatusRefreshProvider, IDisposable
{
	public IMap14LabelServices Services { get; set; }
	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _003CWaitForLimitUnlock_003Ed__8 : IAsyncStateMachine
	{
		public int _003C_003E1__state;

		public AsyncUniTaskMethodBuilder _003C_003Et__builder;

		public MapRegionLimitStatusRefreshForRankUnlockTime _003C_003E4__this;

		public CancellationToken token;

		private DateTime _003CunlockTime_003E5__2;

		private Awaiter _003C_003Eu__1;

		private void MoveNext()
		{
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_0077: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0040: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			int num = _003C_003E1__state;
			MapRegionLimitStatusRefreshForRankUnlockTime mapRegionLimitStatusRefreshForRankUnlockTime = _003C_003E4__this;
			try
			{
				if (num != 0)
				{
					_003CunlockTime_003E5__2 = mapRegionLimitStatusRefreshForRankUnlockTime.mMapRegionInfo.GetMapRegionUnlockTime();
					goto IL_008d;
				}
				Awaiter val = _003C_003Eu__1;
				_003C_003Eu__1 = default;
				num = (_003C_003E1__state = -1);
				goto IL_0086;
				IL_0086:
				val.GetResult();
				goto IL_008d;
				IL_008d:
				if (mapRegionLimitStatusRefreshForRankUnlockTime.Services.Now < _003CunlockTime_003E5__2)
				{
					UniTask val2 = UniTask.WaitForSeconds(1, false, (PlayerLoopTiming)8, token, false);
					val = val2.GetAwaiter();
					if (!val.IsCompleted)
					{
						num = (_003C_003E1__state = 0);
						_003C_003Eu__1 = val;
						_003C_003Et__builder.AwaitUnsafeOnCompleted<Awaiter, _003CWaitForLimitUnlock_003Ed__8>(ref val, ref this);
						return;
					}
					goto IL_0086;
				}
				mapRegionLimitStatusRefreshForRankUnlockTime.mOnRefresh?.Invoke();
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

	private MapRegionInfo mMapRegionInfo;

	private ShowRemainTimeInfo mRemainTimeInfoUI;

	private CancellationTokenSource mCancellationTokenSource;

	private Action mOnRefresh;

	public MapRegionLimitType RegionLimitType => MapRegionLimitType.RegionUnlockTime;

	public void Init(MapRegionInfo mapRegionInfo, ShowRemainTimeInfo remainTimeInfoUI, Action onRefresh)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		mMapRegionInfo = mapRegionInfo;
		mRemainTimeInfoUI = remainTimeInfoUI;
		mOnRefresh = onRefresh;
		mRemainTimeInfoUI.Init(mMapRegionInfo.GetMapRegionUnlockTime());
		mCancellationTokenSource = new CancellationTokenSource();
		UniTaskExtensions.Forget(WaitForLimitUnlock(mCancellationTokenSource.Token));
	}

	void IDisposable.Dispose()
	{
		mOnRefresh = null;
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
		mRemainTimeInfoUI.Stop();
	}

	[AsyncStateMachine(typeof(_003CWaitForLimitUnlock_003Ed__8))]
	private UniTask WaitForLimitUnlock(CancellationToken token)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		_003CWaitForLimitUnlock_003Ed__8 obj = default;
		obj._003C_003Et__builder = AsyncUniTaskMethodBuilder.Create();
		obj._003C_003E4__this = this;
		obj.token = token;
		obj._003C_003E1__state = -1;
		obj._003C_003Et__builder.Start<_003CWaitForLimitUnlock_003Ed__8>(ref obj);
		return obj._003C_003Et__builder.Task;
	}
}

}
