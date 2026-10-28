// Original client/framework implementation; see entity loading source receipt.
using Object = UnityEngine.Object;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using System;
using System.Collections;
using System.Collections.Generic;


namespace Zjcs.Official.EntityLoading
{
public class SortedValueDictionary<T1, T2> : IEnumerable<(T1 Key, T2 Value)>, IEnumerable
{
	private Dictionary<T1, T2> m_Dictionary = new Dictionary<T1, T2>();

	private SortedSet<(T2 Value, T1 Key)> m_SortedSet = new SortedSet<(T2, T1)>();

	public int Count => m_Dictionary.Count;

	public T2 this[T1 key]
	{
		get
		{
			return m_Dictionary[key];
		}
		set
		{
			if (m_Dictionary.TryGetValue(key, out var value2))
			{
				m_SortedSet.Remove((value2, key));
			}
			m_Dictionary[key] = value;
			m_SortedSet.Add((value, key));
		}
	}

	public bool TryGetValue(T1 key, out T2 value)
	{
		return m_Dictionary.TryGetValue(key, out value);
	}

	public void Add(T1 key, T2 value)
	{
		m_Dictionary.Add(key, value);
		m_SortedSet.Add((value, key));
	}

	public bool ContainsKey(T1 key)
	{
		return m_Dictionary.ContainsKey(key);
	}

	public bool Remove(T1 key)
	{
		if (!m_Dictionary.TryGetValue(key, out var value))
		{
			return false;
		}
		m_Dictionary.Remove(key);
		m_SortedSet.Remove((value, key));
		return true;
	}

	public IEnumerator<(T1 Key, T2 Value)> GetEnumerator()
	{
		foreach (var item in m_SortedSet)
		{
			yield return (Key: item.Key, Value: item.Value);
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	IEnumerator<(T1 Key, T2 Value)> IEnumerable<(T1 Key, T2 Value)>.GetEnumerator()
	{
		throw new NotImplementedException();
	}

	public void Clear()
	{
		m_SortedSet.Clear();
		m_Dictionary.Clear();
	}
}

}
