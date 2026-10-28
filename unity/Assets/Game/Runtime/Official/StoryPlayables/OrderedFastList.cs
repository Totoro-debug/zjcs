// Migrated selected official source; see map14-story-playable-runtime source receipt.
using Object = UnityEngine.Object;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Zjcs.Official.StoryPlayables
{

public class OrderedFastList<T> : IEnumerable<T>, IEnumerable, ICollection<T>
{
	private readonly LinkedList<T> mLinkedList;

	private readonly Dictionary<T, LinkedListNode<T>> mNodeMap;

	public int Count => mNodeMap.Count;

	public T First
	{
		get
		{
			if (mLinkedList.First == null)
			{
				return default;
			}
			return mLinkedList.First.Value;
		}
	}

	public T Last
	{
		get
		{
			if (mLinkedList.Last == null)
			{
				return default;
			}
			return mLinkedList.Last.Value;
		}
	}

	public bool IsReadOnly
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public OrderedFastList()
	{
		mLinkedList = new LinkedList<T>();
		mNodeMap = new Dictionary<T, LinkedListNode<T>>();
	}

	public LinkedList<T>.Enumerator GetEnumerator()
	{
		return mLinkedList.GetEnumerator();
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return mLinkedList.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return mLinkedList.GetEnumerator();
	}

	public bool Contains(T item)
	{
		return mNodeMap.ContainsKey(item);
	}

	public void Add(T item)
	{
		if (!mNodeMap.ContainsKey(item))
		{
			LinkedListNode<T> value = mLinkedList.AddLast(item);
			mNodeMap[item] = value;
		}
	}

	public bool Remove(T item)
	{
		if (!mNodeMap.TryGetValue(item, out var value))
		{
			return false;
		}
		mLinkedList.Remove(value);
		mNodeMap.Remove(item);
		return true;
	}

	public void Clear()
	{
		mLinkedList.Clear();
		mNodeMap.Clear();
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		mLinkedList.CopyTo(array, arrayIndex);
	}
}

}
