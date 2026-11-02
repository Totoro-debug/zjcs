namespace Zjcs.Official.CombatAppearance
{
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAnimation;
using System;
using UnityEngine;


public class PlayerAnimatorAppearance : PlayerAppearance
{
	public AnimationController AnimController;

	public Animator Animator;

	public PlayerPhase PlayerPhase;

	public Action OnAnimatorChanged;

    private Func<string, bool> mLocationExists;
    private Func<string, RuntimeAnimatorController> mLoadAnimator;
    public void ConfigureAnimatorResources(Func<string, bool> locationExists, Func<string, RuntimeAnimatorController> loadAnimator)
    {
        mLocationExists = locationExists ?? throw new ArgumentNullException(nameof(locationExists));
        mLoadAnimator = loadAnimator ?? throw new ArgumentNullException(nameof(loadAnimator));
    }

	public override void RefreshAppearance(long playerId, Sex sex, int profession, object data)
	{
		SetRuntimeAnimatorControllerSync(sex, profession);
		base.RefreshAppearance(playerId, sex, profession, data);
		(((Object)(object)AnimController != (Object)null) ? AnimController.myAnimator : Animator).Update(0f);
	}

	public void ReapplyOwnAnimator()
	{
		if (!((Object)(object)AnimController == (Object)null) && !((Object)(object)Animator == (Object)null))
		{
			AnimController.myAnimator = Animator;
			if (Inited)
			{
				SetRuntimeAnimatorControllerSync(mSex, mProfession);
			}
			else
			{
				AnimController.Init();
			}
			Animator val = (((Object)(object)AnimController.myAnimator != (Object)null) ? AnimController.myAnimator : Animator);
			if ((Object)(object)val != (Object)null && ((Behaviour)val).isActiveAndEnabled)
			{
				val.Update(0f);
			}
		}
	}

	private void SetRuntimeAnimatorControllerSync(Sex sex, int profession)
	{
		if ((Object)(object)AnimController == (Object)null && (Object)(object)Animator == (Object)null)
		{
			return;
		}
		string playerAnimatorPath = AppearanceAddressUtils.GetPlayerAnimatorPath(sex, profession, PlayerPhase, PlayerBindings, mLocationExists);
		RuntimeAnimatorController result = mLoadAnimator(playerAnimatorPath);
		if ((Object)(object)result == (Object)null)
		{
            Debug.LogError($"没有找到 AnimatorController: {playerAnimatorPath}. Sex: {sex} Profession: {profession} Phase: {PlayerPhase}");
		}
		SetAnimatorController(result);
	}

	private void SetAnimatorController(RuntimeAnimatorController runtimeAnimatorController)
	{
		if ((Object)(object)AnimController != (Object)null)
		{
			if ((Object)(object)AnimController.RuntimeAnimatorController != (Object)(object)runtimeAnimatorController)
			{
				AnimController.SetRuntimeAnimationController(runtimeAnimatorController);
			}
			else
			{
				AnimController.Init();
			}
		}
		else if ((Object)(object)Animator != (Object)null && (Object)(object)Animator.runtimeAnimatorController != (Object)(object)runtimeAnimatorController)
		{
			Animator.runtimeAnimatorController = runtimeAnimatorController;
		}
		OnAnimatorChanged?.Invoke();
	}
}

}
