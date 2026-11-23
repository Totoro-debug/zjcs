// Selected original home entity registration/timeline implementation; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeEntityBindings;
using Zjcs.Official.HomeEntityCreation;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeEntityRuntime
{
using Object = UnityEngine.Object;
public class HomePlayableBinding : MonoBehaviour
{
	public List<PlayableAnimatorBinding> AnimatorBindings = new List<PlayableAnimatorBinding>();

	public Animator GetBindingAnimator(string key)
	{
		foreach (PlayableAnimatorBinding animatorBinding in AnimatorBindings)
		{
			if (string.Equals(animatorBinding.Key, key))
			{
				return animatorBinding.Animator;
			}
		}
		return null;
	}
}
}
