// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StateKVMatch<TValue> : IEnumerable<MonoKeyValuePair<StateConfigValue, TValue>>, IEnumerable
{
	[SerializeField]
	private List<MonoKeyValuePair<StateConfigValue, TValue>> mSerializeBuffer;

	public bool TryGetValue(StateConfigValue key, out TValue value)
	{
		foreach (MonoKeyValuePair<StateConfigValue, TValue> item in mSerializeBuffer)
		{
			if ((int)item.Key == (int)key)
			{
				value = item.Value;
				return true;
			}
		}
		value = default;
		return false;
	}

	public IEnumerator<MonoKeyValuePair<StateConfigValue, TValue>> GetEnumerator()
	{
		return mSerializeBuffer.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return mSerializeBuffer.GetEnumerator();
	}
}

}
