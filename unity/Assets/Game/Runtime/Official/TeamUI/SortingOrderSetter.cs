namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using UnityEngine;
using UnityEngine.Rendering;

public class SortingOrderSetter : MonoBehaviour
{
	public int mySortingOrderOffset;

	private WorldPopupWindow _popupWindow;

	private WorldPopupWindow myWorldPopupWindow
	{
		get
		{
			if ((Object)(object)_popupWindow == (Object)null)
			{
				_popupWindow = ((Component)this).GetComponentInParent<WorldPopupWindow>();
			}
			return _popupWindow;
		}
	}

	private Canvas myCanvas
	{
		get
		{
			if ((Object)(object)myWorldPopupWindow != (Object)null)
			{
				return myWorldPopupWindow.Canvas;
			}
			return ((Component)((Component)this).transform).GetComponentInParent<Canvas>(false);
		}
	}

	private void OnEnable()
	{
		if ((Object)(object)myWorldPopupWindow != (Object)null)
		{
			myWorldPopupWindow.OnCanvasSortingOrderChanged += new Action(OnWorldPopupWindowCanvasSortingOrderChanged);
		}
		SetOrder();
	}

	private void OnDisable()
	{
		if ((Object)(object)myWorldPopupWindow != (Object)null)
		{
			myWorldPopupWindow.OnCanvasSortingOrderChanged -= new Action(OnWorldPopupWindowCanvasSortingOrderChanged);
		}
	}

	[ContextMenu("重设SortingOrder")]
	private void SetOrder()
	{
		if (!((Object)(object)myCanvas == (Object)null))
		{
			int sortingOrder = myCanvas.sortingOrder + mySortingOrderOffset;
			Canvas component = ((Component)this).GetComponent<Canvas>();
			if ((Object)(object)component != (Object)null)
			{
				component.overrideSorting = true;
				component.sortingOrder = sortingOrder;
			}
			SortingGroup component2 = ((Component)this).GetComponent<SortingGroup>();
			if ((Object)(object)component2 != (Object)null)
			{
				component2.sortingOrder = sortingOrder;
			}
		}
	}

	private void OnWorldPopupWindowCanvasSortingOrderChanged()
	{
		SetOrder();
	}
}

}
