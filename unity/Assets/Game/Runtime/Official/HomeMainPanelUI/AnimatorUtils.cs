// Original main-panel implementation; namespace/base binding only.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UIBase = Zjcs.Official.ProfessionUI.UIBase;
namespace Zjcs.Official.HomeMainPanelUI
{

public static class AnimatorUtils
{
	public static Dictionary<string, float> GetClipLengthDict(Animator animator)
	{
		AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
		Dictionary<string, float> dictionary = new Dictionary<string, float>();
		AnimationClip[] array = animationClips;
		foreach (AnimationClip val in array)
		{
			dictionary.Add(((Object)val).name, val.length);
		}
		return dictionary;
	}

	public static float GetClipLength(Animator animator, string clipName)
	{
		AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
		foreach (AnimationClip val in animationClips)
		{
			if (((Object)val).name == clipName)
			{
				return val.length;
			}
		}
		return 0f;
	}

	public static IEnumerator WaitForStateFinished(Animator animator, int stateNameHash)
	{
		AnimatorStateInfo currentAnimatorStateInfo;
		while (true)
		{
			currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
			if (currentAnimatorStateInfo.shortNameHash != stateNameHash)
			{
				yield return null;
				continue;
			}
			break;
		}
		while (true)
		{
			currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
			if (currentAnimatorStateInfo.normalizedTime < 1f)
			{
				yield return null;
				continue;
			}
			break;
		}
	}

	public static float GetAnimationCurveLength(AnimationCurve curve)
	{
		if (curve == null || curve.length <= 0)
		{
			return 0f;
		}
		return curve.keys[curve.length - 1].time;
	}
}

}
