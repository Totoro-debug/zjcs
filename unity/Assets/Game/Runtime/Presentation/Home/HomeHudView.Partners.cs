using System;
using System.Collections;
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
            var conversations = host.PartnerConversationOpenRefresher?.RefreshOnOpen();
            if (conversations != null && !conversations.IsApplied
                && conversations.Status != ProgressionResultStatus.Unchanged)
                Notify(conversations.Message ?? "伙伴通话刷新失败");
            host.PartnerTipDots?.Refresh();
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

        void IOfflinePartnerPresentation.BindNpcTipDot(TipDotNodeLinker linker, int npcClassId)
            => host.PartnerTipDots?.Bind(linker, npcClassId);
        private AudioSource partnerUiAudioSource;
        void IOfflinePartnerPresentation.PostSound(AudioClip sound, GameObject owner)
        {
            if (sound == null || canvas == null) return;
            if (partnerUiAudioSource == null)
            {
                partnerUiAudioSource = canvas.gameObject.AddComponent<AudioSource>();
                partnerUiAudioSource.playOnAwake = false;
                partnerUiAudioSource.loop = false;
                partnerUiAudioSource.spatialBlend = 0f;
                HomeAudioSettings.Register(partnerUiAudioSource, HomeAudioChannel.Sound);
            }
            partnerUiAudioSource.PlayOneShot(sound);
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
            => OpenPartnerInputDialog(initialize);
        void IOfflinePartnerGiftPresentation.PopupConfirm(Action<IPartnerGiftConfirmDialog> initialize)
            => OpenPartnerConfirmDialog(initialize);
        void IOfflinePartnerGiftPresentation.PopupFriendship(IWindowOperator owner, bool starGod, Action<IPartnerGiftFriendshipDialog> initialize)
            => OpenPartnerFriendshipDialog(initialize);
        object IOfflinePartnerGiftPresentation.Compact(long count) => host.HomeSettings?.FormatNumber(count) ?? count.ToString();
        void IOfflinePartnerGiftPresentation.Close(GameObject window)
        {
            if (window == partnerPromptWindow && window != null) ClosePartnerPromptWindow();
            else if (window != null) UnityEngine.Object.Destroy(window);
        }
        void IOfflinePartnerGiftPresentation.ShowProgressionResult(ProgressionResult result)
            => Notify(result?.Message ?? "伙伴赠礼未完成");

        private void OpenPartnerFriendshipDialog(Action<IPartnerGiftFriendshipDialog> initialize)
        {
            DestroyPartnerFriendshipWindow();
            var staging = new GameObject("PartnerFriendshipStaging", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);
            try
            {
                previousFriendshipLocalize = WorldMultiLanguageText.Localize;
                previousFriendshipLanguageFont = WorldMultiLanguageText.LanguageFont;
                previousFriendshipPool = ProfessionGameObjectPoolBridge.Provider;
                partnerFriendshipServicesInstalled = true;
                WorldMultiLanguageText.LanguageFont = () => host.Catalog.Font;
                var localization = new OfflinePartnerLocalizationService(host.PartnerDetailAssets.Texts,
                    host.PartnerDetailAssets.Common.GiftTexts, host.PartnerFriendshipAssets.PropertyTexts,
                    host.PartnerFriendshipAssets.DialogTexts);
                WorldMultiLanguageText.Localize = key => localization.GetText(key, LanguageType.zh_CN);
                ProfessionGameObjectPoolBridge.Provider = (scope, prefab, parent, keepWorld) =>
                    UnityEngine.Object.Instantiate(prefab, parent, keepWorld);
                var assets = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
                var root = UnityEngine.Object.Instantiate(assets.WindowPrefab,
                    staging.transform, false);
                partnerFriendshipPanel = root;
                root.name = "OfficialPartnerFriendshipDialog";
                var dialog = root.GetComponent<NpcFriendshipDialog>();
                if (dialog == null) throw new InvalidOperationException("Official partner friendship dialog component is missing.");
                dialog.Configure(host.PartnerFriendshipDialogServices);
                foreach (WorldButtonSound sound in root.GetComponentsInChildren<WorldButtonSound>(true))
                    sound.Post = PostPartnerPromptSound;
                foreach (WorldPopupWindowSound sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))
                    sound.Post = PostPartnerPromptSound;
                foreach (WorldItemProgress progress in root.GetComponentsInChildren<WorldItemProgress>(true))
                    progress.Compact = count => host.HomeSettings?.FormatNumber(count) ?? count.ToString();
                initialize?.Invoke(new OfflinePartnerFriendshipDialogAdapter(dialog,
                    host.PartnerFriendshipDialogServices));
                var popup = root.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => ClosePartnerFriendshipWindow());
                root.SetActive(false);
                root.transform.SetParent(canvas.transform, false);
                int order = canvas.sortingOrder;
                foreach (Canvas current in canvas.GetComponentsInChildren<Canvas>())
                    if (current.overrideSorting && !current.transform.IsChildOf(root.transform))
                        order = Math.Max(order, current.sortingOrder);
                partnerFriendshipMask = UnityEngine.Object.Instantiate(assets.BlurMaskPrefab, staging.transform, false);
                partnerFriendshipMask.name = "OfficialPartnerFriendshipMask";
                partnerFriendshipMask.SetActive(false);
                partnerFriendshipMask.transform.SetParent(canvas.transform, false);
                partnerFriendshipCapture = host.gameObject.AddComponent<PartnerWindowBlurCapture>();
                partnerFriendshipCapture.Initialize(assets.BlurMaterial);
                var mask = partnerFriendshipMask.GetComponent<WorldWindowBlurMask>();
                mask.BlurManager = partnerFriendshipCapture;
                partnerFriendshipPreparation = host.StartCoroutine(PreparePartnerFriendshipWindow(root, popup, mask, order + 10));
            }
            catch (Exception error)
            {
                DestroyPartnerFriendshipWindow();
                Debug.LogException(error);
                Notify("伙伴好感度详情打开失败：" + error.Message);
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private GameObject partnerFriendshipMask;
        private bool partnerFriendshipServicesInstalled;
        private PartnerWindowBlurCapture partnerFriendshipCapture;
        private Coroutine partnerFriendshipPreparation;

        private IEnumerator PreparePartnerFriendshipWindow(GameObject root, WorldPopupWindow popup, WorldWindowBlurMask mask, int order)
        {
            yield return mask.Prepare(popup, false);
            if (root == null || partnerFriendshipPanel != root) yield break;
            root.SetActive(true);
            popup.SetCanvasSortingOrder(order);
            popup.Show();
            mask.AttachToWindow(popup);
            partnerFriendshipPreparation = null;
        }

        private bool ClosePartnerFriendshipWindow()
        {
            if (partnerFriendshipPanel == null) return false;
            var root = partnerFriendshipPanel;
            if (!root.activeInHierarchy) { DestroyPartnerFriendshipWindow(); return true; }
            root.GetComponent<WorldPopupWindow>().Hide(() =>
            {
                if (partnerFriendshipPanel == root) DestroyPartnerFriendshipWindow();
            });
            return true;
        }

        private void DestroyPartnerFriendshipWindow()
        {
            if (partnerFriendshipPreparation != null) host.StopCoroutine(partnerFriendshipPreparation);
            partnerFriendshipPreparation = null;
            if (partnerFriendshipMask != null)
            {
                partnerFriendshipMask.GetComponent<WorldWindowBlurMask>().UnattachToWindow();
                UnityEngine.Object.Destroy(partnerFriendshipMask);
            }
            partnerFriendshipMask = null;
            if (partnerFriendshipCapture != null) UnityEngine.Object.Destroy(partnerFriendshipCapture);
            partnerFriendshipCapture = null;
            if (partnerFriendshipPanel != null)
            {
                // Recycle the original dialog's rows and icons while its services
                // are still installed, before restoring the underlying page.
                partnerFriendshipPanel.SetActive(false);
                UnityEngine.Object.Destroy(partnerFriendshipPanel);
                partnerFriendshipPanel = null;
            }
            if (partnerFriendshipServicesInstalled)
            {
                partnerFriendshipServicesInstalled = false;
                ProfessionGameObjectPoolBridge.Provider = previousFriendshipPool;
                previousFriendshipPool = null;
                WorldMultiLanguageText.Localize = previousFriendshipLocalize;
                previousFriendshipLocalize = null;
                WorldMultiLanguageText.LanguageFont = previousFriendshipLanguageFont;
                previousFriendshipLanguageFont = null;
            }
        }

        private sealed class OfflinePartnerConfirmDialog : IPartnerGiftConfirmDialog
        {
            private readonly WorldConfirmDialog dialog;
            public OfflinePartnerConfirmDialog(WorldConfirmDialog value) { dialog = value; }
            public void Init(string textKey, Action onConfirm) => dialog.Init(textKey, onConfirm);
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
        private GameObject partnerPromptWindow;
        private GameObject partnerPromptMask;

        private void OpenPartnerConfirmDialog(Action<IPartnerGiftConfirmDialog> initialize)
        {
            DestroyPartnerPromptWindow();
            var assets = OfficialPartnerConfirmAssetCatalog.LoadDefault();
            var staging = new GameObject("PartnerConfirmStaging", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);
            try
            {
                var root = UnityEngine.Object.Instantiate(assets.Prefab, staging.transform, false);
                root.name = "OfficialPartnerGiftConfirmDialog";
                var dialog = root.GetComponent<WorldConfirmDialog>();
                if (dialog == null) throw new InvalidOperationException("Official confirm dialog component is missing.");
                var localization = new OfflinePartnerLocalizationService(assets.Texts);
                foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = key => localization.GetText(key, LanguageType.zh_CN);
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = catalog.Font;
                foreach (WorldButtonSound sound in root.GetComponentsInChildren<WorldButtonSound>(true))
                    sound.Post = PostPartnerPromptSound;
                foreach (WorldPopupWindowSound sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))
                    sound.Post = PostPartnerPromptSound;
                dialog.CloseWindow = () => ClosePartnerPromptWindow();
                var popup = root.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => ClosePartnerPromptWindow());
                partnerPromptWindow = root;
                initialize?.Invoke(new OfflinePartnerConfirmDialog(dialog));
                root.transform.SetParent(canvas.transform, false);
                root.SetActive(true);
                int order = canvas.sortingOrder;
                foreach (Canvas current in canvas.GetComponentsInChildren<Canvas>())
                    if (current.overrideSorting && !current.transform.IsChildOf(root.transform))
                        order = Math.Max(order, current.sortingOrder);
                popup.SetCanvasSortingOrder(order + 10);
                partnerPromptMask = UnityEngine.Object.Instantiate(OfficialPartnerPromptAssetCatalog.LoadDefault().MaskPrefab,
                    canvas.transform, false);
                partnerPromptMask.name = "OfficialPartnerInputMask";
                partnerPromptMask.GetComponent<WorldWindowMask>().AttachToWindow(popup);
                popup.Show();
            }
            catch
            {
                DestroyPartnerPromptWindow();
                throw;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private void OpenPartnerInputDialog(Action<IPartnerGiftInputDialog> initialize)
        {
            DestroyPartnerPromptWindow();
            var assets = OfficialPartnerPromptAssetCatalog.LoadDefault();
            var staging = new GameObject("PartnerInputStaging", typeof(RectTransform));
            staging.transform.SetParent(canvas.transform, false);
            staging.SetActive(false);
            try
            {
                var root = UnityEngine.Object.Instantiate(assets.InputPrefab, staging.transform, false);
                root.name = "OfficialPartnerInputDialog";
                var dialog = root.GetComponent<WorldInputDialog>();
                if (dialog == null) throw new InvalidOperationException("Official input dialog component is missing.");
                var localization = new OfflinePartnerLocalizationService(assets.Texts);
                foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = key => localization.GetText(key, LanguageType.zh_CN);
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = catalog.Font;
                foreach (WorldContentModerationClickMask mask in root.GetComponentsInChildren<WorldContentModerationClickMask>(true))
                    mask.Configure(new OfflinePartnerContentModeration(this));
                foreach (WorldButtonSound sound in root.GetComponentsInChildren<WorldButtonSound>(true))
                    sound.Post = PostPartnerPromptSound;
                foreach (WorldPopupWindowSound sound in root.GetComponentsInChildren<WorldPopupWindowSound>(true))
                    sound.Post = PostPartnerPromptSound;
                dialog.CloseWindow = _ => ClosePartnerPromptWindow();
                var popup = root.GetComponent<WorldPopupWindow>();
                popup.SetMaskClickAction(() => ClosePartnerPromptWindow());
                partnerPromptWindow = root;
                initialize?.Invoke(new OfflinePartnerInputDialog(dialog));
                root.transform.SetParent(canvas.transform, false);
                root.SetActive(true);
                // Original WindowContainer.OnWindowShowing adds ten, then
                // WindowMaskBase attaches the native mask at window order - 1.
                int order = canvas.sortingOrder;
                foreach (Canvas current in canvas.GetComponentsInChildren<Canvas>())
                    if (current.overrideSorting && !current.transform.IsChildOf(root.transform))
                        order = Math.Max(order, current.sortingOrder);
                popup.SetCanvasSortingOrder(order + 10);
                partnerPromptMask = UnityEngine.Object.Instantiate(assets.MaskPrefab, canvas.transform, false);
                partnerPromptMask.name = "OfficialPartnerInputMask";
                partnerPromptMask.GetComponent<WorldWindowMask>().AttachToWindow(popup);
                popup.Show();
            }
            catch
            {
                DestroyPartnerPromptWindow();
                throw;
            }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private void PostPartnerPromptSound(AudioClip clip, GameObject owner)
            => ((IOfflinePartnerPresentation)this).PostSound(clip, owner);

        private bool ClosePartnerPromptWindow()
        {
            if (partnerPromptWindow == null) return false;
            var root = partnerPromptWindow;
            root.GetComponent<WorldPopupWindow>().Hide(() =>
            {
                if (partnerPromptWindow == root) partnerPromptWindow = null;
                if (partnerPromptMask != null) UnityEngine.Object.Destroy(partnerPromptMask);
                partnerPromptMask = null;
                if (root != null) UnityEngine.Object.Destroy(root);
            });
            return true;
        }

        private void DestroyPartnerPromptWindow()
        {
            if (partnerPromptWindow != null) UnityEngine.Object.Destroy(partnerPromptWindow);
            partnerPromptWindow = null;
            if (partnerPromptMask != null) UnityEngine.Object.Destroy(partnerPromptMask);
            partnerPromptMask = null;
        }

        private sealed class OfflinePartnerContentModeration : IWorldContentModerationServices
        {
            private readonly HomeHudView owner;
            public OfflinePartnerContentModeration(HomeHudView value) { owner = value; }
#pragma warning disable 0067
            public event Action<HashSet<long>> DisabledUpdated;
#pragma warning restore 0067
            // Confirmed offline custom-title policy has no server disable switch.
            public bool CheckDisabled(int customType) => false;
            public void ShowError(int code) => ((IOfflinePartnerGiftPresentation)owner).ShowError(code, null, false);
        }

        private sealed class OfflinePartnerInputDialog : IPartnerGiftInputDialog
        {
            private readonly WorldInputDialog dialog;
            public OfflinePartnerInputDialog(WorldInputDialog value) { dialog = value; }
            public GameObject GameObject => dialog.gameObject;
            public void Init(string titleKey, string placeholderKey, Action<string> submit)
                => dialog.Init(titleKey, placeholderKey, submit);
            // Original Common.ContentCustomType.NpcFriendShipTag = 11.
            public void SetNpcFriendshipTagModerationClickMask() => dialog.SetContentModerationClickMask(11);
        }

    }
}
