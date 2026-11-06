// Migrated selected original countdown; see activity-countdown-runtime-source-manifest.json.
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using ETimeSpanFormatType = Zjcs.Official.WorldUI.WorldTimeSpanFormatType;
namespace Zjcs.Official.ActivityBattleUI
{
using System;
using UnityEngine;
using UnityEngine.UI;

public class CountdownView : ACountdownView
{
	[SerializeField]
	private MultiLanguageText mCountdownText;

	[SerializeField]
	private Image mProgress;

	[SerializeField]
	private ETimeSpanFormatType mFormat;

	public override void UpdateCountdown(float progress, TimeSpan timeSpan)
	{
		if ((UnityEngine.Object)(object)mProgress != (UnityEngine.Object)null)
		{
			mProgress.fillAmount = progress;
		}
		if ((UnityEngine.Object)(object)mCountdownText != (UnityEngine.Object)null)
		{
			mCountdownText.Format(new WorldCultureTimeSpanArg(timeSpan, mFormat, mCountdownText.Localizer));
		}
	}
}
}
