using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private void OpenPartnerRoster()
        {
            CloseDialog();
            var root = new GameObject("PartnerRosterDialog", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
            root.GetComponent<Button>().transition = Selectable.Transition.None;
            root.GetComponent<Button>().onClick.AddListener(CloseDialog);
            partnerRosterPanel = root.AddComponent<PartnerRosterPanelView>();
            partnerRosterPanel.Open(host.PartnerRosterAssets, host.PartnerRoster);
            activeDialog = root;
            activeDialogRenderer = null;
            activePanelKey = "partner_roster";
        }

        void IOfflinePartnerPresentation.BindNpcTipDot(TipDotNodeLinker linker, int npcClassId) { }
        void IOfflinePartnerPresentation.PostSound(AudioClip sound, GameObject owner)
        {
            if (sound == null || owner == null) return;
            var source = owner.GetComponent<AudioSource>() ?? owner.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f; source.PlayOneShot(sound);
        }
        void IOfflinePartnerPresentation.ShowFriendshipLevelUp(NpcRosterItem npc, long oldFriendship, int oldLevel)
            => Notify(npc == null ? "伙伴已结识" : "已结识伙伴 " + npc.NpcClassId);
        void IOfflinePartnerPresentation.ShowExclusiveGift(int itemId, Component owner)
            => Notify("礼物 " + itemId + " 可用于结识伙伴");
        void IOfflinePartnerPresentation.ShowNpcDetails(List<NpcRosterItem> npcs, int index)
            => Notify(index >= 0 && index < npcs.Count ? "伙伴详情 " + npcs[index].NpcClassId : "伙伴详情");
        void IOfflinePartnerPresentation.ShowError(int code) => Notify("伙伴操作不可用：" + code);
        void IOfflinePartnerPresentation.ShowProgressionResult(ProgressionResult result)
            => Notify(result?.Message ?? "伙伴操作未完成");
    }
}
