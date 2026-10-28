namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class AppearanceSkeletonPart : MonoBehaviour, IAppearanceSkeletonPart, IAppearancePart
{
	public GameObject Skeleton;

	public BaseSkeletonFollower mSkeletonFollower;

	public SkinType SkinType;

	private bool mInitAnimConfig;

	private IProfessionPartAnimatedSkeleton mSkeletonComponent;

	private Dictionary<string, PartAnimConfig> mAnimDict;

	public IProfessionAppearancePartFollower SkeletonFollower => mSkeletonFollower;

	public GameObject SkeletonTarget => Skeleton;

    private IProfessionAppearancePartBindings mBindings;
    public void ConfigurePart(IProfessionAppearancePartBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }
    private IProfessionAppearancePartBindings PartBindings => mBindings ?? ProfessionAppearancePartBindings.Required;

	protected IProfessionPartAnimatedSkeleton SkeletonComponent
	{
		get
		{
			if (mSkeletonComponent == null)
			{
				mSkeletonComponent = Skeleton.GetComponent<IProfessionPartAnimatedSkeleton>();
			}
			return mSkeletonComponent;
		}
	}

	public Dictionary<string, PartAnimConfig> AnimDict
	{
		get
		{
			if (mAnimDict == null && !mInitAnimConfig)
			{
				mInitAnimConfig = true;
				InitAnim();
			}
			return mAnimDict;
		}
	}

	public GameObject GameObject => ((Component)this).gameObject;

	public bool HasAnim => AnimDict != null;

	public virtual void Init(GameObject skeletonGo)
	{
		if (mSkeletonFollower)
		{
			mSkeletonFollower.InitFollowTarget(skeletonGo);
			mSkeletonFollower.ForceUpdate();
		}
	}

	public void TryPlayAnim(string sourceAnim)
	{
		Dictionary<string, PartAnimConfig> animDict = AnimDict;
		if (animDict != null && animDict.TryGetValue(sourceAnim, out var _))
		{
			SkeletonComponent.PlayAnimation(sourceAnim, loop: true);
		}
	}

	private void InitAnim()
	{
		string[] animations = SkeletonComponent.AnimationNames;
		string[] items = animations;
		if (items.Length == 0)
		{
			return;
		}
		Dictionary<string, List<PartAnimConfig>> targetKeyAnimDict = PartBindings.Config.GetPartConfig(SkinType).TargetKeyAnimDict;
		mAnimDict = new Dictionary<string, PartAnimConfig>(items.Length);
		string[] items2 = animations;
		foreach (string val in items2)
		{
			if (!targetKeyAnimDict.TryGetValue(val, out var value))
			{
				continue;
			}
			foreach (PartAnimConfig item in value)
			{
				mAnimDict[item.SourceAnim] = item;
			}
		}
	}
}

}
