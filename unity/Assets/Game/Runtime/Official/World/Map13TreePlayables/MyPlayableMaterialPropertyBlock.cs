// Migrated original tree-playable application source; see source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryFollowBone;
using System;
using UnityEngine;




namespace Zjcs.Official.World.Map13TreePlayables
{
[MyPlayableApply(new Type[] { typeof(Renderer) })]
public class MyPlayableMaterialPropertyBlock : MyPlayable
{
	public string ColorPropertyName;

	public Gradient ColorCurve;

	public string FloatPropertyName;

	public AnimationCurve FloatCurve;

	public float Duration = 0.3f;

	private bool mHasFloat;

	private bool mHasColor;

	private float mTimer;

	private Renderer mRenderer;

	private MaterialPropertyBlock mPropertyBlock;

	protected override void PlayImpl()
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected Obj, but got Unknown
		base.PlayImpl();
		Component val = myApplyComponent;
		mRenderer = (Renderer)(object)((val is Renderer) ? val : null);
		if (!((Object)(object)mRenderer == (Object)null))
		{
			if (mPropertyBlock == null)
			{
				mPropertyBlock = new MaterialPropertyBlock();
			}
			mRenderer.GetPropertyBlock(mPropertyBlock);
			mHasFloat = !string.IsNullOrEmpty(FloatPropertyName);
			mHasColor = !string.IsNullOrEmpty(ColorPropertyName);
			mTimer = 0f;
		}
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		mTimer += deltaTime;
		if ((Object)(object)mRenderer == (Object)null)
		{
			return false;
		}
		float num = mTimer / Duration;
		Color val = ColorCurve.Evaluate(num);
		float num2 = FloatCurve.Evaluate(num);
		if (mHasColor)
		{
			mPropertyBlock.SetColor(ColorPropertyName, val);
		}
		if (mHasFloat)
		{
			mPropertyBlock.SetFloat(FloatPropertyName, num2);
		}
		mRenderer.SetPropertyBlock(mPropertyBlock);
		return false;
	}

	public override float GetDuration()
	{
		return Duration;
	}
}

}
