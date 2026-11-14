// Selected original 884 implementation; configuration and logging boundaries only.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeLevelUpRules
{

public class LRUCollection<T1, T2> : IEnumerable<(T1, T2)>, IEnumerable
{
	private readonly int mCapacity;

	private LinkedList<(T1 Key, T2 Value)> mList = new LinkedList<(T1, T2)>();

	private Dictionary<T1, LinkedListNode<(T1 Key, T2 Value)>> mDictionary = new Dictionary<T1, LinkedListNode<(T1, T2)>>();

	private SortedValueDictionary<T1, DateTime> mTTLDict = new SortedValueDictionary<T1, DateTime>();

	private List<T1> mWait4Remove = new List<T1>();

	private bool mRemovingTTL;

	public int Count => mList.Count;

	public T2 this[T1 key]
	{
		get
		{
			return Get(key);
		}
		set
		{
			Add(key, value);
		}
	}

	public LRUCollection(int capacity = 65535)
	{
		mCapacity = capacity;
	}

	public void Visit(T1 key)
	{
		RemoveExpireElement();
		if (mDictionary.TryGetValue(key, out LinkedListNode<(T1, T2)> value))
		{
			LinkedListNode<(T1, T2)> node = value;
			mList.Remove(node);
			mList.AddLast(node);
		}
	}

	public T2 Get(T1 key)
	{
		RemoveExpireElement();
		LinkedListNode<(T1, T2)> linkedListNode = mDictionary[key];
		mList.Remove(linkedListNode);
		mList.AddLast(linkedListNode);
		return linkedListNode.Value.Item2;
	}

	public void Add(T1 key, T2 value, double ttl = -1.0)
	{
		RemoveExpireElement();
		if (mDictionary.TryGetValue(key, out LinkedListNode<(T1, T2)> value2))
		{
			mList.Remove(value2);
			value2.Value = (key, value);
			mList.AddLast(value2);
		}
		else
		{
			LinkedListNode<(T1, T2)> linkedListNode = null;
			if (mList.Count == mCapacity)
			{
				linkedListNode = mList.First;
				mDictionary.Remove(linkedListNode.Value.Item1);
				mList.RemoveFirst();
				linkedListNode.Value = (key, value);
			}
			else
			{
				linkedListNode = new LinkedListNode<(T1, T2)>((key, value));
			}
			mList.AddLast(linkedListNode);
			mDictionary[key] = linkedListNode;
		}
		if (ttl > 0.0)
		{
			mTTLDict[key] = DateTime.Now.AddSeconds(ttl);
		}
		else
		{
			mTTLDict.Remove(key);
		}
	}

	private void RemoveExpireElement()
	{
		if (mRemovingTTL)
		{
			return;
		}
		mRemovingTTL = true;
		DateTime now = DateTime.Now;
		foreach (var item in mTTLDict)
		{
			if (item.Value > now)
			{
				break;
			}
			mWait4Remove.Add(item.Key);
		}
		foreach (T1 item2 in mWait4Remove)
		{
			mTTLDict.Remove(item2);
			Remove(item2);
		}
		mWait4Remove.Clear();
		mRemovingTTL = false;
	}

	public bool ContainsKey(T1 key)
	{
		RemoveExpireElement();
		return mDictionary.ContainsKey(key);
	}

	public bool TryGetValue(T1 key, out T2 value)
	{
		RemoveExpireElement();
		if (mDictionary.TryGetValue(key, out LinkedListNode<(T1, T2)> value2))
		{
			mList.Remove(value2);
			mList.AddLast(value2);
			value = value2.Value.Item2;
			return true;
		}
		value = default;
		return false;
	}

	public bool Remove(T1 key)
	{
		if (!mDictionary.TryGetValue(key, out LinkedListNode<(T1, T2)> value))
		{
			return false;
		}
		mList.Remove(value);
		mDictionary.Remove(key);
		return true;
	}

	public IEnumerator<(T1, T2)> GetEnumerator()
	{
		RemoveExpireElement();
		foreach (var m in mList)
		{
			yield return m;
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}

}
