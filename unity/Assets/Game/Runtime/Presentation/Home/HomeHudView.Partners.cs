using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private sealed class PartnerDetailLowMemorySubscription : IDisposable
        {
            private Application.LowMemoryCallback callback;
            internal PartnerDetailLowMemorySubscription(Action handler)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                callback = () => handler();
                Application.lowMemory += this.callback;
            }
            public void Dispose()
            {
                if (callback == null) return;
                Application.lowMemory -= callback;
                callback = null;
            }
        }

        private void OpenPartnerRoster()
        {
            CloseDialog();
            host.PartnerTipDots?.Refresh();
            var root = new GameObject("PartnerRosterDialog", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
            root.GetComponent<Button>().transition = Selectable.Transition.None;
            root.GetComponent<Button>().onClick.AddListener(HandlePartnerBackgroundClick);
            activeDialog = root;
            activeDialogRenderer = null;
            activePanelKey = "partner_roster";
            try
            {
                var roster = host.PartnerRosterAssets;
                var starGodAssets = OfficialPartnerStarGodAssetCatalog.LoadDefault();
                var panelAssets = OfficialPartnerPanelAssetCatalog.Load();
                partnerUiImages = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
                partnerUiImages.Entries = roster.Images.Entries.Concat(starGodAssets.CreateImageCatalog().Entries)
                    .Concat(host.PartnerGiftAssets.Images.Entries)
                    .GroupBy(value => value.Path).Select(group => group.First()).ToArray();
                var panel = root.AddComponent<PartnerPanelView>();
                partnerPanel = panel;
                var dialoguePresentation = new HomePartnerDialoguePresentation(this);
                partnerDialoguePresentation = dialoguePresentation;
                var partnerData = OfficialPartnerAssetCatalog.LoadDefault();
                var conversationUi = OfflinePartnerConversationUiSessionFactory.Create(host.Profile,
                    host.DailyDungeonProgression.Catalog, partnerData, () => DateTime.UtcNow,
                    () => host.Rules.IsFunctionLimited(57), 8,
                    dialoguePresentation, dialoguePresentation, () => host.PartnerTipDots?.Refresh());
                Func<string, string> panelFallback = WorldMultiLanguageText.Localize;
                partnerActorUi = new OfflinePartnerActorUiRuntime(host.Profile, host.DailyDungeonProgression,
                    partnerData, new OfflineProfessionImageResourceLoader(partnerUiImages),
                    key => panelFallback == null ? key : panelFallback(key),
                    npcId => host.OpenPartnerDetail(npcId), () => {
                        CloseDialog();
                        OpenCharacterPage();
                    });
                dialoguePresentation.Bind(conversationUi);
                var context = new PartnerPanelContext {
                    Roster = host.PartnerRoster,
                    StarGod = host.PartnerStarGod,
                    Windows = new OfflinePartnerWindowService(_ => CloseDialog(), () => CloseDialog()),
                    Images = new OfflineProfessionImageResourceLoader(partnerUiImages),
                    Font = catalog.Font,
                    Localize = key => {
                        foreach (var text in starGodAssets.Texts) if (text.key == key) return text.text;
                        string value = roster.CreateLocalizer()(key);
                        if (value != key) return value;
                        return panelFallback == null ? key : panelFallback(key);
                    },
                    IsFunctionLimit = function => host.Rules.IsFunctionLimited(function),
                    BindNativeTree = tree => {
                        partnerActorUi.BindTree(tree);
                        host.PartnerTipDots?.BindNativeTree(tree);
                    },
                    BindPartnerTipDotDialogues = services => host.PartnerTipDots?.BindDialogues(services),
                    PostSound = ((IOfflinePartnerPresentation)this).PostSound
                };
                conversationUi.Bind(context);
                panel.Open(panelAssets, context);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                CloseDialog();
                Notify("伙伴界面打开失败：" + error.Message);
            }
        }

        private sealed class HomePartnerDialoguePresentation : IOfflinePartnerDialoguePresentation,
            IOfflinePartnerDialoguePlaybackPresentation
        {
            private readonly HomeHudView owner;
            private OfflinePartnerConversationUiSession conversations;
            internal HomePartnerDialoguePresentation(HomeHudView value) { owner = value; }
            internal void Bind(OfflinePartnerConversationUiSession value)
                => conversations = value ?? throw new ArgumentNullException(nameof(value));
            public GameObject Instantiate(GameObject prefab, Transform parent, bool resetTransformOnly)
                => UnityEngine.Object.Instantiate(prefab, parent, resetTransformOnly);
            public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc)
                => owner.partnerActorUi.InitializeNpcIcon(icon, npc);
            public void ShowError(int code) => owner.Notify("伙伴通话错误：" + code);
            public void ShowDialogue(PartnerDialogueData dialogue) => owner.OpenPartnerDialogue(dialogue, conversations);
            public string GetNpcNameKey(int npcClassId)
            {
                var row = OfficialPartnerAssetCatalog.LoadDefault().Rows("npc")
                    .FirstOrDefault(value => Convert.ToInt32(value["Id"]) == npcClassId);
                return row == null ? string.Empty : Convert.ToString(row["NameKey"]);
            }
            public void InitializeActorIcon(Component icon, long actorId, PartnerDialogueSpokesMan speaker)
                => owner.partnerActorUi.InitializeActorIcon(icon, actorId, speaker, conversations.Source.Npcs);
            public ItemIcon GetRewardIcon(int itemClassId, Transform parent)
            {
                var prototype = owner.host.PartnerGiftAssets.GiftPrefab
                    ? owner.host.PartnerGiftAssets.GiftPrefab.GetComponentInChildren<ItemIcon>(true)
                    : null;
                if (!prototype) throw new InvalidOperationException("Original partner gift ItemIcon prototype is missing.");
                return owner.host.PartnerGiftBindings.CreateGiftIcon(itemClassId, parent, prototype.gameObject);
            }
            public void RecycleRewardIcon(ItemIcon icon)
            {
                if (!icon) return;
                owner.host.PartnerGiftBindings.RecycleIcon(icon);
                UnityEngine.Object.Destroy(icon.gameObject);
            }
            public void ShowAcknowledgeFailure(ProgressionResult result, bool invokeByPlayer)
                => owner.Notify(result?.Message ?? "伙伴通话保存失败");
        }

        private void OpenPartnerDialogue(PartnerDialogueData dialogue, OfflinePartnerConversationUiSession conversations)
        {
            if (dialogue == null || conversations == null || partnerActorUi == null || partnerUiImages == null)
                throw new InvalidOperationException("Saved partner dialogue or its local actor bindings are unavailable.");
            if (partnerDialoguePlayback != null) ClosePartnerDialogueImmediate();
            var root = new GameObject("PartnerDialoguePlayback", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(activeDialog.transform, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
            root.GetComponent<Button>().transition = Selectable.Transition.None;
            root.GetComponent<Button>().onClick.AddListener(() => RequestClosePartnerDialogue(null));
            var playback = root.AddComponent<PartnerDialoguePlaybackView>();
            partnerDialogueRoot = root;
            partnerDialoguePlayback = playback;
            var assets = OfficialPartnerDialoguePlaybackAssetCatalog.LoadDefault();
            Func<string, string> panelFallback = WorldMultiLanguageText.Localize;
            var context = new PartnerDialoguePlaybackContext {
                Dialogues = conversations.Playback,
                Awards = new HomePartnerDialogueAwards(),
                HyperLinks = new HomePartnerDialogueHyperLinks(),
                Windows = new OfflinePartnerWindowService(RequestClosePartnerDialogue,
                    () => RequestClosePartnerDialogue(null)),
                Images = new OfflineProfessionImageResourceLoader(partnerUiImages),
                Font = catalog.Font,
                Localize = key => panelFallback == null ? key : panelFallback(key),
                BindNativeTree = tree => {
                    partnerActorUi.BindTree(tree);
                    host.PartnerTipDots?.BindNativeTree(tree);
                },
                PostSound = ((IOfflinePartnerPresentation)this).PostSound
            };
            try
            {
                playback.Open(assets, context, dialogue);
                playback.Window.SetMaskClickAction(() => RequestClosePartnerDialogue(playback.Window.gameObject));
            }
            catch (Exception error)
            {
                ClosePartnerDialogueImmediate();
                Notify("伙伴通话打开失败：" + error.Message);
            }
        }

        private void HandlePartnerBackgroundClick()
        {
            if (partnerDialoguePlayback != null) RequestClosePartnerDialogue(null);
            else CloseDialog();
        }

        private void RequestClosePartnerDialogue(GameObject window)
        {
            var playback = partnerDialoguePlayback;
            if (!playback) return;
            var popup = playback.Window;
            if (window && popup && window != popup.gameObject) return;
            if (!popup || popup.State == WorldPopupWindow.EState.Hide)
            { ClosePartnerDialogueImmediate(); return; }
            popup.Hide(ClosePartnerDialogueImmediate);
        }

        private void ClosePartnerDialogueImmediate()
        {
            var root = partnerDialogueRoot;
            partnerDialogueRoot = null;
            if (partnerDialoguePlayback)
            {
                partnerDialoguePlayback.Close();
                partnerDialoguePlayback = null;
            }
            if (root) UnityEngine.Object.Destroy(root);
        }

        private sealed class HomePartnerDialogueAwards : IPartnerDialogueAwardServices
        {
            private readonly HashSet<Func<object, bool>> handlers = new HashSet<Func<object, bool>>();
            public void RegisterHandler(Func<object, bool> handler) => handlers.Add(handler);
            public void RemoveHandler(Func<object, bool> handler) => handlers.Remove(handler);
            // Partner node rewards are already committed by Acknowledge in the local conversation transaction.
            public bool IsConversationAward(object message) => false;
            public void PopupAward(object message) { }
        }

        private sealed class HomePartnerDialogueHyperLinks : IPartnerStaticHyperLinkServices
        {
            public PartnerStaticHyperLinkInfo GetHyperLinkInfo(int id) => null;
            public IPartnerStaticHyperLinkHandler CreateHandler(int type, string data) => null;
            public IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 point)
            {
                var events = EventSystem.current;
                if (!events) return Array.Empty<RaycastResult>();
                var pointer = new PointerEventData(events) { position = point };
                var results = new List<RaycastResult>();
                events.RaycastAll(pointer, results);
                return results;
            }
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
            OpenPartnerWindow(npcId, NpcDetailPanel.TabType.Detail, services, gifts);
        }

        internal void OpenPartnerGift(int npcId, OfficialPartnerGiftPageAssetCatalog assets,
            OfflinePartnerGiftUiServices services, OfflinePartnerGiftPlayer gifts)
        {
            OpenPartnerWindow(npcId, NpcDetailPanel.TabType.Gift, host.PartnerDetailServices, gifts);
        }

        private void OpenPartnerWindow(int npcId, NpcDetailPanel.TabType initialTab,
            OfflinePartnerDetailPropertyServices properties, OfflinePartnerGiftPlayer gifts)
        {
            CloseDialog();
            try
            {
                if (gifts == null || gifts.GetNpcByClassId(npcId) == null)
                    throw new InvalidOperationException("伙伴尚未结识");
                var official = host.PartnerCatalog ?? throw new InvalidOperationException("Official partner rules are unavailable.");
                var giftConfig = OfficialPartnerGiftAssetCatalog.LoadDefault();
                var detailAssets = host.PartnerDetailAssets;
                var giftAssets = host.PartnerGiftAssets;
                var profileAssets = OfficialPartnerProfileAssetCatalog.Load();
                var achievementAssets = OfficialPartnerAchievementAssetCatalog.Load();
                var windowAssets = OfficialPartnerDetailWindowAssetCatalog.Load();
                var actorAssets = Resources.Load<PartnerNativeDetailActorAssets>("Partners/PartnerNativeDetailActorAssets");
                var presentationAssets = Resources.Load<PartnerNativeDetailPresentationAssets>("Partners/PartnerNativeDetailPresentationAssets");
                if (!actorAssets || !presentationAssets) throw new InvalidOperationException("Official partner actors are unavailable.");

                var language = new OfflinePartnerLocalizationService(detailAssets.Texts,
                    detailAssets.Common.GiftTexts, host.PartnerFriendshipAssets.PropertyTexts,
                    host.PartnerFriendshipAssets.DialogTexts, giftAssets.GiftTexts, giftAssets.RelationshipTexts);
                var text = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in profileAssets.Texts) text[entry.Key] = entry.Value;
                foreach (var entry in achievementAssets.Texts) text[entry.Key] = entry.Value;
                foreach (var entry in windowAssets.Texts) text[entry.key] = entry.text;
                string Localize(string key) => text.TryGetValue(key, out var value)
                    ? value : language.GetText(key, LanguageType.zh_CN);

                partnerDetailWindowImages = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
                var starGodImages = OfficialPartnerStarGodAssetCatalog.LoadDefault().CreateImageCatalog();
                partnerDetailWindowImages.Entries = new[] { giftAssets.Images, detailAssets.Common.Images,
                        profileAssets.Images, achievementAssets.Images, host.PartnerRosterAssets.Images, starGodImages }
                    .SelectMany(value => value.Entries).GroupBy(value => value.Path, StringComparer.Ordinal)
                    .Select(group => group.First()).ToArray();
                var imageLoader = new OfflineProfessionImageResourceLoader(partnerDetailWindowImages);

                partnerDetailAppearanceManagerConfig = ScriptableObject.CreateInstance<AppearanceAssetManagerConfig>();
                partnerDetailAppearanceManager = new AppearanceAssetManager(partnerDetailAppearanceManagerConfig,
                    callback => new PartnerDetailLowMemorySubscription(callback));
                partnerDetailTextures = new OfflinePartnerUiTextureResources(actorAssets, partnerDetailAppearanceManager);
                previousPartnerAppearanceTextures = ProfessionAppearanceTextureBindings.Current;
                ProfessionAppearanceTextureBindings.Current = partnerDetailTextures;

                var detailConfiguration = new OfflinePartnerDetailConfiguration(official, giftConfig, presentationAssets.Regions.text);
                var live = new OfflinePartnerDetailLiveServices(actorAssets, detailConfiguration.GetNpcInfo,
                    id => detailConfiguration.TryGetFullBodyImagePath(id, PartnerFullBodyImageType.Live, out var path) ? path : null);
                partnerAdventureTips = new OfflinePartnerAdventureTipServices(presentationAssets,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    tip => { if (tip) UnityEngine.Object.Destroy(tip); if (partnerAdventureTipRoot == tip) partnerAdventureTipRoot = null; });
                var presentation = new OfflinePartnerDetailPresentationServices(detailConfiguration, actorAssets,
                    presentationAssets, partnerDetailTextures, live, partnerAdventureTips, ShowPartnerAdventureTip);

                var profileService = new OfflinePartnerProfileService(host.Profile, new PartnerProfileRules(official.Rows));
                var achievementService = new OfflinePartnerAchievementService(host.Profile,
                    new PartnerFriendshipAchievementRules(official.Rows));
                achievementService.RefreshFromSave();
                var rewardPresentation = new OfflinePartnerRewardUiPresentation(host.PartnerGiftBindings,
                    profileAssets, achievementAssets, Localize,
                    key => text.ContainsKey(key) || language.ContainsKey(key),
                    linker => host.PartnerTipDots?.BindNativeTree(linker.gameObject),
                    () => { gifts.Reload(); host.PartnerTipDots?.Refresh(); },
                    result => ((IOfflinePartnerDetailPresentation)this).ShowProgressionResult(result), Notify);
                var profileUi = new OfflinePartnerProfileUiServices(profileService, rewardPresentation.Profile);
                var achievementUi = new OfflinePartnerAchievementUiServices(achievementService, official, () => false,
                    rewardPresentation.Achievement);
                var npcServices = new OfflinePartnerDetailWindowServices(host.Profile, gifts, profileUi,
                    detailConfiguration, window =>
                    {
                        if (partnerDetailWindow && ReferenceEquals(window, partnerDetailWindow.Content)) CloseDialog();
                        else if (window) UnityEngine.Object.Destroy(window);
                    });

                var root = new GameObject(initialTab == NpcDetailPanel.TabType.Gift
                    ? "PartnerGiftDialog" : "PartnerDetailDialog", typeof(RectTransform), typeof(Image), typeof(Button));
                root.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
                root.GetComponent<Button>().transition = Selectable.Transition.None;
                root.GetComponent<Button>().onClick.AddListener(CloseDialog);
                activeDialog = root; activeDialogRenderer = null;
                activePanelKey = initialTab == NpcDetailPanel.TabType.Gift ? "partner_gift" : "partner_detail";
                partnerDetailWindow = root.AddComponent<PartnerDetailWindowView>();

                var context = new PartnerDetailWindowContext
                {
                    Npcs = npcServices,
                    Properties = properties,
                    Gifts = host.PartnerGiftServices,
                    Profiles = profileUi,
                    Achievements = achievementUi,
                    Treasures = this,
                    Presentation = presentation,
                    Windows = new OfflinePartnerWindowService(window =>
                    {
                        if (partnerDetailWindow && ReferenceEquals(window, partnerDetailWindow.Content)) CloseDialog();
                        else if (window) UnityEngine.Object.Destroy(window);
                    }, CloseDialog),
                    Images = imageLoader,
                    Font = Resources.Load<TMP_FontAsset>("Combat/FZY4K-Dynamic") ?? host.Catalog.Font,
                    Localize = Localize,
                    BindNativeTree = tree => host.PartnerTipDots?.BindNativeTree(tree),
                    PostSound = ((IOfflinePartnerPresentation)this).PostSound
                };
                partnerDetailWindow.Open(windowAssets, context,
                    new List<IPartnerDetailWindowNpcData> { npcServices.GetNpcByClassId(npcId) }, 0, initialTab);
            }
            catch (Exception error)
            {
                CloseDialog();
                Notify("伙伴详情打开失败：" + error.Message);
            }
        }

        private void ShowPartnerAdventureTip(Vector3 position, int mapGroup, int profession, int preference)
        {
            if (partnerAdventureTipRoot) UnityEngine.Object.Destroy(partnerAdventureTipRoot);
            var assets = Resources.Load<PartnerNativeDetailPresentationAssets>("Partners/PartnerNativeDetailPresentationAssets");
            if (!assets || !assets.AdventureTip || partnerAdventureTips == null || activeDialog == null)
                throw new InvalidOperationException("Original partner adventure tip is unavailable.");
            var root = UnityEngine.Object.Instantiate(assets.AdventureTip, activeDialog.transform, false);
            root.SetActive(false);
            partnerAdventureTips.BindTree(root);
            var tip = root.GetComponent<PopTipNpcAdventure>();
            if (!tip) throw new InvalidOperationException("Original partner adventure tip controller is missing.");
            tip.Configure(partnerAdventureTips);
            tip.Init(position, mapGroup, (Zjcs.Official.ProfessionUI.Profession)profession, (NpcGiftType)preference);
            partnerAdventureTipRoot = root;
            root.SetActive(true);
        }

        internal void UpdatePartnerDetailRuntime()
        {
            partnerDetailAppearanceManager?.Update();
            if (Input.GetMouseButtonDown(0)) partnerAdventureTips?.DispatchPointerDown(Input.mousePosition);
        }

        IProfessionItemInfo IPartnerTreasureIconServices.GetItemInfo(int itemId)
        {
            OfficialPartnerCatalog official = host.PartnerCatalog;
            if (official == null || itemId <= 0 || !official.Rows("item").Any(row =>
                    row.TryGetValue("ClassId", out var value) && Convert.ToInt32(value) == itemId))
                return null;
            return new OfflinePartnerItemInfo(official, itemId);
        }

        bool IPartnerTreasureIconServices.IsTreasureActive(int itemId)
            => host.PartnerDetailServices?.IsTreasureActive(itemId) == true;

        void IPartnerTreasureIconServices.SetUIGray(GameObject target, bool gray)
        {
            if (!target) return;
            Material imageGray = Resources.Load<Material>("World/Mainline/HandIn/UIGray");
            if (!imageGray) throw new InvalidOperationException("Official GlobalUIConfig GrayMat is not packaged.");
            foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TextMeshProUGUI)
                {
                    TMPEffect effect = graphic.GetComponent<TMPEffect>();
                    if (gray)
                    {
                        if (!effect) effect = graphic.gameObject.AddComponent<TMPEffect>();
                        effect.SetGray(true);
                    }
                    else if (effect) effect.SetGray(false);
                }
                else if (graphic is Text)
                    graphic.material = gray ? imageGray : null;
                else if (graphic is Image || graphic is RawImage)
                    graphic.material = gray ? imageGray : null;
            }
        }

        void IPartnerTreasureIconServices.PopupTreasureDetail(int itemId)
        {
            if (itemId <= 0) return;
            partnerTreasurePopup?.Close();
            OfficialMap11UiAssets assets = Resources.Load<OfficialMap11UiAssets>("Map11/UI/DynamicItems");
            GameObject prefab = Resources.Load<GameObject>("Map11/UI/TreasureDetailDialog");
            if (!assets || !prefab)
                throw new InvalidOperationException("Official treasure detail UI assets are unavailable.");
            int quality = assets.ItemQualityId(itemId);
            string[] qualities = { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" };
            if (quality < 0 || quality >= qualities.Length)
                throw new InvalidOperationException("Official treasure quality is unavailable: " + itemId);
            string itemName = WorldMultiLanguageText.Localize?.Invoke("item_" + itemId + "_name");
            string qualityName = WorldMultiLanguageText.Localize?.Invoke("Quality." + qualities[quality]);
            string itemTypeName = WorldMultiLanguageText.Localize?.Invoke("ItemType.Treasure");
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(qualityName) || string.IsNullOrEmpty(itemTypeName))
                throw new InvalidOperationException("Official treasure detail text is unavailable: " + itemId);
            long ownedCount = host.Profile.Save.Player.Inventory.FirstOrDefault(value => value.ItemId == itemId)?.Count ?? 0;
            partnerTreasurePopup = OfficialTreasureDetailPopup.Open(canvas.transform, prefab, assets, itemId,
                itemName, itemTypeName, qualityName, ownedCount, () => partnerTreasurePopup = null);
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
                var localization = new OfflinePartnerLocalizationService(host.PartnerDetailAssets.Texts,
                    host.PartnerDetailAssets.Common.GiftTexts, host.PartnerFriendshipAssets.PropertyTexts,
                    host.PartnerFriendshipAssets.DialogTexts);
                var assets = OfficialPartnerFriendshipWindowAssetCatalog.LoadDefault();
                var root = UnityEngine.Object.Instantiate(assets.WindowPrefab,
                    staging.transform, false);
                partnerFriendshipPanel = root;
                partnerFriendshipUiBinding = OfflineUiBindingModule.Register(root.transform, new OfflineUiBindings {
                    Font = () => host.Catalog.Font,
                    Localize = key => localization.GetText(key, LanguageType.zh_CN),
                    Pool = (scope, prefab, parent, keepWorld) => UnityEngine.Object.Instantiate(prefab, parent, keepWorld)
                });
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
        private IDisposable partnerFriendshipUiBinding;
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
            partnerFriendshipUiBinding?.Dispose(); partnerFriendshipUiBinding = null;
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
