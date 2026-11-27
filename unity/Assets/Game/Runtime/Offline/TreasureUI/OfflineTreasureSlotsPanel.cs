using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.BattlePlanUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureSlots;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.WorldUI;
using Object=UnityEngine.Object;

namespace Zjcs.Offline.TreasureUI
{
    // Original enclosing panel supplies its slot, scene resource loader, font, moderation, audio and player owner.
    public sealed class OfflineTreasureSlotsPanel : IDisposable
    {
        public GameObject Root{get;}
        public TreasureSlotsPanelContent Content{get;}
        public PlanSwitchWidget PlanWidget{get;}
        public OfflineTreasureSlotsPanel(Transform parent,ITreasureSlotsPanelServices services,IHomeHudResourceLoader resources,
            IWorldContentModerationServices moderation,TMP_FontAsset font,Func<string,string> localize,Action<AudioClip,GameObject> sound)
        {
            if(!parent||services==null||resources==null||moderation==null||!font||localize==null||sound==null)throw new ArgumentNullException();
            var prefab=Resources.Load<GameObject>("Treasure/NativeSlots/TreasureSlotsPanelContent");
            if(!prefab)throw new InvalidOperationException("The original treasure slots panel is unavailable.");
            var staging=new GameObject("Original treasure slots service injection");staging.SetActive(false);
            try {
                Root=Object.Instantiate(prefab,staging.transform,false);
                Root.AddComponent<HomeHudResourceScope>().Loader=resources;
                Content=Root.GetComponent<TreasureSlotsPanelContent>();
                if(!Content)throw new InvalidOperationException("The original slot panel controller is unbound.");
                Content.Configure(services);
                foreach(var slot in Root.GetComponentsInChildren<TreasureSlotItem>(true))slot.Configure(services);
                foreach(var widget in Root.GetComponentsInChildren<PlanSwitchWidget>(true))widget.Configure(services.Plans);
                PlanWidget=Root.GetComponentInChildren<PlanSwitchWidget>(true);
                foreach(var mask in Root.GetComponentsInChildren<WorldContentModerationClickMask>(true))mask.Configure(moderation);
                foreach(var text in Root.GetComponentsInChildren<TMP_Text>(true))text.font=font;
                foreach(var text in Root.GetComponentsInChildren<WorldMultiLanguageText>(true)){text.Localizer=localize;text.FontProvider=()=>font;}
                foreach(var click in Root.GetComponentsInChildren<WorldButtonSound>(true))click.Post=sound;
                Root.SetActive(false);Root.transform.SetParent(parent,false);
            }
            catch{if(Root)Object.Destroy(Root);throw;}
            finally{Object.Destroy(staging);}
        }
        public void Show(ElementType element)
        {
            if(!Root)throw new ObjectDisposedException(nameof(OfflineTreasureSlotsPanel));
            // The original active window receives its catalog selection after scene services are enabled.
            Root.SetActive(true);Content.Init(element);
        }
        public void Dispose(){if(Root){Root.SetActive(false);Object.Destroy(Root);}}
    }
}
