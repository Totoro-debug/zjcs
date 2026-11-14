namespace Zjcs.Official.HomeEnvironmentTime
{
public interface IHomeDayStageHandler
{
	bool CanInterrupt { get; }

	HomeDayTime HomeDayTime { get; }

	float DeltaTime { get; }

	bool TryUpdate();
}

}
