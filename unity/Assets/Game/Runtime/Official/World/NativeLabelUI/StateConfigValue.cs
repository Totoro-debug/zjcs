// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;

[Serializable]
public struct StateConfigValue
{
	[SerializeField]
	private int mValue;

	public static implicit operator int(StateConfigValue value)
	{
		return value.mValue;
	}

	public static implicit operator StateConfigValue(int value)
	{
		return new StateConfigValue
		{
			mValue = value
		};
	}

	public override int GetHashCode()
	{
		return mValue.GetHashCode();
	}

	public override string ToString()
	{
		return mValue.ToString();
	}
}

}
