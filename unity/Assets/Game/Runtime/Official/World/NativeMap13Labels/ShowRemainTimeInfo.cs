// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using System.Collections;
using UnityEngine;

public class ShowRemainTimeInfo : UIBase
{
	[SerializeField]
	public MultiLanguageText mRemainTimeText;

	private DateTime mEndTime;

	private float mRefreshInterval;

	private WaitForSeconds mWaitForSeconds;

	private string mCustomKey;

	private ETimeSpanFormatType mFormat;

	private Action mCallBack;

	private bool mStop;

	public void Init(DateTime endTime, float refreshInterval = 1f, string customKey = null, ETimeSpanFormatType timeSpanFormat = ETimeSpanFormatType.Normal, Action callBack = null)
	{
		InitWrapper(() =>
		{
			mEndTime = endTime;
			mRefreshInterval = refreshInterval;
			mStop = false;
			mCustomKey = customKey;
			mFormat = timeSpanFormat;
			mCallBack = callBack;
		});
	}

	public void StartTimer()
	{
		((MonoBehaviour)this).StopAllCoroutines();
		if (((Behaviour)this).isActiveAndEnabled)
		{
			((MonoBehaviour)this).StartCoroutine(StartTimerImpl());
		}
	}

	private IEnumerator StartTimerImpl()
	{
		if (mWaitForSeconds == null)
		{
			mWaitForSeconds = new WaitForSeconds(mRefreshInterval);
		}
		while (!(Services.Now > mEndTime))
		{
			int num = Mathf.FloorToInt((float)Services.CalcRemainSecond(mEndTime));
			RefreshText(TimeSpan.FromSeconds(num));
			yield return mWaitForSeconds;
		}
		RefreshText(new TimeSpan(0L));
		mCallBack?.Invoke();
	}

	private void RefreshText(TimeSpan timeSpan)
	{
		if (!mStop)
		{
			RefrshTimeText(timeSpan);
		}
	}

	private void RefrshTimeText(TimeSpan timeSpan)
	{
		object obj = new CultureTimeSpanArg(timeSpan, mFormat);
		if (mCustomKey != null)
		{
			mRemainTimeText.SetKey(mCustomKey, obj);
		}
		else
		{
			mRemainTimeText.Format(obj);
		}
	}

	public void SetCustomText(string textKey, params object[] args)
	{
		Stop();
		((Component)mRemainTimeText).gameObject.SetActive(true);
		mRemainTimeText.SetKey(textKey, args);
	}

	public void Stop(bool inactiveText = true)
	{
		mStop = true;
		((MonoBehaviour)this).StopAllCoroutines();
		if (inactiveText)
		{
			((Component)mRemainTimeText).gameObject.SetActive(false);
		}
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Player.OnDebugTimeOffset += OnDebugTimeOffset;
		if (!mStop)
		{
			((Component)mRemainTimeText).gameObject.SetActive(true);
		}
		if (Services.Now >= mEndTime)
		{
			RefreshText(new TimeSpan(0L));
		}
		else
		{
			StartTimer();
		}
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		((MonoBehaviour)this).StopAllCoroutines();
		if (Services.Player != null)
		{
			Services.Player.OnDebugTimeOffset -= OnDebugTimeOffset;
		}
	}

	private void OnDebugTimeOffset(long tick)
	{
		if (!mStop)
		{
			StartTimer();
		}
	}
}

}
