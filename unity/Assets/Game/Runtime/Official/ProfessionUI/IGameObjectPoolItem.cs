namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
public interface IGameObjectPoolItem
{
	void OnGameObjectPoolReset();

	void OnGameObjectPoolClear();
}

}
