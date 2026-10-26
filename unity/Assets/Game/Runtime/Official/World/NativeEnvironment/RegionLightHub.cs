// Migrated from selected official environment code; see Map13 source receipt.
namespace Zjcs.Official.World.NativeEnvironment
{
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RegionLightHub
{
	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _003CDoFadeOut_003Ed__7 : IAsyncStateMachine
	{
		public int _003C_003E1__state;

		public AsyncUniTaskVoidMethodBuilder _003C_003Et__builder;

		public RegionLightHub _003C_003E4__this;

		public float duration;

		public CancellationToken cancellationToken;

		private float _003Celpased_003E5__2;

		private UniTask.Awaiter _003C_003Eu__1;

		private void MoveNext()
		{
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0152: Unknown result type (might be due to invalid IL or missing references)
			//IL_0107: Unknown result type (might be due to invalid IL or missing references)
			//IL_010c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0110: Unknown result type (might be due to invalid IL or missing references)
			//IL_0115: Unknown result type (might be due to invalid IL or missing references)
			//IL_012a: Unknown result type (might be due to invalid IL or missing references)
			//IL_012c: Unknown result type (might be due to invalid IL or missing references)
			int num = _003C_003E1__state;
			RegionLightHub regionLightHub = _003C_003E4__this;
			try
			{
				UniTask.Awaiter val;
				if (num == 0)
				{
					val = _003C_003Eu__1;
					_003C_003Eu__1 = default;
					num = (_003C_003E1__state = -1);
					goto IL_0161;
				}
				_003Celpased_003E5__2 = 0f;
				int count;
				if (regionLightHub.NightLights.Count != 0)
				{
					count = regionLightHub.NightLights.Count;
					for (int i = 0; i < count; i++)
					{
						Light2D val2 = regionLightHub.NightLights[i];
						regionLightHub.mLight2DInitialIntensity.Add(val2, val2.intensity);
					}
					goto IL_0071;
				}
				regionLightHub.Clear();
				goto end_IL_000e;
				IL_0161:
				val.GetResult();
				_003Celpased_003E5__2 += Time.deltaTime;
				goto IL_0071;
				IL_0071:
				float num2 = Mathf.Clamp01(_003Celpased_003E5__2 / duration);
				count = regionLightHub.NightLights.Count;
				for (int j = 0; j < count; j++)
				{
					Light2D val3 = regionLightHub.NightLights[j];
					if (!regionLightHub.mLight2DInitialIntensity.TryGetValue(val3, out var value))
					{
						value = val3.intensity;
						regionLightHub.mLight2DInitialIntensity.Add(val3, value);
					}
					val3.intensity = Mathf.Lerp(value, 0f, num2);
				}
				if (!(_003Celpased_003E5__2 > duration))
				{
					UniTask val4 = UniTask.Yield((PlayerLoopTiming)8, cancellationToken, false);
					val = val4.GetAwaiter();
					if (!val.IsCompleted)
					{
						num = (_003C_003E1__state = 0);
						_003C_003Eu__1 = val;
						_003C_003Et__builder.AwaitUnsafeOnCompleted<UniTask.Awaiter, _003CDoFadeOut_003Ed__7>(ref val, ref this);
						return;
					}
					goto IL_0161;
				}
				regionLightHub.mLight2DInitialIntensity.Clear();
				regionLightHub.ChangeLightState(DayNightState.Day);
				regionLightHub.Clear();
				end_IL_000e:;
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

	public List<Light2D> DayLights = new List<Light2D>();

	public List<Light2D> NightLights = new List<Light2D>();

	public MapRegionLight RegionLight;

	private CancellationTokenSource mCancellationTokenSource;

	private Dictionary<Light2D, float> mLight2DInitialIntensity = new Dictionary<Light2D, float>();

	public void Clear()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}

	public void FadeOut(float duration)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (mCancellationTokenSource == null)
		{
			mCancellationTokenSource = new CancellationTokenSource();
			UniTaskVoid val = DoFadeOut(duration, mCancellationTokenSource.Token);
			val.Forget();
		}
	}

	[AsyncStateMachine(typeof(_003CDoFadeOut_003Ed__7))]
	private UniTaskVoid DoFadeOut(float duration, CancellationToken cancellationToken)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		_003CDoFadeOut_003Ed__7 obj = default;
		obj._003C_003Et__builder = AsyncUniTaskVoidMethodBuilder.Create();
		obj._003C_003E4__this = this;
		obj.duration = duration;
		obj.cancellationToken = cancellationToken;
		obj._003C_003E1__state = -1;
		obj._003C_003Et__builder.Start<_003CDoFadeOut_003Ed__7>(ref obj);
		return obj._003C_003Et__builder.Task;
	}

	public void ChangeLightState(DayNightState state)
	{
		int i = 0;
		for (int count = DayLights.Count; i < count; i++)
		{
			((Component)DayLights[i]).gameObject.SetActive(state == DayNightState.Day);
		}
		int j = 0;
		for (int count2 = NightLights.Count; j < count2; j++)
		{
			((Component)NightLights[j]).gameObject.SetActive(state == DayNightState.Night);
		}
	}
}

}
