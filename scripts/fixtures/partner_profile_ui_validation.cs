using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using TMPro;
using Status = Zjcs.Official.PartnersUI.NpcDetailPanelProfileContent.ProfileStatus;
using ImageStatus = Zjcs.Official.PartnersUI.NpcDetailPanelProfileContent.ContentImageStatus;

// Reuses the imported portrait camera/canvas. Only the parent viewport and
// tip-dot endpoint are fixture scaffolding; the profile hierarchy is native.
public sealed class PartnerProfileUiValidation : IOfflinePartnerProfilePresentation, ILocalizationTextService
{
    sealed class Tips : IProfessionTipDotSystem, IProfessionTipDotSystemController
    {
        public bool IsShowTipDot => true;
        public bool TempLimitShowTip => false;
        public Action OnTipDotShowChanged { get; set; }
        public SafeAction OnTipDotSystemInited { get; set; } = new SafeAction();
        public SafeAction OnTipDotSystemPreClear { get; set; } = new SafeAction();
        public TipDotGraphType? GetGraphType(string name) => TipDotGraphType.Dynamic;
        public bool LinkNodeStateChange(TipDotNodeKey key, GameObject owner, Action<TipDotStateInfo> callback) { callback(default); return true; }
        public bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject owner, Action<TipDotStateInfo> callback) => true;
    }
    readonly Tips tips = new Tips();
    OfficialPartnerProfileAssetCatalog assets;
    OfflinePartnerGiftRuntimeBindings icons;
    OfflinePartnerProfileUiServices ui;
    OfflinePartnerProfileService profile;
    OfflineMainlineProgressSession session;
    NpcDetailPanelProfileContent page;
    GameObject root;
    Camera camera;
    EventSystem events;
    int failures, commits, clicks;

    public static IEnumerator Check(Transform parent, Camera camera, EventSystem events, TMP_FontAsset font)
        => new PartnerProfileUiValidation().Run(parent, camera, events, font);

    IEnumerator Run(Transform parent, Camera camera, EventSystem events, TMP_FontAsset font)
    {
        this.camera = camera; this.events = events;
        var previousClear = camera.clearFlags; camera.clearFlags = CameraClearFlags.SolidColor;
        var otherCanvases = Object.FindObjectsOfType<Canvas>().Where(canvas => canvas.transform != parent && !canvas.transform.IsChildOf(parent)).ToArray();
        foreach (var canvas in otherCanvases) canvas.enabled = false;
        var otherChildren = parent.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToArray();
        foreach (var child in otherChildren) child.gameObject.SetActive(false);
        assets = OfficialPartnerProfileAssetCatalog.Load();
        LanguageManager.Initialize(this);
        var sounds = new OfflinePartnerUiSoundScope(OfficialPartnerPanelAssetCatalog.Load().GetSound);
        var selected = OfficialPartnerAssetCatalog.LoadDefault();
        var rules = new PartnerProfileRules(selected.Rows);
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-profile-ui"));
        string slot = slots.CreateSlot();
        session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "档案点击验证";
            player.Inventory.Clear(); player.CharacterProperties.NpcFriendships.Clear();
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 19 });
        }).IsApplied, "isolated real character");
        profile = new OfflinePartnerProfileService(session, rules);
        ui = new OfflinePartnerProfileUiServices(profile, this);
        var images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        images.Entries = assets.Images.Entries.Concat(assets.Common.Images.Entries).GroupBy(row => row.Path).Select(group => group.First()).ToArray();
        var loader = new OfflineProfessionImageResourceLoader(images);
        icons = new OfflinePartnerGiftRuntimeBindings(session, selected, assets.Common,
            id => new OfflinePartnerItemInfo(selected, id), Localize, loader);
        var previousImages = ProfessionImageResourceBridge.Provider;
        var previousText = WorldMultiLanguageText.Localize;
        var previousFont = WorldMultiLanguageText.LanguageFont;
        var previousPool = ProfessionGameObjectPoolBridge.Provider;
        ProfessionImageResourceBridge.Provider = _ => loader;
        WorldMultiLanguageText.Localize = Localize;
        WorldMultiLanguageText.LanguageFont = () => font;
        ProfessionGameObjectPoolBridge.Provider = (scope, prefab, slotTransform, world) => {
            var instance = Object.Instantiate(prefab, slotTransform, world);
            foreach (var linker in instance.GetComponentsInChildren<TipDotNodeLinker>(true)) ConfigureTip(linker);
            return instance;
        };
        var inactive = new GameObject("Inactive original profile binding", typeof(RectTransform));
        inactive.transform.SetParent(parent, false); inactive.SetActive(false);
        var rect = (RectTransform)inactive.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        root = Object.Instantiate(assets.ProfilePrefab, inactive.transform, false);
        page = root.GetComponent<NpcDetailPanelProfileContent>();
        Require(page && root.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), "exact native subtree has no missing scripts");
        page.Configure(ui); page.Init(ui.GetNpcData(8401));
        inactive.SetActive(true); root.SetActive(true);
        yield return null; yield return new WaitForSeconds(0.3f);
        Verify(Status.Lock, ImageStatus.Short);
        Require(!Get<WorldStateSwitcher<bool>>(page, "mIsLongProfileStates").State && ShortTabs().Count == 3, "original three-chapter selector");
        Require(Get<WorldMultiLanguageText>(page, "mLockTipText").Text.Contains("20"), "original locked condition description");
        Require(Get<ItemIcon>(page, "mItemIcon").IconSprite, "original configured reward sprite is loaded");
        Capture("profile-short-locked.png");

        Require(session.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.Single().Level = 20).IsApplied, "first chapter threshold");
        root.SetActive(false); root.SetActive(true); yield return null;
        Verify(Status.CanUnlock, ImageStatus.Short);
        Capture("profile-short-ready.png");
        Click(Get<Button>(page, "mUnlockBtn").gameObject); yield return null;
        Verify(Status.Unlock, ImageStatus.Hide);
        Require(session.InventoryCount(107) == 3 && profile.ReceivedProfileRewards(8401).SequenceEqual(new[] { 1 }) && commits == 1, "pointer claim commits original reward once");
        Require(Get<WorldMultiLanguageText>(page, "mContentText").Text == assets.Texts["npc_profile_Content_8401_1"], "exact original unlocked body");
        Capture("profile-short-unlocked.png");
        Click(ShortTabs()[1].TabItem.gameObject); yield return null;
        Verify(Status.Lock, ImageStatus.Short);
        Click(ShortTabs()[0].TabItem.gameObject); yield return null;
        Verify(Status.Unlock, ImageStatus.Hide);
        Require(session.InventoryCount(107) == 3 && commits == 1, "revisiting chapter cannot award again");

        Require(session.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.Single().Level = 40).IsApplied, "second chapter threshold");
        root.SetActive(false); root.SetActive(true); yield return null;
        Require(Get<PartnerProfileRules.ProfileInfo>(page, "mProfileInfo").ProfileId == 2, "original reopen prefers first unclaimed eligible chapter");
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Click(Get<Button>(page, "mUnlockBtn").gameObject); yield return null;
            Verify(Status.CanUnlock, ImageStatus.Short);
            Require(failures == 1 && commits == 1 && session.InventoryCount(107) == 3, "failed pointer claim keeps body locked and reward absent");
        }
        Click(Get<Button>(page, "mUnlockBtn").gameObject); yield return null;
        Verify(Status.Unlock, ImageStatus.Hide);
        Require(session.InventoryCount(107) == 6 && commits == 2, "retry pointer grants once");
        session.Reload(); root.SetActive(false); root.SetActive(true); yield return null;
        Click(ShortTabs()[1].TabItem.gameObject); yield return null;
        Verify(Status.Unlock, ImageStatus.Hide);
        Require(session.InventoryCount(107) == 6 && commits == 2, "reload and revisit preserve receipt");
        Click(ShortTabs()[2].TabItem.gameObject); yield return null;
        Verify(Status.Lock, ImageStatus.Long);
        Capture("profile-short-final-locked.png");

        int longNpc = selected.Rows("npc_profile").GroupBy(row => Convert.ToInt32(row["Id"]))
            .First(group => group.Count() == 5 && !(group.Key >= 11015 && group.Key <= 11022)).Key;
        Require(session.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = longNpc, Level = 1 })).IsApplied, "real five-chapter NPC");
        root.SetActive(false); page.Init(ui.GetNpcData(longNpc)); root.SetActive(true);
        yield return null; yield return null; yield return new WaitForSeconds(0.3f);
        Require(Get<WorldStateSwitcher<bool>>(page, "mIsLongProfileStates").State && LongTabs().Count == 5, "original five-chapter selector");
        Capture("profile-five-chapters.png");
        var scroll = Get<ScrollRect>(page, "mTabScrollRect");
        // The last chapter begins outside the original viewport. Simulate a
        // scroll-wheel input through the actual ScrollRect before clicking it.
        var wheel = new PointerEventData(events) { scrollDelta = new Vector2(-1200, 0) };
        ExecuteEvents.Execute(scroll.gameObject, wheel, ExecuteEvents.scrollHandler);
        yield return null; yield return new WaitForSeconds(0.3f);
        Click(LongTabs().Last().TabItem.gameObject); yield return null;
        Require(Get<PartnerProfileRules.ProfileInfo>(page, "mProfileInfo").ProfileId == LongTabs().Last().Info.ProfileId, "pointer selects final original chapter after scroll input");
        Capture("profile-five-last.png");
        root.SetActive(false); yield return null;
        Require(root.GetComponentsInChildren<ItemIcon>(true).Length == 0, "disable recycles reward icon");
        Object.Destroy(inactive); yield return null;
        icons.Dispose(); ProfessionImageResourceBridge.Provider = previousImages; WorldMultiLanguageText.Localize = previousText;
        ProfessionGameObjectPoolBridge.Provider = previousPool;
        sounds.Dispose();
        camera.clearFlags = previousClear;
        WorldMultiLanguageText.LanguageFont = previousFont;
        foreach (var canvas in otherCanvases) if (canvas) canvas.enabled = true;
        foreach (var child in otherChildren) if (child) child.gameObject.SetActive(true);
        Object.Destroy(images);
        Debug.Log("Original profile UI passed: native short/long chapter selectors, actual pointer/scroll, locked/ready/body/reward, failed claim retry, receipt reload and icon recycling; clicks=" + clicks);
    }

    List<NpcDetailPanelProfileContentTabItem> ShortTabs() => Get<List<NpcDetailPanelProfileContentTabItem>>(page, "mShortTabItems").Where(tab => tab.gameObject.activeInHierarchy).ToList();
    List<NpcDetailPanelProfileContentTabItem> LongTabs() => Get<List<NpcDetailPanelProfileContentTabItem>>(page, "mTabItems").Where(tab => tab.gameObject.activeInHierarchy).ToList();
    void Verify(Status expected, ImageStatus image)
    {
        Require(Get<WorldStateSwitcher<Status>>(page, "mStateSwitcher").State == expected, "original profile state " + expected);
        Require(Get<WorldStateSwitcher<ImageStatus>>(page, "mContentImageStateSwitcher").State == image, "original content image " + image);
        var attachment = Get<ItemIcon>(page, "mItemIcon").GetComponentInChildren<ItemIconAttachment>(true)
            .GetAttachment(ItemIconAttachmentType.GotTip, false);
        Require(attachment && attachment.gameObject.activeSelf == (expected == Status.Unlock), "original received-reward mark follows chapter state");
    }
    public ItemIcon GetIcon(int id, Transform slot) => icons.CreateGiftIcon(id, slot, assets.RewardIconPrefab);
    public void RecycleIcon(ItemIcon icon) { icon.Clear(); icon.gameObject.SetActive(false); Object.Destroy(icon.gameObject); }
    public void ConfigureTip(TipDotNodeLinker linker) { linker.Controller = tips; linker.TipDotSystem = tips; }
    public bool ContainsText(string key) => assets.Texts.ContainsKey(key);
    public LanguageType CurrentLanguage => LanguageType.zh_CN;
    public void ReloadData() { }
    public bool ContainsKey(string key) => ContainsText(key);
    public bool TryGetOriginalText(string key, LanguageType language, out string value) => assets.Texts.TryGetValue(key, out value);
    public string GetText(string key, LanguageType language, params object[] args) => args.Length == 0 ? Localize(key) : string.Format(Localize(key), args);
    public string GetTextFormated(string key, LanguageType language, params object[] args) => GetText(key, language, args);
    public void SetReplacer(ITextReplacer replacer) { }
    public bool TryGetText(string key, LanguageType language, out string value) => TryGetOriginalText(key, language, out value);
    public bool TryGetText(string key, out string value) => TryGetOriginalText(key, CurrentLanguage, out value);
    public void AddDataProvider(ILanguageTextDataProvider provider) { }
    string Localize(string key) => assets.Texts.TryGetValue(key, out var word) ? word : key;
    public string FormatText(string key, object value) => string.Format(Localize(key), value);
    public void OnProfileCommitted() => commits++;
    public void ShowFailure(ProgressionResult result) { Require(!result.IsApplied, "failure presentation"); failures++; }
    void Click(GameObject button)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform;
        foreach (float y in new[] { 0.5f, 0.25f, 0.75f }) foreach (float x in new[] { 0.5f, 0.25f, 0.75f })
        {
            var point = new Vector2(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x), Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y));
            var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(point)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); events.RaycastAll(data, hits);
            if (hits.Count == 0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) != button) continue;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            clicks++; return;
        }
        throw new Exception("Original profile target is occluded: " + button.name + " " + rect.position);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        var target = camera.targetTexture;
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target; var frame = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        frame.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); frame.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), frame.EncodeToPNG());
        RenderTexture.active = null; Object.Destroy(frame);
    }
    static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    static void Require(bool value, string message) { if (!value) throw new Exception("Original profile UI: " + message); }
}
