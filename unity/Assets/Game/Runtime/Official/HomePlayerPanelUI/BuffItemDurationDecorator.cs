// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class BuffItemDurationDecorator : BuffItemBaseDecorator
{
	[SerializeField]
	private Image mTimeDownImg;

	private HomePanelCancellationDisposable mCancellationDisposable;

	private IHomePanelStatusEffect mStatusEffectData;

	public override void Init(IHomePanelStatusEffect data)
	{
		mStatusEffectData = data;
		int duration = data.Info.Duration;
		if (duration > 0)
		{
			mCancellationDisposable = new HomePanelCancellationDisposable();
			OnShowTimeDown(duration, mCancellationDisposable.Token).Forget();
		}
	}

	public override void Release()
	{
		if (mCancellationDisposable != null)
		{
			mCancellationDisposable?.Dispose();
			mCancellationDisposable = null;
		}
	}

	private async UniTask OnShowTimeDown(int duration, CancellationToken cancellationToken)
	{
		if (duration > 0)
		{
			DateTime endTime = mStatusEffectData.LastGainedTime.AddSeconds(duration);
			float remainSeconds;
			do
			{
				remainSeconds = (float)(endTime - HomePlayerPanelScope.Required(this).GetDateTime()).TotalSeconds;
				mTimeDownImg.fillAmount = remainSeconds / (float)duration;
				await UniTask.Yield(cancellationToken);
			}
			while (remainSeconds > 0f);
		}
	}
}

}
