// Selected original suit UI; see its source receipt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.TreasureProperties;
using PropType = System.Int32;
using Object = UnityEngine.Object;
namespace Zjcs.Official.TreasureDetailUI {

    public interface ITreasureSuitViewServices
    {
        bool HasItem(int itemId,long count);
        int GetPieceIdByTreasureId(int treasureClassId);
        Transform InstantiateSuitMember(Transform prefab,Transform parent);
        // The window owner initializes first, then applies these original operation flags.
        void PopupTreasure(int treasureClassId,bool canOperation);
        void PopupPiece(int pieceId,bool showButtons);
    }
    public sealed class TreasureSuitContent : MonoBehaviour
    {
        private ITreasureSuitViewServices Services;
        private int TreasureClassId;
        private bool mCanOperation;
        public void Configure(ITreasureSuitViewServices services) => Services=services??throw new ArgumentNullException(nameof(services));
        public void Show(int treasureClassId,bool canOperation,TreasureSuitInfo suit,Dictionary<int,int> activatedStars)
        {
            TreasureClassId=treasureClassId;mCanOperation=canOperation;
            SetSuitInfo(suit,activatedStars);
        }
	public GameObject mySuitRoot;

	public WorldMultiLanguageText mySuitTitle;

	public WorldMultiLanguageText mySuitCount;

	public Transform mySuitItemNameRoot;

	public Transform mySuitItemPrefab;

	public List<TreasureSuitPropItem> mySuitPropItems;

private void SetSuitInfo(TreasureSuitInfo suitInfo, Dictionary<int, int> activedStars)
	{
		bool flag = suitInfo != null;
		mySuitRoot.SetActive(flag);
		if (flag)
		{
			string itemNameKey = TreasureSuitLanguage.GetItemNameKey(suitInfo.ClassId);
			int count = activedStars.Count;
			mySuitCount.SetKey("treasureDialog_suit_count", new CultureTextArg(itemNameKey), count, suitInfo.Treasures.Count);
			DoRefreshSuitTreasureInfo(suitInfo, activedStars.Keys);
			DoRefreshSuitProps(suitInfo, activedStars);
		}
	}
private void DoRefreshSuitTreasureInfo(TreasureSuitInfo suitInfo, ICollection<int> activedTreasureId)
	{
		mySuitItemNameRoot.DestroyActivedChildren();
		for (int i = 0; i < suitInfo.Treasures.Count; i++)
		{
			Transform val = Services.InstantiateSuitMember(mySuitItemPrefab, mySuitItemNameRoot);
			int treasureClassId = suitInfo.Treasures[i];
			TreasureDetailSuitItem component = ((Component)val).GetComponent<TreasureDetailSuitItem>();
			component.Show(treasureClassId, activedTreasureId.Contains(treasureClassId));
			if (TreasureClassId != treasureClassId)
			{
				((UnityEvent)component.Button.onClick).AddListener((UnityAction)(() =>
				{
					PopupSuitDetailPanel(treasureClassId);
				}));
			}
		}
	}
private void DoRefreshSuitProps(TreasureSuitInfo suitInfo, Dictionary<int, int> activedStars)
	{
		List<TreasureSuitPropCondition> list = suitInfo.GetActivedCondition(activedStars).ToList();
		foreach (TreasureSuitPropItem mySuitPropItem in mySuitPropItems)
		{
			((Component)mySuitPropItem).gameObject.SetActive(false);
		}
		int num = 0;
		foreach (KeyValuePair<TreasureSuitPropCondition, (int, List<PropType>)> propDatum in suitInfo.PropData)
		{
			bool active = list.Contains(propDatum.Key);
			TreasureSuitPropCondition key = propDatum.Key;
			int item = propDatum.Value.Item1;
			foreach (PropType item2 in propDatum.Value.Item2)
			{
				mySuitPropItems[num].RefreshProp(key, item, item2, active);
				((Component)mySuitPropItems[num]).gameObject.SetActive(true);
				num++;
			}
		}
	}
    private void PopupSuitDetailPanel(int treasureClassId)
    {
        bool flag=Services.HasItem(treasureClassId,1L);
        if(!mCanOperation | flag)
        {
            Services.PopupTreasure(treasureClassId,mCanOperation);
            return;
        }
        int pieceIdByTreasureId=Services.GetPieceIdByTreasureId(treasureClassId);
        Services.PopupPiece(pieceIdByTreasureId,true);
    }

    }

}
