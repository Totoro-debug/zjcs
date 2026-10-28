namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationAppearance : PlayerAppearance
{
	[SerializeField]
	[HideInInspector]
	[Header("[Anim]")]
	protected string mRuleKey;

	[SerializeField]
	[HideInInspector]
	protected List<string> mParams;

	[SerializeField]
	[HideInInspector]
	protected bool mLoop = true;

	protected string mAnimName;

	public bool AutoResetAnimation { get; set; } = true;

	public string AnimName => mAnimName;

	public override void RefreshAppearance(long playerId, Sex sex, int profession, object data)
	{
		TryPlayAnimation(sex, profession);
		base.RefreshAppearance(playerId, sex, profession, data);
		mProfession = profession;
		mSex = sex;
	}

	protected void TryPlayAnimation(Sex sex, int profession)
	{
		IAppearanceAnimComponent appearanceAnimComponent;
		if (!AutoResetAnimation || !((Component)AppearanceCharacter).TryGetComponent<IAppearanceAnimComponent>(out appearanceAnimComponent))
		{
			return;
		}
		try
		{
			if (profession == 0)
			{
				profession = PlayerBindings.GetBaseProfession(profession);
			}
			if (mProfession != profession || mSex != sex || string.IsNullOrEmpty(mAnimName))
			{
				mAnimName = ProfessionAppearanceAnimationNames.GetAnimName(PlayerBindings, PlayerBindings.AnimationRules, mRuleKey, mParams, sex, profession);
			}
			if (!string.IsNullOrEmpty(mAnimName))
			{
				appearanceAnimComponent.PlayAnimation(mAnimName, mLoop);
			}
		}
		catch (Exception exception)
		{
            Debug.LogException(exception);
		}
	}
}

}
