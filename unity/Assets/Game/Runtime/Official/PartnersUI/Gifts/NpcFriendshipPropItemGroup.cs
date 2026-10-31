// Migrated selected official client; see partner-gift-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PlayerPropItem = Zjcs.Official.ProfessionUI.ProfessionPlayerPropItem;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using PropConfigUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyConfig;
using LanguageUtils = Zjcs.Official.ProfessionUI.ProfessionPropertyLanguage;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipNpcInfo;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using System.Collections.Generic;
using UnityEngine;

public class NpcFriendshipPropItemGroup : MonoBehaviour, IGameObjectPoolItem
{
    private IPartnerFriendshipUiServices services;
    public void Configure(IPartnerFriendshipUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
        foreach (var item in propItems) item.Configure(value);
    }

	[SerializeField]
	private List<NpcFriendshipPropItem> propItems;

	public void Init(List<(PropType propType, long value)> propList, bool showBg, List<(PropType propType, long value)> addPropsList = null)
	{
		_ = services.IsTravelFunctionLimited;
		for (int i = 0; i < propItems.Count; i++)
		{
			NpcFriendshipPropItem npcFriendshipPropItem = propItems[i];
			CanvasGroup component = ((Component)npcFriendshipPropItem).GetComponent<CanvasGroup>();
			if (i < propList.Count)
			{
				(PropType, long) tuple = propList[i];
				long addVal = ((addPropsList != null && i < addPropsList.Count) ? addPropsList[i].value : 0);
				component.SetAlphaAndInteractable(1f);
				npcFriendshipPropItem.Init(tuple.Item1, tuple.Item2, showBg, addVal);
			}
			else
			{
				component.SetAlphaAndInteractable(0f);
			}
		}
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}

}
