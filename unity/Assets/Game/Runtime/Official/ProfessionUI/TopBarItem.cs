namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using System.Collections.Generic;
using UnityEngine;

public class TopBarItem : MonoBehaviour, IGameObjectPoolItem
{
	[SerializeField]
	private Transform mSlot;

	private List<IProfessionCurrencyItem> mCurrencyItems = new List<IProfessionCurrencyItem>();

	private void OnEnable()
	{


		((Component)this).transform.localPosition = Vector3.zero;
		((Component)this).transform.localScale = Vector3.one;
	}

	public void SetCurrencyItem(IProfessionCurrencyItem item)
	{


		item.Component.transform.SetParent(mSlot);
		RectTransform component = item.Component.GetComponent<RectTransform>();
		((Transform)component).localPosition = Vector3.zero;
		((Transform)component).localScale = Vector3.one;
		mCurrencyItems.Add(item);
	}

	public List<IProfessionCurrencyItem> GetAllCurrencyItems()
	{
		return mCurrencyItems;
	}

	public void OnGameObjectPoolClear()
	{
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void SetStopUpdateCount(bool isStop)
	{
		foreach (IProfessionCurrencyItem mCurrencyItem in mCurrencyItems)
		{
			mCurrencyItem.SetStopUpdateCount(isStop);
		}
	}
}

}
