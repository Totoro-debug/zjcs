namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using System.Collections.Generic;

public interface ITopBarManager
{
	TopBarItem BorrowTopBar(List<CurrencyItemData> data);

	void ReturnTopBar(TopBarItem topBar);

	void UpdateTopBar(TopBarItem topBar, List<CurrencyItemData> data);
}

}
