using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

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
        {
            if (index < 0 || index >= npcs.Count) { Notify("伙伴详情"); return; }
            host.OpenPartnerDetail(npcs[index].NpcClassId);
        }
        void IOfflinePartnerPresentation.ShowError(int code) => Notify("伙伴操作不可用：" + code);
        void IOfflinePartnerPresentation.ShowProgressionResult(ProgressionResult result)
            => Notify(result?.Message ?? "伙伴操作未完成");
        void IOfflinePartnerDetailPresentation.ShowProgressionResult(ProgressionResult result)
            => Notify(result?.Message ?? "伙伴操作未完成");

        internal void OpenPartnerDetail(int npcId, OfficialPartnerDetailPropertyAssetCatalog assets,
            OfflinePartnerDetailPropertyServices services, OfflinePartnerGiftPlayer gifts)
        {
            CloseDialog();
            try
            {
                var catalog = OfficialPartnerAssetCatalog.LoadDefault();
                var config = OfficialPartnerGiftAssetCatalog.LoadDefault();
                var npcData = gifts.GetNpcByClassId(npcId);
                if (npcData == null) throw new InvalidOperationException("伙伴尚未结识");
                var npc = new OfflinePartnerDetailNpcData(host.Profile, npcData);
                var row = catalog.Rows("npc").FirstOrDefault(value => Convert.ToInt32(value["Id"]) == npcId);
                if (row == null) throw new InvalidOperationException("Official partner detail row is missing: " + npcId);
                var info = new PartnerDetailNpcInfo { Id = npcId,
                    TreasureClassId = Convert.ToInt32(row["TreasureClassId"]),
                    IsStarGodNpc = Convert.ToInt32(row["NpcRoleType"]) == 2,
                    Friendship = config.GetNpcInfo(npcId) };
                var root = new GameObject("PartnerDetailDialog", typeof(RectTransform), typeof(Image), typeof(Button));
                root.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
                root.GetComponent<Button>().transition = Selectable.Transition.None;
                root.GetComponent<Button>().onClick.AddListener(CloseDialog);
                var panel = root.AddComponent<PartnerDetailPanelView>();
                partnerDetailPanel = panel;
                panel.Open(assets, services, npc, info, new OfflinePartnerLocalizationService(assets.Texts, assets.Common.GiftTexts));
                activeDialog = root; activeDialogRenderer = null; activePanelKey = "partner_detail";
            }
            catch (Exception error) { partnerDetailPanel?.Close(); partnerDetailPanel = null; Notify("伙伴详情打开失败：" + error.Message); }
        }

        bool IOfflinePartnerDetailPresentation.CanNavigateNpc(int npcId) => false;
        PartnerDetailNpcTarget IOfflinePartnerDetailPresentation.CreateNpcTarget(int npcId) => new PartnerDetailNpcTarget { NpcClassId = npcId };
        void IOfflinePartnerDetailPresentation.GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback)
            => callback?.Invoke(new PartnerDetailLiteInfo { Level = host.Level, SubRank = host.SubRank,
                TargetData = new PartnerDetailNpcTarget { NpcClassId = target.NpcClassId, CreatePlanesRank = host.Rank } });
        void IOfflinePartnerDetailPresentation.PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target)
            => Notify("伙伴 " + target.NpcClassId + " 等级 " + host.Level);
        void IOfflinePartnerDetailPresentation.PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
            => Notify("伙伴好感度详情暂未独立展开");
        void IOfflinePartnerDetailPresentation.NavigateNpcPosition(int npcId, Action<PartnerDetailNavigateResult> complete)
            => complete?.Invoke(new PartnerDetailNavigateResult { IsSuccess = false, TipMsg = "伙伴位置导航暂不可用" });
        bool IOfflinePartnerDetailPresentation.IsNavigateTipValid(string tip) => !string.IsNullOrEmpty(tip);
        void IOfflinePartnerDetailPresentation.BroadcastNavigateTip(string tip) => Notify(tip);
        string IOfflinePartnerDetailPresentation.GetText(string key) => key;
        void IOfflinePartnerDetailPresentation.SystemMessage(string text) => Notify(text);
    }
}
