using System;

namespace Zjcs.Official.HomeEnvironmentTime
{
public interface IHomeClock
{
	HomeDayTime HomeDayTime { get; }

	void Init(HomeDayTime homeDayTime);

	void AddClock(HomeDayTime homeDayTime, Action<HomeDayTime> callback);

	void RemoveClock(HomeDayTime homeDayTime, Action<HomeDayTime> callback);

	void Update(HomeDayTime homeDayTime);
}

}
