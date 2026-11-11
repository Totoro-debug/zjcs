// Original menu peripheral component; local service and observable bindings.
using System.Linq;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomePlayerPanelUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class TimeInfoUI : MonoBehaviour
{
	public IHomeMenuTimeServices Services { private get; set; }

	[SerializeField]
	private MultiLanguageText mServerTipText;

	[SerializeField]
	private MultiLanguageText mServerTimeText;

	[SerializeField]
	private MultiLanguageText mLocalTimeText;

	[SerializeField]
	private int mUpdateInterval = 60;

	[SerializeField]
	private WorldStateSwitcher<bool> mStateSwitcher;

	private const string kMTimeFormat = "yyyy-MM-dd HH:mm";

	private HomePanelCancellationDisposable mHomePanelCancellationDisposable;

	private void OnEnable()
	{
		bool flag = Services.DisplayServerTimeRegionList.Contains(Services.RegionCode);
		mStateSwitcher.SetState(flag);
		if (flag)
		{
			UpdateTime();
			DateTime dateTime = Services.GetDateTime();
			float delaySeconds = mUpdateInterval - dateTime.Second % mUpdateInterval;
			StartTimerWithDelay(delaySeconds).Forget();
		}
	}

	private void OnDisable()
	{
		mHomePanelCancellationDisposable?.Dispose();
		mHomePanelCancellationDisposable = null;
	}

	private async UniTaskVoid StartTimerWithDelay(float delaySeconds)
	{
		mHomePanelCancellationDisposable = new HomePanelCancellationDisposable();
		CancellationToken token = mHomePanelCancellationDisposable.Token;
		await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), ignoreTimeScale: false, PlayerLoopTiming.Update, token);
		UpdateTime();
		await TimerLoop(token);
	}

	private async UniTask TimerLoop(CancellationToken token)
	{
		while (!token.IsCancellationRequested)
		{
			await UniTask.Delay(TimeSpan.FromSeconds(mUpdateInterval), ignoreTimeScale: false, PlayerLoopTiming.Update, token);
			UpdateTime();
		}
	}

	private void UpdateTime()
	{
		IHomeMenuTimeServices instance = Services;
		DateTime dateTimeWithTimeZone = Services.GetDateTimeWithTimeZone(instance.GetDateTime());
		int timeZone = Services.TimeZone;
		mServerTipText.SetKey(Services.GetServerTimeWithZoneTip(timeZone), timeZone);
		mServerTimeText.SetKey(dateTimeWithTimeZone.ToString("yyyy-MM-dd HH:mm"));
		mLocalTimeText.SetKey(instance.GetClientLocalDateTime().ToString("yyyy-MM-dd HH:mm"));
	}
}
}
