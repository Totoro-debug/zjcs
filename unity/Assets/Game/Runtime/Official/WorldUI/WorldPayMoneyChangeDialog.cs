using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    // PayMoneyChangeDialog.OnEnableAndInited / RefreshMax(count > 0) /
    // ShowChangeCount. Transactions and window ownership are supplied locally.
    public sealed class WorldPayMoneyChangeDialog : MonoBehaviour
    {
        public WorldItemIcon FlareStarItemIcon, DiamondItemIcon;
        public TMP_Text ExchangeFixedTxt;
        public GameObject CountSliderGo;
        public WorldPopupWindow PopupWindow;

        public void Init(string fixedText, string countText, string resultCountText,
            Sprite flare, Sprite diamond, Sprite quality, string flareName, string diamondName)
        {
            FlareStarItemIcon.Init(flare, quality, countText);
            FlareStarItemIcon.SetBottomName(flareName);
            DiamondItemIcon.Init(diamond, quality, resultCountText);
            DiamondItemIcon.SetBottomName(diamondName);
            ExchangeFixedTxt.text = fixedText;
            ExchangeFixedTxt.gameObject.SetActive(true);
            CountSliderGo.SetActive(false);
            PopupWindow.Show();
        }
    }
}
