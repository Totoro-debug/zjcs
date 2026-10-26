// Migrated official Map13 world-label call path; see source receipt.
namespace Zjcs.Official.World.NativeMap13Labels
{
using Object = UnityEngine.Object;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

[Serializable]
public class MonoDictionary<TKey, TValue> : ISerializationCallbackReceiver, IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>, IEnumerable, IDictionary, ICollection, IReadOnlyDictionary<TKey, TValue>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>, ISerializable, IDeserializationCallback
{
	[SerializeField]
	private List<MonoKeyValuePair<TKey, TValue>> mSerializeBuffer;

	private Dictionary<TKey, TValue> mSerialized = new Dictionary<TKey, TValue>();

	public TValue this[TKey key]
	{
		get
		{
			return mSerialized[key];
		}
		set
		{
			mSerialized[key] = value;
		}
	}

	public int Count => mSerialized.Count;

	public Dictionary<TKey, TValue>.ValueCollection Values => mSerialized.Values;

	public Dictionary<TKey, TValue>.KeyCollection Keys => mSerialized.Keys;

	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => mSerialized.Keys;

	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => mSerialized.Values;

	ICollection<TKey> IDictionary<TKey, TValue>.Keys => mSerialized.Keys;

	ICollection<TValue> IDictionary<TKey, TValue>.Values => mSerialized.Values;

	ICollection IDictionary.Keys => mSerialized.Keys;

	ICollection IDictionary.Values => mSerialized.Values;

	bool ICollection.IsSynchronized => ((ICollection)mSerialized).IsSynchronized;

	object ICollection.SyncRoot => ((ICollection)mSerialized).SyncRoot;

	bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => ((ICollection<KeyValuePair<TKey, TValue>>)mSerialized).IsReadOnly;

	bool IDictionary.IsReadOnly => ((IDictionary)mSerialized).IsReadOnly;

	bool IDictionary.IsFixedSize => ((IDictionary)mSerialized).IsFixedSize;

	object IDictionary.this[object key]
	{
		get
		{
			return ((IDictionary)mSerialized)[key];
		}
		set
		{
			((IDictionary)mSerialized)[key] = value;
		}
	}

	public void Add(TKey key, TValue value)
	{
		mSerialized.Add(key, value);
	}

	public bool Remove(TKey key)
	{
		TValue val = default;
		mSerialized.Remove(key, out val);
		return mSerialized.Remove(key);
	}

	public bool Remove(TKey key, out TValue value)
	{
		return mSerialized.Remove(key, out value);
	}

	public void Clear()
	{
		mSerialized.Clear();
	}

	public bool ContainsKey(TKey key)
	{
		return mSerialized.ContainsKey(key);
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		return mSerialized.TryGetValue(key, out value);
	}

	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		mSerialized.GetObjectData(info, context);
	}

	public void OnDeserialization(object sender)
	{
		mSerialized.OnDeserialization(sender);
	}

	public Dictionary<TKey, TValue>.Enumerator GetEnumerator()
	{
		return mSerialized.GetEnumerator();
	}

	void IDictionary.Add(object key, object value)
	{
		((IDictionary)mSerialized).Add(key, value);
	}

	void IDictionary.Remove(object key)
	{
		((IDictionary)mSerialized).Remove(key);
	}

	bool IDictionary.Contains(object key)
	{
		return ((IDictionary)mSerialized).Contains(key);
	}

	IDictionaryEnumerator IDictionary.GetEnumerator()
	{
		return mSerialized.GetEnumerator();
	}

	void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> keyValuePair)
	{
		((ICollection<KeyValuePair<TKey, TValue>>)mSerialized).Add(keyValuePair);
	}

	bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> keyValuePair)
	{
		return ((ICollection<KeyValuePair<TKey, TValue>>)mSerialized).Remove(keyValuePair);
	}

	bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> keyValuePair)
	{
		return ((ICollection<KeyValuePair<TKey, TValue>>)mSerialized).Contains(keyValuePair);
	}

	IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
	{
		return mSerialized.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return mSerialized.GetEnumerator();
	}

	void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
	{
		((ICollection)mSerialized).CopyTo((Array)array, index);
	}

	void ICollection.CopyTo(Array array, int index)
	{
		((ICollection)mSerialized).CopyTo(array, index);
	}

	void ISerializationCallbackReceiver.OnBeforeSerialize()
	{
	}

	void ISerializationCallbackReceiver.OnAfterDeserialize()
	{
		if (mSerializeBuffer == null)
		{
			return;
		}
		mSerialized.Clear();
		foreach (MonoKeyValuePair<TKey, TValue> item in mSerializeBuffer)
		{
			mSerialized.TryAdd(item.Key, item.Value);
		}
	}
}

}
