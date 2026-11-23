// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Spine;
using Spine.Unity;
using Animation = Spine.Animation;
using AnimationState = Spine.AnimationState;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class AppearancePartSideSwitcher : MonoBehaviour
{
	public IHomeWeaponAnimationBindings AnimationBindings { get; set; }

	public AppearancePartSideType Side;

	public AppearancePartSide[] Sides;

	public SkeletonAnimation SkeletonAnimation;

	public SkeletonGraphic SkeletonGraphic;

	public List<AppearancePartSideSwitchListener> SideSwitchListeners;

	public Action OnSideChanged;

	public void SetSide(AppearancePartSideType side)
	{
		Side = side;
		UpdateSide();
	}

	private void UpdateSide()
	{
		if (Sides == null || Sides.Length == 0)
		{
			return;
		}
		AppearancePartSide appearancePartSide = null;
		AppearancePartSide[] sides = Sides;
		foreach (AppearancePartSide appearancePartSide2 in sides)
		{
			if (appearancePartSide2.CheckActive(Side))
			{
				appearancePartSide = appearancePartSide2;
			}
			else
			{
				appearancePartSide2.UpdateState(active: false);
			}
		}
		if (appearancePartSide == null)
		{
			return;
		}
		appearancePartSide.UpdateState(active: true);
		if (SideSwitchListeners != null)
		{
			foreach (AppearancePartSideSwitchListener sideSwitchListener in SideSwitchListeners)
			{
				sideSwitchListener.SetAppearancePartSide(Side);
			}
		}
		try
		{
			OnSideChanged?.Invoke();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception, this);
		}
		if ((bool)SkeletonAnimation)
		{
			HomeWeaponAnimation.PlayAnimation(AnimationBindings, SkeletonAnimation, appearancePartSide.SkeletonAnimName, loop: true);
		}
		else if ((bool)SkeletonGraphic)
		{
			HomeWeaponAnimation.PlayAnimation(AnimationBindings, SkeletonGraphic, appearancePartSide.SkeletonAnimName, loop: true);
		}
	}
}

}
