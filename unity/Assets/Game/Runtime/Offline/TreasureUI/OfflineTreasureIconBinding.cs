using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.WorldUI;
using TMPro;

namespace Zjcs.Offline.TreasureUI
{
    public static class OfflineTreasureIconBinding
    {
        // Instantiate under an inactive staging parent, then bind before original OnEnable/Start.
        public static void Bind(GameObject root,IProfessionItemIconContext context,ITreasureIconServices services,
            IItemIconAttachmentManager attachments,Func<string,string> localize,Func<TMP_FontAsset> font)
        {
            if(root==null||context==null||services==null||attachments==null||localize==null||font==null)throw new ArgumentNullException();
            if(root.activeInHierarchy)throw new InvalidOperationException("Bind the original treasure hierarchy before activation.");
            foreach(var icon in root.GetComponentsInChildren<ItemIcon>(true))icon.Bind(context);
            foreach(var icon in root.GetComponentsInChildren<ItemIconTreasure>(true))icon.ConfigureTreasure(services);
            foreach(var tip in root.GetComponentsInChildren<TreasurePlayerRedDotItemBase>(true))tip.ConfigureTreasure(services);
            foreach(var tip in root.GetComponentsInChildren<Zjcs.Official.HomeMainUiControls.TipDotObject>(true))tip.TipDotSystem=services.TipDotSystem;
            foreach(var attachment in root.GetComponentsInChildren<ItemIconAttachment>(true))attachment.Bind(attachments);
            foreach(var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
            {text.Localizer=localize;text.FontProvider=font;}
        }
    }
}
