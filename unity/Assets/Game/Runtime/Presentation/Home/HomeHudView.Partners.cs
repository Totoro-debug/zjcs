using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

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
        {
            var npc = host.PartnerRoster.NpcRoster(true).FirstOrDefault(value => value.ExclusiveGiftId == itemId);
            if (npc != null && npc.MeetStatus == NpcMeetStatus.HadMeet) host.OpenPartnerGift(npc.NpcClassId);
            else Notify("礼物 " + itemId + " 可用于结识伙伴");
        }
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
                panel.Open(assets, services, npc, info, new OfflinePartnerLocalizationService(assets.Texts,
                    assets.Common.GiftTexts, host.PartnerFriendshipAssets.PropertyTexts));
                activeDialog = root; activeDialogRenderer = null; activePanelKey = "partner_detail";
            }
            catch (Exception error) { partnerDetailPanel?.Close(); partnerDetailPanel = null; Notify("伙伴详情打开失败：" + error.Message); }
        }

        internal void OpenPartnerGift(int npcId, OfficialPartnerGiftPageAssetCatalog assets,
            OfflinePartnerGiftUiServices services, OfflinePartnerGiftPlayer gifts)
        {
            CloseDialog();
            try
            {
                var npcData = gifts.GetNpcByClassId(npcId);
                if (npcData == null) throw new InvalidOperationException("伙伴尚未结识");
                var config = OfficialPartnerGiftAssetCatalog.LoadDefault();
                var info = config.GetNpcInfo(npcId);
                if (info == null) throw new InvalidOperationException("Official partner gift row is missing: " + npcId);
                var root = new GameObject("PartnerGiftDialog", typeof(RectTransform), typeof(Image), typeof(Button));
                root.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
                root.GetComponent<Button>().transition = Selectable.Transition.None;
                root.GetComponent<Button>().onClick.AddListener(CloseDialog);
                var panel = root.AddComponent<PartnerGiftPanelView>();
                partnerGiftPanel = panel;
                panel.Open(assets, services, npcData, info,
                    new OfflinePartnerLocalizationService(assets.GiftTexts, assets.RelationshipTexts,
                        host.PartnerFriendshipAssets.PropertyTexts),
                    () => Notify("伙伴好感度已更新"));
                activeDialog = root; activeDialogRenderer = null; activePanelKey = "partner_gift";
            }
            catch (Exception error) { partnerGiftPanel?.Close(); partnerGiftPanel = null; Notify("伙伴赠礼打开失败：" + error.Message); }
        }

        bool IOfflinePartnerDetailPresentation.CanNavigateNpc(int npcId) => false;
        PartnerDetailNpcTarget IOfflinePartnerDetailPresentation.CreateNpcTarget(int npcId) => new PartnerDetailNpcTarget { NpcClassId = npcId };
        void IOfflinePartnerDetailPresentation.GetLiteInfo(PartnerDetailNpcTarget target, PartnerDetailLiteInfoCallback callback)
            => callback?.Invoke(new PartnerDetailLiteInfo { Level = host.Level, SubRank = host.SubRank,
                TargetData = new PartnerDetailNpcTarget { NpcClassId = target.NpcClassId, CreatePlanesRank = host.Rank } });
        void IOfflinePartnerDetailPresentation.PopupNpcBrief(IWindowOperator owner, PartnerDetailNpcTarget target)
            => Notify("伙伴 " + target.NpcClassId + " 等级 " + host.Level);
        void IOfflinePartnerDetailPresentation.PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
            => OpenPartnerFriendshipDialog(initialize);
        void IOfflinePartnerDetailPresentation.NavigateNpcPosition(int npcId, Action<PartnerDetailNavigateResult> complete)
            => complete?.Invoke(new PartnerDetailNavigateResult { IsSuccess = false, TipMsg = "伙伴位置导航暂不可用" });
        bool IOfflinePartnerDetailPresentation.IsNavigateTipValid(string tip) => !string.IsNullOrEmpty(tip);
        void IOfflinePartnerDetailPresentation.BroadcastNavigateTip(string tip) => Notify(tip);
        string IOfflinePartnerDetailPresentation.GetText(string key) => key;
        void IOfflinePartnerDetailPresentation.SystemMessage(string text) => Notify(text);

        void IOfflinePartnerGiftPresentation.ConfigureGiftIcon(ItemIconNpcGift icon)
        {
            host.PartnerGiftBindings?.BindGiftIcon(icon);
        }
        void IOfflinePartnerGiftPresentation.ShowError(int error, object args, bool flag)
            => Notify("伙伴赠礼操作不可用：" + error);
        string IOfflinePartnerGiftPresentation.GetText(string key)
            => WorldMultiLanguageText.Localize == null ? key : WorldMultiLanguageText.Localize(key);
        void IOfflinePartnerGiftPresentation.SystemMessage(string text) => Notify(text);
        void IOfflinePartnerGiftPresentation.PopupInput(Action<IPartnerGiftInputDialog> initialize)
            => initialize?.Invoke(new OfflinePartnerInputDialog(this));
        void IOfflinePartnerGiftPresentation.PopupConfirm(Action<IPartnerGiftConfirmDialog> initialize)
        {
            var dialog = new OfflinePartnerConfirmDialog();
            initialize?.Invoke(dialog);
            dialog.Build(this, dialog.TextKey);
        }
        void IOfflinePartnerGiftPresentation.PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
            => OpenPartnerFriendshipDialog(initialize);
        object IOfflinePartnerGiftPresentation.Compact(long count) => host.HomeSettings?.FormatNumber(count) ?? count.ToString();
        void IOfflinePartnerGiftPresentation.Close(GameObject window) { if (window != null) UnityEngine.Object.Destroy(window); }
        void IOfflinePartnerGiftPresentation.ShowProgressionResult(ProgressionResult result)
            => Notify(result?.Message ?? "伙伴赠礼未完成");

        private void OpenPartnerFriendshipDialog(Action<IPartnerGiftFriendshipDialog> initialize)
        {
            CloseDialog();
            try
            {
                var root = UnityEngine.Object.Instantiate(host.PartnerFriendshipAssets.DialogPrefab,
                    canvas.transform, false);
                root.name = "OfficialPartnerFriendshipDialog";
                var dialog = root.GetComponent<NpcFriendshipDialog>();
                if (dialog == null) throw new InvalidOperationException("Official partner friendship dialog component is missing.");
                dialog.Configure(host.PartnerFriendshipDialogServices);
                previousFriendshipLocalize = WorldMultiLanguageText.Localize;
                previousFriendshipLanguageFont = WorldMultiLanguageText.LanguageFont;
                WorldMultiLanguageText.LanguageFont = () => host.Catalog.Font;
                var localization = new OfflinePartnerLocalizationService(host.PartnerDetailAssets.Texts,
                    host.PartnerDetailAssets.Common.GiftTexts, host.PartnerFriendshipAssets.PropertyTexts,
                    host.PartnerFriendshipAssets.DialogTexts);
                WorldMultiLanguageText.Localize = key => localization.GetText(key, LanguageType.zh_CN);
                previousFriendshipPool = ProfessionGameObjectPoolBridge.Provider;
                ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) =>
                    UnityEngine.Object.Instantiate(prefab, parent, keepWorld);
                foreach (WorldItemProgress progress in root.GetComponentsInChildren<WorldItemProgress>(true))
                    progress.Compact = count => host.HomeSettings?.FormatNumber(count) ?? count.ToString();
                partnerFriendshipPanel = root;
                initialize?.Invoke(new OfflinePartnerFriendshipDialogAdapter(dialog,
                    host.PartnerFriendshipDialogServices));
                root.GetComponent<WorldPopupWindow>()?.Show();
            }
            catch (Exception error)
            {
                if (partnerFriendshipPanel != null) UnityEngine.Object.Destroy(partnerFriendshipPanel);
                partnerFriendshipPanel = null;
                ProfessionGameObjectPoolBridge.Provider = previousFriendshipPool;
                previousFriendshipPool = null;
                WorldMultiLanguageText.Localize = previousFriendshipLocalize;
                previousFriendshipLocalize = null;
                WorldMultiLanguageText.LanguageFont = previousFriendshipLanguageFont;
                previousFriendshipLanguageFont = null;
                Notify("伙伴好感度详情打开失败：" + error.Message);
            }
        }

        private sealed class OfflinePartnerConfirmDialog : IPartnerGiftConfirmDialog
        {
            private GameObject root;
            private Action confirm;
            public string TextKey { get; private set; }

            public GameObject GameObject => root;

            public void Init(string textKey, Action onConfirm)
            {
                TextKey = textKey;
                confirm = onConfirm;
            }

            public void Build(HomeHudView owner, string textKey)
            {
                root = owner.CreatePartnerPromptRoot("PartnerGiftConfirm");
                Transform panel = owner.CreatePartnerPromptPanel(root.transform);
                owner.CreatePartnerPromptText(panel, owner.LocalizePartnerText(textKey), 26f,
                    new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.84f));
                owner.CreatePartnerPromptButton(panel, "取消", new Vector2(0.08f, 0.1f), new Vector2(0.44f, 0.30f),
                    () => owner.ClosePartnerPrompt(this));
                owner.CreatePartnerPromptButton(panel, "确认", new Vector2(0.56f, 0.1f), new Vector2(0.92f, 0.30f),
                    () => { owner.ClosePartnerPrompt(this); confirm?.Invoke(); });
            }
        }

        private sealed class OfflinePartnerFriendshipDialogAdapter : IPartnerGiftFriendshipDialog
        {
            private readonly NpcFriendshipDialog dialog;
            private readonly OfflinePartnerFriendshipDialogServices services;
            public OfflinePartnerFriendshipDialogAdapter(NpcFriendshipDialog dialog,
                OfflinePartnerFriendshipDialogServices services)
            { this.dialog = dialog; this.services = services; }
            public void Init(PartnerGiftNpcInfo npc)
            {
                if (npc == null) return;
                dialog.Init(services.GetNpc(npc.Id));
            }
        }
        private sealed class OfflinePartnerInputDialog : IPartnerGiftInputDialog
        {
            private readonly HomeHudView owner;
            private GameObject root;
            private TMP_InputField input;
            private Action<string> submit;
            public OfflinePartnerInputDialog(HomeHudView owner) { this.owner = owner; }
            public GameObject GameObject => root;
            public void Init(string titleKey, string placeholderKey, Action<string> submit)
            {
                this.submit = submit;
                root = owner.CreatePartnerPromptRoot("PartnerGiftInput");
                Transform panel = owner.CreatePartnerPromptPanel(root.transform);
                owner.CreatePartnerPromptText(panel, owner.LocalizePartnerText(titleKey), 27f,
                    new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.90f));
                input = owner.CreatePartnerPromptInput(panel, owner.LocalizePartnerText(placeholderKey),
                    new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.64f));
                owner.CreatePartnerPromptButton(panel, "取消", new Vector2(0.08f, 0.10f), new Vector2(0.44f, 0.30f),
                    () => owner.ClosePartnerPrompt(this));
                owner.CreatePartnerPromptButton(panel, "确认", new Vector2(0.56f, 0.10f), new Vector2(0.92f, 0.30f),
                    () =>
                    {
                        string value = input == null ? string.Empty : input.text;
                        submit?.Invoke(value);
                    });
                input.Select();
                input.ActivateInputField();
            }
            public void SetNpcFriendshipTagModerationClickMask() { }
        }

        private string LocalizePartnerText(string key)
            => WorldMultiLanguageText.Localize == null ? key : WorldMultiLanguageText.Localize(key);

        private GameObject CreatePartnerPromptRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.66f);
            return root;
        }

        private Transform CreatePartnerPromptPanel(Transform parent)
        {
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.08f, 0.34f);
            rect.anchorMax = new Vector2(0.92f, 0.66f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color32(53, 47, 44, 248);
            return panel.transform;
        }

        private TMP_Text CreatePartnerPromptText(Transform parent, string value, float size,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = catalog.Font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.text = value ?? string.Empty;
            text.raycastTarget = false;
            return text;
        }

        private TMP_InputField CreatePartnerPromptInput(Transform parent, string placeholder,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = new Color32(238, 232, 218, 255);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0.03f, 0.08f);
            viewportRect.anchorMax = new Vector2(0.97f, 0.92f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            var text = CreatePartnerPromptText(viewport.transform, string.Empty, 24f, Vector2.zero, Vector2.one);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var hint = CreatePartnerPromptText(viewport.transform, placeholder, 22f, Vector2.zero, Vector2.one);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            hint.color = new Color32(130, 125, 116, 255);
            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = viewportRect;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = image;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.characterLimit = 32;
            return field;
        }

        private Button CreatePartnerPromptButton(Transform parent, string label, Vector2 anchorMin,
            Vector2 anchorMax, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = new Color32(174, 123, 72, 255);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            CreatePartnerPromptText(go.transform, label, 22f, Vector2.zero, Vector2.one);
            return button;
        }

        private void ClosePartnerPrompt(object dialog)
        {
            GameObject root = dialog is OfflinePartnerInputDialog inputDialog ? inputDialog.GameObject
                : ((OfflinePartnerConfirmDialog)dialog).GameObject;
            if (root != null) UnityEngine.Object.Destroy(root);
        }
    }
}
