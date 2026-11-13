// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
[RequireComponent(typeof(Animation))]
public class AnimationAutoNext : MonoBehaviour
{
	[SerializeField]
	private string mNextAnimationName;

	[SerializeField]
	private float mDelayTime;

	private Animation mAnimation;

	private HomeAwardCancellation mHomeAwardCancellation;

	private void Awake()
	{
		mAnimation = GetComponent<Animation>();
	}

	private void OnEnable()
	{
		mHomeAwardCancellation = new HomeAwardCancellation();
		PlayAnimation(mHomeAwardCancellation.Token).Forget();
	}

	private void OnDisable()
	{
		if (mHomeAwardCancellation != null)
		{
			mHomeAwardCancellation.Dispose();
			mHomeAwardCancellation = null;
		}
	}

	private async UniTask PlayAnimation(CancellationToken cancellationToken)
	{
		await UniTask.WaitForSeconds(mAnimation.clip.length, ignoreTimeScale: false, PlayerLoopTiming.Update, cancellationToken);
		await UniTask.WaitForSeconds(mDelayTime, ignoreTimeScale: false, PlayerLoopTiming.Update, cancellationToken);
		mAnimation.Play(mNextAnimationName);
	}
}
}
