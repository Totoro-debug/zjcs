namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;

public struct CurrencyItemData
{
	public PlayerItemType Type;

	public PlayerItemUIItemIdProvider ItemIdProivder;

	public CurrencyItemData(PlayerItemType type)
	{
		Type = type;
		ItemIdProivder = default;
	}

	public CurrencyItemData(PlayerItemType type, PlayerItemUIItemIdProvider playerItemUIItemIdProvider)
	{
		Type = type;
		ItemIdProivder = playerItemUIItemIdProvider;
	}
}

}
