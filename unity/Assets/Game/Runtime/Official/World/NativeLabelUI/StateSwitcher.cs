// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class StateSwitcher<T> where T : struct, IConvertible
{
	[SerializeReference]
	private List<IStateConfig> mStateConfigs;

	[SerializeField]
	private GameObject mOwner;

	public T State { get; protected set; }

	public void SetState(T state)
	{
		if (state is int state2)
		{
			Switch(state2);
		}
		else if (state is bool flag)
		{
			Switch(flag ? 1 : 0);
		}
		else
		{
			if (!typeof(T).IsEnum)
			{
				Debug.LogError("StateSwitcher不支持该类型:T");
				return;
			}
			Switch(Convert.ToInt32(state));
		}
		State = state;
	}

	private void Switch(int state)
	{
		try
		{
			foreach (IStateConfig mStateConfig in mStateConfigs)
			{
				mStateConfig.SetState(state);
			}
		}
		catch (Exception exception)
		{
			string value = string.Empty;
			if ((Object)(object)mOwner != (Object)null)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(((Object)mOwner).name);
				Transform val = mOwner.transform;
				while ((Object)(object)(val = val.parent) != (Object)null)
				{
					stringBuilder.Insert(0, '/');
					stringBuilder.Insert(0, ((Object)((Component)val).gameObject).name);
				}
				stringBuilder.Insert(0, "执行路径:");
				value = stringBuilder.ToString();
			}
			Debug.LogError($"{value}\n{exception}", mOwner);
		}
	}

	public void Clear()
	{
		foreach (IStateConfig mStateConfig in mStateConfigs)
		{
			mStateConfig.Clear();
		}
	}
}

}
