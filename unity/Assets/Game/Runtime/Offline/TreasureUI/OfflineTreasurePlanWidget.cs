using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.TreasureUI
{
    // The enclosing original panel owns the layout slot, selected regional font, moderation projection and audio.
    public sealed class OfflineTreasurePlanWidget : IDisposable
    {
        public GameObject Root { get; }
        public PlanSwitchWidget Widget { get; }
        private readonly OfflineTreasurePlanActions actions;
        public OfflineTreasurePlanWidget(Transform parent,IBattlePlanWidgetServices services,IWorldContentModerationServices moderation,
            TMP_FontAsset font,Func<string,string> localize,Action<AudioClip,GameObject> postSound,
            OfflineTreasureSlotService slots,Action<ProgressionResult> failed)
        {
            if(parent==null || font==null || localize==null || services==null || moderation==null || postSound==null)
                throw new ArgumentNullException("Bind the enclosing original UI services before creating its plan widget.");
            var prefab=Resources.Load<GameObject>("Treasure/NativePlanWidget/PlanSwitchWidget");
            if(prefab==null) throw new InvalidOperationException("Original treasure plan widget prefab is unavailable.");
            var staging=new GameObject("Original plan widget service injection");staging.SetActive(false);
            try {
                Root=Object.Instantiate(prefab,staging.transform,false);
                Widget=Root.GetComponent<PlanSwitchWidget>();
                if(Widget==null) throw new InvalidOperationException("Original plan widget controller is unbound.");
                Widget.Configure(services);
                foreach(var mask in Root.GetComponentsInChildren<WorldContentModerationClickMask>(true)) mask.Configure(moderation);
                foreach(var text in Root.GetComponentsInChildren<TMP_Text>(true)) text.font=font;
                foreach(var text in Root.GetComponentsInChildren<WorldMultiLanguageText>(true)) {text.Localizer=localize;text.FontProvider=()=>font;}
                foreach(var sound in Root.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post=postSound;
                actions=new OfflineTreasurePlanActions(Widget,services,slots,failed);
                Root.SetActive(false);Root.transform.SetParent(parent,false);
            }
            catch {if(Root!=null)Object.Destroy(Root);throw;}
            finally {Object.Destroy(staging);}
        }
        public void Init(PlanSwitchWidget.PlanIndexSource source=PlanSwitchWidget.PlanIndexSource.ActivePlanIndex,int selected=0,bool skipEmpty=false,bool rename=true,bool clear=true)
        {actions.RefreshConfirmed();Widget.Init(BattlePlanType.Treasure,source,selected,skipEmpty,rename,clear);}
        public void Dispose() {actions?.Dispose();if(Root!=null){Root.SetActive(false);Object.Destroy(Root);}}
    }
}
