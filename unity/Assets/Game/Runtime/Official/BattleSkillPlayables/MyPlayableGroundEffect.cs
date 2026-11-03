using UnityEngine;
using Zjcs.Official.StoryPlayables;
using GroundDissolve = Zjcs.Official.Opening.GroundDissolve;

public class MyPlayableGroundEffect : MyPlayable
{
	public AnimationCurve EffectValue;
	public float Duration;

	private GroundDissolve mGroundDissolve;
	private float mPreEffectValue;
	private float mTimer;

	protected override void PlayImpl()
	{
		base.PlayImpl();
		mGroundDissolve = GroundDissolve.Instance;
		if (mGroundDissolve != null)
			mPreEffectValue = mGroundDissolve.Dissolve;
		mTimer = 0f;
	}

	protected override bool ProcessFrameImpl(float deltaTime)
	{
		mTimer += deltaTime;
		if (mGroundDissolve == null)
			return false;

		mGroundDissolve.Dissolve = EffectValue.Evaluate(mTimer / Duration);
		return false;
	}

	protected override void StopImpl()
	{
		if (mGroundDissolve != null)
			mGroundDissolve.Dissolve = mPreEffectValue;
	}

	public override float GetDuration() => Duration;
}
