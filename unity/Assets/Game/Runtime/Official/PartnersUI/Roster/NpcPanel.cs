// Original panel branches; see panel-controller-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NpcPanel : MonoBehaviour
{
    private IPartnerPanelServices services;
    public void Configure(IPartnerPanelServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
    }

	[SerializeField]
	private PanelTitle mPanelTitle;

	[SerializeField]
	private GameObject mNpcListGo;

	[SerializeField]
	private GameObject mNpcDialogueListGo;

	[SerializeField]
	private GameObject mTabGroupGo;

	[SerializeField]
	private WorldTabGroup mTabGroup;

	[SerializeField]
	private MonoDictionary<NpcPanelTabType, WorldTabItem> mTabType2TabItemDict;

	[SerializeField]
	private WorldStateSwitcher<NpcPanelTabType> mTabSwitcher;

	private NpcPanelTabType mDefaultTabType;

	protected void Awake()
	{
		((UnityEvent<WorldTabItem, bool>)mTabGroup.OnTabItemChanged).AddListener((UnityAction<WorldTabItem, bool>)OnTabItemChanged);
	}

	private void OnDestroy()
	{
		((UnityEvent<WorldTabItem, bool>)mTabGroup.OnTabItemChanged).RemoveListener((UnityAction<WorldTabItem, bool>)OnTabItemChanged);
	}

	protected void OnEnable()
	{
		bool active = UpdateTabItem() > 1;
		mTabGroupGo.SetActive(active);
		NpcPanelTabType firstActiveTabType = mDefaultTabType;
		if (!((Component)mTabType2TabItemDict[firstActiveTabType]).gameObject.activeSelf)
		{
			firstActiveTabType = GetFirstActiveTabType(firstActiveTabType);
		}
		mTabGroup.ResetTabItem(needNotify: true, mTabType2TabItemDict[firstActiveTabType]);
	}

	protected void OnDisable()
	{
		mDefaultTabType = NpcPanelTabType.NpcList;
	}

	private void OnTabItemChanged(WorldTabItem changeItem, bool isOn)
	{
		NpcPanelTabType npcPanelTabType = NpcPanelTabType.NpcList;
		NpcPanelTabType npcPanelTabType2 = default;
		WorldTabItem tabItem = default;
		foreach (KeyValuePair<NpcPanelTabType, WorldTabItem> item in mTabType2TabItemDict)
		{
			npcPanelTabType2 = item.Key; tabItem = item.Value;
			NpcPanelTabType npcPanelTabType3 = npcPanelTabType2;
			WorldTabItem tabItem2 = tabItem;
			if ((Object)(object)changeItem == (Object)(object)tabItem2)
			{
				npcPanelTabType = npcPanelTabType3;
				break;
			}
		}
		mTabSwitcher.SetState(npcPanelTabType);
		switch (npcPanelTabType)
		{
		case NpcPanelTabType.NpcList:
			if (isOn)
			{
				mPanelTitle.SetKey("ui_npc_panel_addressbook");
			}
			break;
		case NpcPanelTabType.Dialogue:
			services.InitDialogueList(mNpcDialogueListGo);
			if (isOn)
			{
				mPanelTitle.SetKey("ui_npc_panel_dialogue");
			}
			break;
		case NpcPanelTabType.StarGod:
			if (isOn)
			{
				mPanelTitle.SetKey("ui_npc_star_god_title");
			}
			break;
		}
	}

	private int UpdateTabItem()
	{
		IPartnerPanelServices mainPlayer = services;
		int num = 0;
		NpcPanelTabType npcPanelTabType = default;
		int functionType = default;
		foreach (KeyValuePair<NpcPanelTabType, int> item in NpcPanelTabs.Tab2FunctionTypeDict)
		{
			npcPanelTabType = item.Key; functionType = item.Value;
			NpcPanelTabType key = npcPanelTabType;
			int functionType2 = functionType;
			bool flag = !mainPlayer.IsFunctionLimit(functionType2);
			((Component)mTabType2TabItemDict[key]).gameObject.SetActive(flag);
			if (flag)
			{
				num++;
			}
		}
		return num;
	}

	private NpcPanelTabType GetFirstActiveTabType(NpcPanelTabType ignoreType)
	{
		NpcPanelTabType npcPanelTabType = default;
		WorldTabItem tabItem = default;
		foreach (KeyValuePair<NpcPanelTabType, WorldTabItem> item in mTabType2TabItemDict)
		{
			npcPanelTabType = item.Key; tabItem = item.Value;
			NpcPanelTabType npcPanelTabType2 = npcPanelTabType;
			WorldTabItem tabItem2 = tabItem;
			if (npcPanelTabType2 != ignoreType && ((Component)tabItem2).gameObject.activeSelf)
			{
				return npcPanelTabType2;
			}
		}
		return mDefaultTabType;
	}

	public void SetDefaultShowTab(NpcPanelTabType tabType)
	{
		mDefaultTabType = tabType;
	}
}

}
