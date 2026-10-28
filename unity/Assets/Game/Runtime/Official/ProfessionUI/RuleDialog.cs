using System;
using System.Collections.Generic;
using Zjcs.Official.WorldUI;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
// client-source/RuleDialog.cs. Original overloads, pooled rows and refresh order.
// The host supplies the original RuleTipInfoParser result through this interface.
public class RuleDialog : MonoBehaviour
{
    [NonSerialized] public IProfessionRuleTipData Rules;
	[SerializeField]
	private WorldMultiLanguageText mTitle;

	[SerializeField]
	private Transform mTipSlot;

	[SerializeField]
	private GameObject mTipInfoPrefab;

	[SerializeField]
	private List<TipInfo> mTips = new List<TipInfo>();

	private void OnDisable()
	{
		int count = mTips.Count;
		for (int i = 0; i < count; i++)
		{
			((Component)mTips[i]).gameObject.SetActive(false);
		}
	}

	public void SetRule(string ruleKey)
	{
		GetRuleInfo(ruleKey, out var titleKey, out var tipKeys);
		mTitle.SetKey(titleKey);
		int count = tipKeys.Count;
		EnsureTipSlot(count);
		for (int i = 0; i < count; i++)
		{
			TipInfo tipInfo = mTips[i];
			string key = tipKeys[i];
			((Component)tipInfo).gameObject.SetActive(true);
			tipInfo.SetKey(key);
		}
	}

	public void SetRule(string ruleKey, List<ProfessionRuleTextKeyArg> extRules, bool extraShowImg)
	{
		GetRuleInfo(ruleKey, out var titleKey, out var tipKeys);
		mTitle.SetKey(titleKey);
		int count = tipKeys.Count + extRules.Count;
		EnsureTipSlot(count);
		int num = 0;
		foreach (string item in tipKeys)
		{
			TipInfo tipInfo = mTips[num++];
			((Component)tipInfo).gameObject.SetActive(true);
			tipInfo.SetKey(item);
		}
		foreach (ProfessionRuleTextKeyArg extRule in extRules)
		{
			TipInfo tipInfo2 = mTips[num++];
			((Component)tipInfo2).gameObject.SetActive(true);
			tipInfo2.SetKey(extraShowImg, extRule.Key, extRule.Args);
		}
	}

	public void SetRuleWithShowDelegate(string ruleKey, Action<TipInfo, string, int> showDelegate)
	{
		GetRuleInfo(ruleKey, out var titleKey, out var tipKeys);
		mTitle.SetKey(titleKey);
		int count = tipKeys.Count;
		EnsureTipSlot(count);
		for (int i = 0; i < count; i++)
		{
			TipInfo tipInfo = mTips[i];
			string arg = tipKeys[i];
			((Component)tipInfo).gameObject.SetActive(true);
			showDelegate?.Invoke(tipInfo, arg, i);
		}
	}

	private void GetRuleInfo(string ruleKey, out string titleKey, out IReadOnlyList<string> tipKeys)
	{
        if (Rules == null) throw new InvalidOperationException("Original rule-tip data is unbound.");
        titleKey = Rules.GetTitleKey(ruleKey);
        tipKeys = Rules.GetTipsKey(ruleKey);
	}

	public void SetRule(ProfessionRuleTextKeyArg title, List<ProfessionRuleTextKeyArg> rules)
	{
		mTitle.SetKey(title.Key, title.Args);
		int count = rules.Count;
		EnsureTipSlot(count);
		for (int i = 0; i < count; i++)
		{
			TipInfo tipInfo = mTips[i];
			((Component)tipInfo).gameObject.SetActive(true);
			ProfessionRuleTextKeyArg textKeyArgWrap = rules[i];
			tipInfo.SetKey(textKeyArgWrap.Key, textKeyArgWrap.Args);
		}
	}

	private void EnsureTipSlot(int count)
	{
		if (mTips.Count < count)
		{
			int num = count - mTips.Count;
			for (int i = 0; i < num; i++)
			{
			    // Original UIUtils.Instantiate(GameObject, parent, All): preserve its
                    // local-position/rotation/scale reset and activation order.
                    GameObject tip = UnityEngine.Object.Instantiate(mTipInfoPrefab, mTipSlot, false);
                    tip.transform.localPosition = Vector3.zero;
                    tip.transform.localRotation = Quaternion.identity;
                    tip.transform.localScale = Vector3.one;
                    tip.SetActive(true);
                    mTips.Add(tip.GetComponent<TipInfo>());
			}
		}
	}
}
}
