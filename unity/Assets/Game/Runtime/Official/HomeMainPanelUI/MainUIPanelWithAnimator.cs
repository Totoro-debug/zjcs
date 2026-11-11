// Original main-panel implementation; namespace/base binding only.
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UIBase = Zjcs.Official.ProfessionUI.UIBase;
namespace Zjcs.Official.HomeMainPanelUI
{

public abstract class MainUIPanelWithAnimator : MainUIPanelBase
{
	[SerializeField]
	private Animator mAnimator;

	private static int sAnimatorParamKey = Animator.StringToHash("IsShow");

	private static int sShowStatusNameHash = Animator.StringToHash("Show");

	private static int sHideStatusNameHash = Animator.StringToHash("Hide");

	public override async UniTask Show(CancellationToken cancellationToken)
	{
		gameObject.SetActive(value: true);
		mAnimator.SetBool(sAnimatorParamKey, value: true);
		await AnimatorUtils.WaitForStateFinished(mAnimator, sShowStatusNameHash).ToUniTask(PlayerLoopTiming.Update, cancellationToken);
	}

	public override async UniTask Hide(CancellationToken cancellationToken)
	{
		mAnimator.SetBool(sAnimatorParamKey, value: false);
		if (!gameObject.activeInHierarchy || mAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != sShowStatusNameHash)
		{
			gameObject.SetActive(value: false);
			return;
		}
		await AnimatorUtils.WaitForStateFinished(mAnimator, sHideStatusNameHash).ToUniTask(PlayerLoopTiming.Update, cancellationToken);
		gameObject.SetActive(value: false);
	}
}

}
