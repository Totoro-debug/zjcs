using System;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

using Zjcs.Official.World.NativeMap14Labels;

namespace Zjcs.Offline.World.NativeMap14Labels
{
    public static class Map14LabelBinding
    {
        // Explicit local replacement of the original injection, before original Awake/Start.
        public static void Bind(GameObject instance, IMap14LabelServices map, IMultiLanguageUIService ui,
            ILanguageTypeService language, ILocalizationTextService localization, IMultiLanguageFontService fonts)
        {
            if (instance.activeInHierarchy) throw new InvalidOperationException("Bind original region-label services before activation.");
            foreach (var component in instance.GetComponentsInChildren<UIBase>(true)) component.Services = map;
            foreach (var component in instance.GetComponentsInChildren<MapRegionDescItemUI>(true)) component.Services = map;
            foreach (var component in instance.GetComponentsInChildren<UIColorByLevel>(true)) component.Services = map;
            foreach (var component in instance.GetComponentsInChildren<MultiLanguageText>(true))
            {
                // TMP can add glyphs in Awake, before MultiLanguageText.Start refreshes
                // its font. Bind the same original font data to its local instance first.
                component.GetComponent<TMPro.TMP_Text>().font = fonts.GetMultiLanguageFontAsset().FontAsset;
                component.BindLanguageServices(ui, language);
                component.LocalizationTextService = localization;
                component.MultiLanguageFontService = fonts;
            }
        }
    }
}
