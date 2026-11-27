using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.ItemDetailUI
{
    // UIUtils.SetUIGray: retain the TMP effect and legacy text/image branches.
    public static class ItemDetailUiGray
    {
        public static void Apply(GameObject target, bool gray, Material imageGray, Material textGray)
        {
            foreach (var graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TextMeshProUGUI)
                {
                    if (gray)
                    {
                        var effect = graphic.GetComponent<TMPEffect>() ?? graphic.gameObject.AddComponent<TMPEffect>();
                        effect.SetGray(true);
                    }
                    else
                    {
                        var effect = graphic.GetComponent<TMPEffect>();
                        if (effect) effect.SetGray(false);
                    }
                }
                else if (graphic is Text) graphic.material = gray ? textGray : null;
                else if (graphic is Image || graphic is RawImage) graphic.material = gray ? imageGray : null;
            }
        }
    }
}
