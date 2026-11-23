using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;

public sealed partial class HomeFurnitureControllerChecks
{
    IEnumerator CheckAppearanceAvailability()
    {
        string[] blocked = { "pWeapon_100_G_125", "pWeapon_200_G_125", "pWeapon_100_G_140", "pWeapon_200_G_140" };
        foreach (string name in blocked)
        {
            Require(!OfflineAppearanceAvailability.IsSelectable(name), "User-confirmed blank artwork stays closed: " + name);
            var state = new WardrobeAppearanceState { Sex = 0, Profession = 1 };
            var items = new Dictionary<int, WardrobeItemDefinition> { [1] = new WardrobeItemDefinition {
                ItemId = 1, ItemType = 317, SkinBySex = new Dictionary<int, int> { [0] = 1 } } };
            var skins = new Dictionary<int, WardrobeSkinDefinition> { [1] = new WardrobeSkinDefinition { SkinId = 1, SkinName = name, Type = 7 } };
            Require(!OfflineWardrobeRules.TryWear(state, 1, new Dictionary<int, long> { [1] = 1 }, items, skins,
                new HashSet<string> { "0:1:1" }, 100) && state.AppendedItemByType.Count == 0 && state.WearTimeByType.Count == 0,
                "Closed artwork cannot enter the wardrobe mutation: " + name);
        }
        Require(OfflineAppearanceAvailability.IsSelectable("pWeapon_100_G_007"), "Complete original weapon artwork remains selectable");
        var assets = OfficialCharacterAssetCatalog.LoadDefault();
        var catalog = assets.Load();
        var tasks = OfficialMainlineTaskCatalog.Load(Resources.Load<TextAsset>("Official/Progression/mainline-tasks").text,
            Resources.Load<TextAsset>("Official/Progression/mainline-tasks-source-manifest").text);
        string profiles = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR"), "wardrobe-profiles");
        var slots = new ProfileSlotCatalog(profiles);
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        var closedItems = catalog.Appearances.Values.Where(item => item.SkinBySex.TryGetValue(0, out int skin)
            && catalog.Skins.TryGetValue(skin, out var definition) && !OfflineAppearanceAvailability.IsSelectable(definition.SkinName)).ToArray();
        Require(closedItems.Length > 0, "Actual packaged wardrobe contains a closed source artwork case");
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "隔离外观验证";
            player.Wardrobe.Sex = 0; player.Wardrobe.Profession = 1;
            foreach (var item in closedItems) player.Inventory.Add(new OfflineItemStack { ItemId = item.ItemId, Count = 1 });
        }, "seed-closed-artwork").IsApplied, "Seed only the isolated wardrobe profile");
        var progression = new OfflineCharacterProgressionService(session, catalog);
        foreach (var item in closedItems)
        {
            string before = JsonUtility.ToJson(session.Save.Player);
            Require(!progression.IsAppearanceAvailable(item.ItemId) && !progression.CanWearAppearance(item.ItemId, 100), "Actual wardrobe eligibility rejects closed artwork");
            Require(!progression.WearAppearance(item.ItemId, 100, "closed-artwork-" + item.ItemId).IsApplied
                && JsonUtility.ToJson(session.Save.Player) == before, "Rejected wear preserves inventory and stored wardrobe");
        }
        var canvas = New("WardrobeAvailabilityCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = viewCamera; canvas.planeDistance = 1;
        var panel = canvas.gameObject.AddComponent<OfficialWardrobePanelView>();
        panel.Initialize(assets, progression, null, () => { }, selectedSlotId: slot, profileSavesRoot: profiles);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(OfficialWardrobePanelView).GetMethod("SelectCategory", flags).Invoke(panel, new object[] { 317, null, null });
        var showAll = (Toggle)typeof(OfficialWardrobePanelView).GetField("showAllToggle", flags).GetValue(panel);
        Require(showAll, "Actual wardrobe show-all control is present");
        showAll.isOn = false;
        ExecuteEvents.Execute(showAll.gameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        yield return null;
        Require(showAll.isOn, "Process-local submit reaches the real wardrobe show-all control");
        var entries = (IEnumerable)typeof(OfficialWardrobePanelView).GetField("visibleEntries", flags).GetValue(panel);
        foreach (var entry in entries)
        {
            int itemId = (int)entry.GetType().GetField("ItemId").GetValue(entry);
            Require(itemId == 0 || closedItems.All(item => item.ItemId != itemId), "Show-all cannot expose closed artwork cards");
        }
        Canvas.ForceUpdateCanvases();
        var render = new RenderTexture(608, 1080, 24); viewCamera.targetTexture = render; viewCamera.Render();
        var previous = RenderTexture.active; RenderTexture.active = render;
        var capture = new Texture2D(608, 1080, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0, 0, 608, 1080), 0, 0); capture.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_FURNITURE_VALIDATION_DIR"), "wardrobe-closed-artwork.png"), capture.EncodeToPNG());
        RenderTexture.active = previous; viewCamera.targetTexture = null; render.Release(); Destroy(render); Destroy(capture);
        Destroy(canvas.gameObject); yield return null;
        session.Reload();
        Require(closedItems.All(item => session.InventoryCount(item.ItemId) == 1) && session.Save.Player.Wardrobe.WornItems.Count == 0,
            "Disk reopen preserves source items without equipping closed artwork");
        Debug.Log("HOME_APPEARANCE_AVAILABILITY_PASSED: four closed artwork names, actual catalog/transactions, process-local controls, show-all filtering and isolated disk reopen.");
    }
}
