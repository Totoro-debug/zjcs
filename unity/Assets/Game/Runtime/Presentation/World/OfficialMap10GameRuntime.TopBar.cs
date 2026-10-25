using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMap10GameRuntime
    {
        private readonly List<WorldCurrencyBinding> worldCurrencies = new List<WorldCurrencyBinding>();
        private sealed class WorldCurrencyBinding
        {
            public OfficialMap11TopBarItem Item;
            public TMP_Text Text;
            public Color Normal, Tip, Limit;
        }

        private void InitializeWorldTopBar(OfficialMap11UiData data)
        {
            var handler = data.components.Single(value => value.panel == "WorldCanvas" && value.type == "TopBarHandler").fields;
            Transform slot = ui.Find("WorldCanvas", handler.mSlot).transform;
            GameObject bar = Instantiate(Resources.Load<GameObject>("Map11/UI/PublicTopBar"), slot, false);
            var barFields = data.components.Single(value => value.panel == "PublicTopBar" && value.type == "TopBarItem").fields;
            Transform list = bar.transform.Find(barFields.mSlot.Substring("/PublicTopBar/".Length));
            var fields = data.components.Single(value => value.panel == "MoneyUI" && value.type == "MePlayerItemUI").fields;
            OfficialMap11UiAssets icons = Resources.Load<OfficialMap11UiAssets>("Map11/UI/DynamicItems");
            foreach (var item in data.topBarItems)
            {
                GameObject currency = Instantiate(Resources.Load<GameObject>("Map11/UI/MoneyUI"), list, false);
                Transform Find(string path) => currency.transform.Find(path.Substring("/MoneyUI/".Length));
                Find(fields.mIcon).GetComponent<Image>().sprite = icons.itemSprites[System.Array.IndexOf(icons.itemIds, item.itemId)];
                Find(fields.mAddBtn).gameObject.SetActive(false); // PlayerDefaultItemUIHandler.ShowAddBtn.
                worldCurrencies.Add(new WorldCurrencyBinding { Item = item, Text = Find(fields.mCountText).GetComponent<TMP_Text>(),
                    Normal = fields.mNormalColor, Tip = fields.mTipColor, Limit = fields.mLimitColor });
            }
            RefreshWorldTopBar();
        }

        private void RefreshWorldTopBar()
        {
            foreach (var binding in worldCurrencies)
            {
                long count = progressionSave.InventoryCount(binding.Item.itemId);
                string text = count.ToString(CultureInfo.CurrentCulture);
                if (binding.Item.maxCount > 0)
                {
                    Color color = count >= binding.Item.maxCount ? binding.Tip : binding.Normal;
                    text = "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>/<color=#"
                        + ColorUtility.ToHtmlStringRGBA(binding.Limit) + ">" + binding.Item.maxCount.ToString(CultureInfo.CurrentCulture) + "</color>";
                }
                binding.Text.text = text;
            }
        }
    }
}
