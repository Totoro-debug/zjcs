// Ported selected official client; see partner-star-god-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System.Collections.Generic;
using UnityEngine;

public class StarGodNpcItemGroupItem : MonoBehaviour
{
	[SerializeField]
	private List<StarGodNpcItem> mStarGodNpcItems;

	[SerializeField]
	private StarGodNpcItem mCenterStarGodNpcItems;

	[SerializeField]
	private WorldStateSwitcher<bool> mIsCenterStates;

	public int GroupMaxCount => mStarGodNpcItems.Count;

	public void Init(List<NpcRosterItem> npcDatas, bool isFirst)
	{
		bool flag = (npcDatas.Count <= 1) & isFirst;
		mIsCenterStates.SetState(flag);
		if (flag)
		{
			mCenterStarGodNpcItems.Init(npcDatas[0]);
			return;
		}
		for (int i = 0; i < mStarGodNpcItems.Count; i++)
		{
			bool active = i < npcDatas.Count;
			StarGodNpcItem starGodNpcItem = mStarGodNpcItems[i];
			((Component)starGodNpcItem).gameObject.SetActive(active);
			if (i < npcDatas.Count)
			{
				NpcRosterItem npcData = npcDatas[i];
				starGodNpcItem.Init(npcData);
			}
		}
	}
}

}
