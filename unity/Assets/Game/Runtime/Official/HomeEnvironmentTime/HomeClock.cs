using System;
using System.Collections.Generic;

namespace Zjcs.Official.HomeEnvironmentTime
{
public class HomeClock : IHomeClock
{
	private readonly LinkedList<HomeClockEntry> mHomeClockEntries = new LinkedList<HomeClockEntry>();

	private HomeDayTime mHomeDayTime;

	public HomeDayTime HomeDayTime => mHomeDayTime;

	public void Init(HomeDayTime homeDayTime)
	{
		mHomeDayTime = homeDayTime;
	}

	public void AddClock(HomeDayTime homeDayTime, Action<HomeDayTime> callback)
	{
		for (LinkedListNode<HomeClockEntry> linkedListNode = mHomeClockEntries.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
		{
			HomeDayTime homeDayTime2 = linkedListNode.Value.HomeDayTime;
			if (homeDayTime2.CompareTo(homeDayTime) == 0)
			{
				linkedListNode.Value.AddCallback(callback);
				return;
			}
			if (homeDayTime2 > homeDayTime)
			{
				HomeClockEntry value = new HomeClockEntry(homeDayTime);
				value.AddCallback(callback);
				mHomeClockEntries.AddBefore(linkedListNode, value);
				return;
			}
		}
		HomeClockEntry value2 = new HomeClockEntry(homeDayTime);
		value2.AddCallback(callback);
		mHomeClockEntries.AddLast(value2);
	}

	public void RemoveClock(HomeDayTime homeDayTime, Action<HomeDayTime> callback)
	{
		for (LinkedListNode<HomeClockEntry> linkedListNode = mHomeClockEntries.First; linkedListNode != null; linkedListNode = linkedListNode.Next)
		{
			if (linkedListNode.Value.HomeDayTime.CompareTo(homeDayTime) == 0)
			{
				linkedListNode.Value.RemoveCallback(callback);
				if (linkedListNode.Value.Callbacks.Count == 0)
				{
					mHomeClockEntries.Remove(linkedListNode);
				}
				break;
			}
		}
	}

	public void Update(HomeDayTime homeDayTime)
	{
		LinkedListNode<HomeClockEntry> linkedListNode = mHomeClockEntries.First;
		bool flag = homeDayTime < mHomeDayTime;
		while (linkedListNode != null)
		{
			if ((flag || linkedListNode.Value.HomeDayTime >= mHomeDayTime) && linkedListNode.Value.HomeDayTime < homeDayTime)
			{
				linkedListNode.Value.Invoke();
			}
			if (linkedListNode.Value.HomeDayTime > homeDayTime)
			{
				break;
			}
			linkedListNode = linkedListNode.Next;
		}
		mHomeDayTime = homeDayTime;
	}
}

}
