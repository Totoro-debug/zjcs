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
using Rules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;



public sealed class PartnerAchievementUiValidation : IOfflinePartnerAchievementPresentation
{
    OfficialPartnerAchievementAssetCatalog assets;
    OfflinePartnerGiftRuntimeBindings icons;
    OfflinePartnerAchievementUiServices ui;
    OfflinePartnerAchievementService achievements;
    OfflineMainlineProgressSession session;
    PartnerAchievementPanel panel;
    GameObject root;
    Camera camera;
    EventSystem events;
    int failures, commits, clicks;
    bool groupLimited = true;
    public static IEnumerator Check(Transform parent, Camera camera, EventSystem events, TMP_FontAsset font)
        => new PartnerAchievementUiValidation().Run(parent, camera, events, font);
    IEnumerator Run(Transform parent, Camera camera, EventSystem events, TMP_FontAsset font)
    {
        this.camera = camera; this.events = events;
        var previousClear = camera.clearFlags; camera.clearFlags = CameraClearFlags.SolidColor;
        var otherCanvases = Object.FindObjectsOfType<Canvas>().Where(canvas => canvas.transform != parent && !canvas.transform.IsChildOf(parent)).ToArray();
        foreach (var canvas in otherCanvases) canvas.enabled = false;
        var otherChildren = parent.Cast<Transform>().Where(child => child.gameObject.activeSelf).ToArray();
        foreach (var child in otherChildren) child.gameObject.SetActive(false);
        assets = OfficialPartnerAchievementAssetCatalog.Load();
        var selected = OfficialPartnerAssetCatalog.LoadDefault();
        var rules = new Rules(selected.Rows);
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-achievement-ui"));
        string slot = slots.CreateSlot();
        session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "馈赠点击验证";
            player.Inventory.Clear(); player.CharacterProperties.NpcFriendships.Clear();
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 4 });
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 10608, Level = 5 });
        }).IsApplied, "isolated real character");
        achievements = new OfflinePartnerAchievementService(session, rules);
        achievements.RefreshFromSave();
        ui = new OfflinePartnerAchievementUiServices(achievements, selected, () => groupLimited, this);
        Require(ui.AchievementPanelShowCount == 5 && !ui.ShowAllInfosInSameType(Rules.AchievementType.NpcFriendshipLevel), "original selected display settings");
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
        ProfessionGameObjectPoolBridge.Provider = (scope, prefab, slotTransform, worldSpace) => Object.Instantiate(prefab, slotTransform, worldSpace);
        var sounds = new OfflinePartnerUiSoundScope(OfficialPartnerPanelAssetCatalog.Load().GetSound);
        var inactive = new GameObject("Inactive original achievement binding", typeof(RectTransform));
        inactive.transform.SetParent(parent, false); inactive.SetActive(false);
        var rect = (RectTransform)inactive.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        root = Object.Instantiate(assets.PanelPrefab, inactive.transform, false);
        panel = root.GetComponent<PartnerAchievementPanel>();
        Require(panel && root.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), "native region has no missing scripts");
        panel.Configure(ui); panel.SetDataKey(new Rules.AchievementDataKeyNpcFriendshipLevel(8401));
        inactive.SetActive(true); root.SetActive(true);
        yield return null; yield return new WaitForSeconds(0.3f);
        Require(Rows().Length == 5 && Rows().All(row => row.StateSwitcher.State == Rules.AchievementState.Processing), "original five processing rows below threshold");
        Require(Rows()[0].ProgressText.Text.Contains("4") && Rows()[0].ProgressText.Text.Contains("5"), "original current/max progress");
        Require(Rows()[0].TitleText.Text.Contains(Localize("ui_npc_name_8401")), "original NPC title argument");
        Require(root.GetComponentsInChildren<ItemIcon>().All(icon => icon.IconSprite), "all original configured reward images loaded");
        Capture("achievement-processing.png");
        session.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.First(value => value.NpcClassId == 8401).Level = 10);
        achievements.RefreshFromSave(); Reopen(); yield return null; yield return new WaitForSeconds(0.3f);
        Require(Rows()[0].StateSwitcher.State == Rules.AchievementState.Completed && Rows()[1].StateSwitcher.State == Rules.AchievementState.Completed, "two completed rows sorted first");
        Capture("achievement-ready.png");
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Click(Rows()[0].GetRewardBtn.gameObject); yield return null;
            Require(failures == 1 && commits == 0 && session.InventoryCount(100) == 0 && Rows()[0].StateSwitcher.State == Rules.AchievementState.Completed, "failed pointer claim preserves rows and inventory");
        }
        Click(Rows()[0].GetRewardBtn.gameObject); yield return null;
        Require(commits == 1 && session.InventoryCount(100) == 3 && session.InventoryCount(101) == 3, "pointer receives all currently eligible rewards from actual requested group");
        Require(achievements.ReadCurrentPlayer().IsAlreadyGotAchievementReward(rules.GetAchievementInfoByGroupAndIndex(400, 1)), "atomic receipt saved");
        Require(Rows().All(row => row.StateSwitcher.State == Rules.AchievementState.Processing), "original callback removes completed rows");
        yield return new WaitForSeconds(0.3f); Capture("achievement-received.png");
        session.Reload(); Reopen(); yield return null;
        Require(commits == 1 && session.InventoryCount(100) == 3 && Rows()[0].ProgressText.Text.Contains("15"), "reopen removes received rows without a second reward");
        groupLimited = false; Reopen(); yield return null;
        Require(Rows().All(row => !Get<WorldStateSwitcher<bool>>(row, "mTipDotStateSwitcher").State), "original achievement-group function branch");
        panel.SetDataKey(new Rules.AchievementDataKeyStarGodNpcFriendshipLevel(10608)); yield return null;
        Require(Rows()[0].StateSwitcher.State == Rules.AchievementState.Completed, "independent star friendship key");
        Click(Rows()[0].GetRewardBtn.gameObject); yield return null;
        Require(commits == 2 && session.InventoryCount(118) == 2, "star pointer grants configured reward without receiving ordinary star-NPC group");
        Require(achievements.ReadCurrentPlayer().IsAnyCanGetAchievementReward(new Rules.AchievementDataKeyNpcFriendshipLevel(10608)), "ordinary and star groups stay independent");
        yield return new WaitForSeconds(0.3f); Capture("achievement-star-received.png");
        root.SetActive(false); yield return null;
        Require(root.GetComponentsInChildren<ItemIcon>(true).Length == 0, "disable recycles reward icons");
        Object.Destroy(inactive); yield return null;
        icons.Dispose(); sounds.Dispose(); Object.Destroy(images);
        ProfessionImageResourceBridge.Provider = previousImages; WorldMultiLanguageText.Localize = previousText;
        WorldMultiLanguageText.LanguageFont = previousFont; camera.clearFlags = previousClear;
        ProfessionGameObjectPoolBridge.Provider = previousPool;
        foreach (var canvas in otherCanvases) if (canvas) canvas.enabled = true;
        foreach (var child in otherChildren) if (child) child.gameObject.SetActive(true);
        Debug.Log("Original achievement UI passed: native five-row range/sort/progress/reward images, actual pointer claim failure/retry, reload, group-function branch and independent normal/star rewards; clicks=" + clicks);
    }
    void Reopen() { root.SetActive(false); root.SetActive(true); }
    PartnerAchievementPanelItem[] Rows() => root.GetComponentsInChildren<PartnerAchievementPanelItem>().Where(row => row.gameObject.activeInHierarchy).ToArray();
    public ItemIcon GetIcon(int id, Transform parent) => icons.CreateGiftIcon(id, parent, assets.RewardIconPrefab);
    public void RecycleIcon(ItemIcon icon) { icon.Clear(); icon.gameObject.SetActive(false); Object.Destroy(icon.gameObject); }
    public string Localize(string key) => assets.Texts.TryGetValue(key, out var word) ? word : key;
    public void ShowSystemMessage(string key) { throw new Exception("Unexpected original message: " + key); }
    public void ShowFailure(ProgressionResult result) { Require(!result.IsApplied, "failure presentation"); failures++; }
    public void OnAchievementCommitted() => commits++;
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
        throw new Exception("Original achievement target is occluded: " + button.name + " " + rect.position);
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
    static void Require(bool value, string message) { if (!value) throw new Exception("Original achievement UI: " + message); }
}
