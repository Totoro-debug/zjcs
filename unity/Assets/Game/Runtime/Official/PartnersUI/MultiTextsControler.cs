// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MultiTextsControler : MonoBehaviour
{
	[Tooltip("多语言文本控件列表")]
	public List<MultiLanguageText> MultiTextList;

	[Tooltip("唤醒时是否自动遍历，获取多语言文本控件")]
	public bool AutoTraverseOnAwake;

	public void SetMultiTextKey(string key, params object[] args)
	{
		for (int i = 0; i < MultiTextList.Count; i++)
		{
			MultiTextList[i].SetKey(key, args);
		}
	}

	private void TraverseNode(Transform node, Action<MultiLanguageText> action)
	{
		MultiLanguageText component = ((Component)node).GetComponent<MultiLanguageText>();
		if ((Object)(object)component != (Object)null)
		{
			action?.Invoke(component);
		}
		for (int i = 0; i < node.childCount; i++)
		{
			Transform child = ((Component)this).transform.GetChild(i);
			if ((Object)(object)child != (Object)null)
			{
				TraverseNode(child, action);
			}
		}
	}

	public virtual void Awake()
	{
		if (!AutoTraverseOnAwake)
		{
			return;
		}
		TraverseNode(((Component)this).transform, (MultiLanguageText text) =>
		{
			if (!MultiTextList.Contains(text))
			{
				MultiTextList.Add(text);
			}
		});
	}
}

}
