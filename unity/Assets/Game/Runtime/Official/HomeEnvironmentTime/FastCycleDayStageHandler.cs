using UnityEngine;

namespace Zjcs.Official.HomeEnvironmentTime
{
public class FastCycleDayStageHandler : IHomeDayStageHandler
{
	private readonly HomeDateTime mDateTime;

	private readonly float mTotalTime;

	private readonly float mTimeScale;

	private float mTimer;

	private readonly IHomeEnvironmentManager mManager;

	private HomeDayTime mHomeDayTime;

	private float mDeltaTime;

	private readonly AnimationCurve mAnimationCurve;

	public bool CanInterrupt => false;

	public HomeDayTime HomeDayTime => mHomeDayTime;

	public float DeltaTime => mDeltaTime;

	public FastCycleDayStageHandler(IHomeEnvironmentManager manager, int loopDay, float dayDuration)
	{
		mManager = manager;
		mAnimationCurve = manager.Config.Value.FastCycleCurve;
		mDateTime = new HomeDateTime(manager.HomeDateTime);
		mTimeScale = (float)mDateTime.SecondsPerDay / dayDuration / manager.Config.Value.HomeTimeScaleRuntime;
		mTotalTime = mDateTime.SecondsPerDay * loopDay;
	}

	public bool TryUpdate()
	{
		float num = mTimer;
		float deltaTime = mManager.DeltaTime;
		float num2 = deltaTime * mTimeScale;
		mTimer = Mathf.Clamp(mTimer + num2, 0f, mTotalTime);
		float num3 = (mAnimationCurve.Evaluate(mTimer / mTotalTime) - mAnimationCurve.Evaluate(num / mTotalTime)) * mTotalTime;
		mDeltaTime = num3 + deltaTime;
		mDateTime.Update(mDeltaTime);
		mHomeDayTime = mDateTime.GetHomeDayTime();
		return mTimer >= mTotalTime;
	}
}

}
