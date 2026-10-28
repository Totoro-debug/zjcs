namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class AppearanceAnimNameCombineRule : ScriptableObject, ISerializationCallbackReceiver
{
	[SerializeField]
	private List<AnimCombineRule> mRules;

	private readonly Dictionary<string, AnimCombineRule> mRuleDict = new Dictionary<string, AnimCombineRule>();

	public IReadOnlyList<AnimCombineRule> Rules => mRules;

	public AnimCombineRule GetRule(string key)
	{
		mRuleDict.TryGetValue(key, out var value);
		return value;
	}

	public void OnBeforeSerialize()
	{
	}

	public void OnAfterDeserialize()
	{
		mRuleDict.Clear();
		foreach (AnimCombineRule mRule in mRules)
		{
			mRuleDict.Add(mRule.Key, mRule);
		}
	}
}

}
