// Migrated from selected official sources; see partner-ui-helpers-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LanguageUtils = Zjcs.Official.PartnersUI.PartnerUiLanguage;
using UnityEngine;

public class FilterGroupForProfession : CommonFilterGroup<Profession>
{
	public override CommonFilterType FilterGroupType => CommonFilterType.Profession;

	protected int AddNewBtnNodes(Transform slot, int count)
	{
		if (count > 0)
		{
			GameObject gameObject = ((Component)slot.GetChild(0)).gameObject;
			for (int i = 0; i < count; i++)
			{
				PartnerUiInstantiation.Instantiate(gameObject, slot);
			}
		}
		return slot.childCount;
	}

	public void Init(params Profession[] types)
	{
		Transform itemContainer = ItemContainer;
		int childCount = itemContainer.childCount;
		childCount = AddNewBtnNodes(itemContainer, types.Length - childCount);
		for (int i = 0; i < childCount; i++)
		{
			Transform child = itemContainer.GetChild(i);
			if (i < types.Length)
			{
				SwitchShowBtn component = ((Component)child).GetComponent<SwitchShowBtn>();
				((Component)child).GetComponent<FilterGroupItemForProfession>().FilterType = types[i];
				component.SetBtnTextKey(LanguageUtils.GetProfessionNameKey(types[i]));
				((Component)child).gameObject.SetActive(true);
			}
			else
			{
				((Component)child).gameObject.SetActive(false);
			}
		}
		InitAllItems();
	}
}

}
