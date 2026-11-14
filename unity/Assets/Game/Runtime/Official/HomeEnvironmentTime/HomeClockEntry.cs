using System;
using System.Collections.Generic;

namespace Zjcs.Official.HomeEnvironmentTime
{
public struct HomeClockEntry
{
	public HomeDayTime HomeDayTime;

	public List<Action<HomeDayTime>> Callbacks;

	public HomeClockEntry(HomeDayTime homeDayTime)
	{
		HomeDayTime = homeDayTime;
		Callbacks = new List<Action<HomeDayTime>>();
	}

	public void AddCallback(Action<HomeDayTime> callback)
	{
		Callbacks.Add(callback);
	}

	public void RemoveCallback(Action<HomeDayTime> callback)
	{
		Callbacks.Remove(callback);
	}

	public void Invoke()
	{
		foreach (Action<HomeDayTime> callback in Callbacks)
		{
			callback(HomeDayTime);
		}
	}
}

}
