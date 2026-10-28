using System;
using UnityEngine;
using Zjcs.Official.EntityRadarCost;
using Zjcs.Official.EntityWorldInput;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Offline.World.NativeMap14EntityAssets;
using Zjcs.Offline.World.NativeMap14HeroStatues;
namespace Zjcs.Offline.World.NativeMap14RadarTips
{
    public static class NativeMap14RadarTipBinding
    {
        public static void Bind(GameObject instance,IRadarStageCostQueries costs,IRadarItemIconQueries icons,
            IWorldInputResourceLoader resources,ILanguageTypeService language,ILocalizationTextService localization,
            IMultiLanguageFontService fonts,IMultiLanguageUIService ui,NativeMap14EntitySpriteLibrary sprites)
        {
            if(!instance)throw new ArgumentNullException(nameof(instance));
            if(instance.activeInHierarchy)throw new InvalidOperationException("Bind original radar services before activation.");
            if(costs==null||icons==null||resources==null||language==null||localization==null||fonts==null||ui==null||sprites==null)
                throw new ArgumentNullException("Original radar presentation services are required.");
            foreach(var tip in instance.GetComponentsInChildren<MapControllerEntityRadarTipStageCost>(true))
            {tip.CostQueries=costs;tip.ItemIcons=icons;tip.ResourceLoader=resources;}
            foreach(var text in instance.GetComponentsInChildren<MultiLanguageText>(true))
            {text.BindLanguageServices(ui,language);text.LocalizationTextService=localization;text.MultiLanguageFontService=fonts;}
            foreach(var binding in instance.GetComponentsInChildren<NativeMap14HeroStatueSprites>(true))binding.Bind(sprites);
        }
    }
}
