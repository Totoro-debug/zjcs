using System;
using UnityEngine;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Offline.World.NativeMap13Labels
{
    public static class Map13LabelBinding
    {
        // The official prefab already contains its TextMeshPro MeshRenderer.
        // Set the isolated original font before TMP Awake can populate an atlas.
        public static void Bind(GameObject instance, IMap13LabelServices map, IMultiLanguageUIService ui,
            ILanguageTypeService language, ILocalizationTextService localization, IMultiLanguageFontService fonts)
        {
            if (instance.activeInHierarchy) throw new InvalidOperationException("Bind original region-label services before activation.");
            foreach (var component in instance.GetComponentsInChildren<UIBase>(true)) component.Services = map;
            foreach (var component in instance.GetComponentsInChildren<MapRegionDescItemUI>(true)) component.Services = map;
            foreach (var component in instance.GetComponentsInChildren<UIColorByLevel>(true)) component.Services = map;
            foreach (var component in instance.GetComponentsInChildren<MultiLanguageText>(true))
            {
                component.GetComponent<TMPro.TMP_Text>().font = fonts.GetMultiLanguageFontAsset().FontAsset;
                component.BindLanguageServices(ui, language);
                component.LocalizationTextService = localization;
                component.MultiLanguageFontService = fonts;
            }
        }
    }
}
