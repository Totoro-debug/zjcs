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
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;

public class HomeNpcBubbleTip : RedDotItemBase
{
	[SerializeField]
	private SpriteRenderer mIcon;

	[SerializeField]
	private WorldStateSwitcher<bool> mShowStateSwitcher;

	[SerializeField]
	private SkeletonAnimation mSkeletonAnimation;

	[SerializeField]
	[SpineAnimation("", "", true, false, false)]
	private string mShowAnim;

	[SerializeField]
	[SpineAnimation("", "", true, false, false)]
	private string mNotShowAnim;

	private readonly HomeFurnitureLifetime mSubscriptions = new HomeFurnitureLifetime();

	private readonly List<string> mLoadedIcons = new List<string>();

	public IHomeFurnitureServices FurnitureServices { get; set; }

	public IHomeNpcBubbleTipServices BubbleServices { get; set; }

	public IHomeWeaponAnimationBindings AnimationBindings { get; set; }

	private void ReleaseLoadedIcons()
	{
		if ((Object)(object)mIcon != (Object)null)
		{
			mIcon.sprite = null;
		}
		if (BubbleServices != null)
		{
			for (int i = 0; i < mLoadedIcons.Count; i++)
			{
				BubbleServices.UnloadNpcIcon(mLoadedIcons[i]);
			}
		}
		mLoadedIcons.Clear();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mSubscriptions.Dispose();
		if (BubbleServices != null)
		{
			mSubscriptions.Add(BubbleServices.SubscribeDialogueChanged(OnDialogueChanged));
			mSubscriptions.Add(BubbleServices.SubscribePostHomeChange(OnPostHomeChange));
		}
	}

	protected override void OnDisableAndInited()
	{
		mSubscriptions.Dispose();
		ReleaseLoadedIcons();
		base.OnDisableAndInited();
	}

	private void OnDialogueChanged()
	{
		SetDirty();
	}

	private void OnPostHomeChange()
	{
		SetDirty();
	}

	public override void Refresh()
	{
		ReleaseLoadedIcons();
		base.Refresh();
		mShowStateSwitcher.SetState(IsShow);
		if ((Object)(object)mSkeletonAnimation != (Object)null)
		{
			string animationName = (IsShow ? mShowAnim : mNotShowAnim);
			HomeWeaponAnimation.PlayAnimation(AnimationBindings, mSkeletonAnimation, animationName, loop: true);
		}
	}

	public override bool ShowRedDot()
	{
		if (FurnitureServices == null || !FurnitureServices.InMyHome || FurnitureServices.Player == null)
		{
			return false;
		}
		if (FurnitureServices.IsFunctionLimit(57))
		{
			return false;
		}
		if (BubbleServices != null && BubbleServices.TryFindActiveDialogueNpcClassId(out int npcClassId))
		{
			SetNpc(npcClassId);
			return true;
		}
		return base.ShowRedDot();
	}

	public HomeNpcBubbleTip SetNpc(int npcClassId)
	{
		string iconPath = BubbleServices?.GetNpcIconPath(npcClassId);
		if (string.IsNullOrEmpty(iconPath))
		{
			Debug.LogError($"{nameof(HomeNpcBubbleTip)}.{nameof(SetNpc)}({npcClassId}) 没有找到 Npc 配置数据！", this);
			return this;
		}
		mIcon.sprite = BubbleServices.LoadNpcIcon(iconPath);
		mLoadedIcons.Add(iconPath);
		return this;
	}
}
}
