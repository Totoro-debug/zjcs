// Migrated selected original countdown; see activity-countdown-runtime-source-manifest.json.
namespace Zjcs.Official.ActivityBattleUI
{
using System;
using System.Collections;
using UnityEngine;

public class CountdownText : MonoBehaviour
{
	[SerializeField]
	private float mUpdateIntervalS = 0.5f;

	[SerializeField]
	private ACountdownView mCountdownView;

	private WaitForSeconds mIntervalCo;

	private TimeSpan? mIntervalTimeSpan;

	private Coroutine mUpdateCo;

	private Action mTimeoutCallback;

	private TimeSpan mTimeSpan;

	private TimeSpan mRemainTimeSpan;

	private WaitForSeconds IntervalCo
	{
		get
		{
			if (mIntervalCo == null)
			{
				mIntervalCo = new WaitForSeconds(mUpdateIntervalS);
			}
			return mIntervalCo;
		}
	}

	private TimeSpan IntervalTimeSpan
	{
		get
		{
			if (!mIntervalTimeSpan.HasValue)
			{
				mIntervalTimeSpan = TimeSpan.FromSeconds(mUpdateIntervalS);
			}
			return mIntervalTimeSpan.Value;
		}
	}

	private void OnDisable()
	{
		mTimeoutCallback = null;
		StopUpdateCo();
	}

	private void StartUpdateCo()
	{
		mUpdateCo = ((MonoBehaviour)this).StartCoroutine(UpdateCountdown());
	}

	private IEnumerator UpdateCountdown()
	{
		do
		{
			double num = mRemainTimeSpan / mTimeSpan;
			mCountdownView.UpdateCountdown((float)num, mRemainTimeSpan);
			yield return IntervalCo;
			mRemainTimeSpan = mRemainTimeSpan.Subtract(IntervalTimeSpan);
		}
		while (!(mRemainTimeSpan <= TimeSpan.Zero));
		mUpdateCo = null;
		mTimeoutCallback?.Invoke();
	}

	private void StopUpdateCo()
	{
		if (mUpdateCo != null)
		{
			((MonoBehaviour)this).StopCoroutine(mUpdateCo);
			mUpdateCo = null;
		}
	}

	public void Show(double duration, double remain, Action timeoutCallback)
	{
		Show(TimeSpan.FromSeconds(duration), TimeSpan.FromSeconds(remain), timeoutCallback);
	}

	public void Show(double duration, Action timeoutCallback)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(duration);
		Show(timeSpan, timeSpan, timeoutCallback);
	}

	public void Show(TimeSpan duration, Action timeoutCallback)
	{
		Show(duration, duration, timeoutCallback);
	}

	public void Show(TimeSpan duration, TimeSpan remain, Action timeoutCallback)
	{
		((Component)this).gameObject.SetActive(true);
		StopUpdateCo();
		if (!((Component)this).gameObject.activeInHierarchy)
		{
			Debug.LogError("Countdown 必须在节点被激活时显示！[" + gameObject.name + "]", this);
		}
		else
		{
			mTimeSpan = duration;
			mRemainTimeSpan = remain;
			mTimeoutCallback = timeoutCallback;
			StartUpdateCo();
		}
	}

	public void Hide()
	{
		((Component)this).gameObject.SetActive(false);
	}
}
}
