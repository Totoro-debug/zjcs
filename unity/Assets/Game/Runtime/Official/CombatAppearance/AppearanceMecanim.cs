using UnityEngine;

namespace Zjcs.Official.CombatAppearance
{
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;

public class AppearanceMecanim : AppearanceRenderer
{
	private IProfessionAppearanceSceneMecanim mSkeletonMecanim;

	private Animator mAnimator;

	private IProfessionAppearanceSceneCache mMecanimCacheHelper;


	public IProfessionAppearanceSceneMecanim SkeletonMecanim
	{
		get
		{
			if (mSkeletonMecanim == null)
			{
				mSkeletonMecanim = ((Component)this).GetComponent<IProfessionAppearanceSceneMecanim>();
			}
			return mSkeletonMecanim;
		}
	}

	public Animator Animator
	{
		get
		{
			if ((Object)(object)mAnimator == (Object)null)
			{
				mAnimator = SkeletonMecanim.Animator;
			}
			return mAnimator;
		}
	}

	public IProfessionAppearanceSceneCache MecanimCacheHelper => mMecanimCacheHelper;

	protected override void OnInitComponent()
	{
		base.OnInitComponent();
		if (mMecanimCacheHelper == null)
		{
			mMecanimCacheHelper = Bindings.CreateMecanimCache(SkeletonMecanim);
		}
	}

	protected override void OnSkinUpdated()
	{
		SkeletonMecanim.Update(0f);
		mMecanimCacheHelper?.OnSkinUpdate();
		base.OnSkinUpdated();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		mMecanimCacheHelper?.OnDestroy();
		mMecanimCacheHelper = null;
	}

	public override void Release(bool tryUnloadAllAssets)
	{
		base.Release(tryUnloadAllAssets);
		mMecanimCacheHelper?.Clear();
	}
}
}
