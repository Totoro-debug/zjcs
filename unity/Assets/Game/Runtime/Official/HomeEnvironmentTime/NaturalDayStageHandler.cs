namespace Zjcs.Official.HomeEnvironmentTime
{
public class NaturalDayStageHandler : IHomeDayStageHandler
{
	private HomeDayTime mHomeDayTime;

	private float mDeltaTime;

	private readonly IHomeEnvironmentManager mManager;

	public bool CanInterrupt => true;

	public HomeDayTime HomeDayTime => mHomeDayTime;

	public float DeltaTime => mDeltaTime;

	public NaturalDayStageHandler(IHomeEnvironmentManager manager)
	{
		mManager = manager;
		TryUpdate();
	}

	public bool TryUpdate()
	{
		mHomeDayTime = mManager.HomeDayTime;
		mDeltaTime = mManager.DeltaTime;
		return false;
	}
}

}
