using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.BagItemQueries;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Cooking;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.BagTipDots;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.Inventory;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BagItemQueryBindingsValidation
{
    const string Active = "Zjcs.BagItemQueryBindingsValidation.Active";
    static IEnumerator checks; static int frame = -1, errors;
    static HomeFeatureRuntime home;
    static BagItemQueryBindingsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false)) {
                AudioListener.volume = 0; Application.runInBackground = true;
                Application.logMessageReceived += OnLog; checks = Check();
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Local Bag query binding validation timed out"));
            if (checks == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (!checks.MoveNext()) { checks = null; Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)); } }
            catch (Exception error) { checks = null; Finish(error); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_BAG_BINDINGS_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false); home?.Close(); home = null;
        Application.logMessageReceived -= OnLog;
        if (error == null) Debug.Log("BAG_ITEM_QUERY_BINDINGS_PASSED: original configuration, real isolated inventory/pet/card/formula/equipment data, pure queries, pending-purify persistence/validation, actual home Bag entry/close/reopen, formal indicators and native item rows. Full native storage composition and Windows Player remain separate.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    static string Work => Environment.GetEnvironmentVariable("ZJCS_BAG_BINDINGS_OUTPUT");
    static IEnumerator Check()
    {
        var catalog = OfflineBagItemQueryCatalog.LoadDefault();
        Require(catalog.Items.Count > 10000 && catalog.Pay.PeriodicCardMaxDurationDay > 0, "Selected original item/eligibility configuration is packaged locally");
        string root = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(root); string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "行囊验证";
        Require(creation.TryCreateCharacter(slot, draft, out var error), error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
        var slots = new ProfileSlotCatalog(root); var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var cooking = new OfflineCookingService(profile, OfficialCookingCatalog.LoadDefault());
        var recipe = catalog.Cookings.First(row => row.ConditionType == "UsedItemCondition" && row.UseSourceOperate == 89
            && cooking.GetRecipe(row.Id)?.formulaItemId == row.ConditionItemId);
        var pet = catalog.PetRows.First(row => catalog.ShowCanUseRedDotItemTypes.Contains((ItemType)catalog.Item(row.PetPieceId).ItemType));
        int equipPiece = catalog.Items.First(row => row.ItemType == (int)ItemType.CommonEquipPiece && catalog.EquipPieceMergeInfoParser.Get(row.ClassId) != null).ClassId;
        int pieceCount = catalog.EquipPieceMergeInfoParser.Get(equipPiece).PieceMergeCount;
        int cardItem = catalog.Items.First(row => row.ItemType == (int)ItemType.PeriodCard && catalog.CardItem(row.ClassId) != null).ClassId;
        int cardId = catalog.CardItem(cardItem).PeriodCardId;
        int replaceItem = catalog.Items.First(row => row.ItemType == (int)ItemType.EquipAttributeReplaceItem && catalog.EquipAttributeReplaceItemInfoParser.Get(row.ClassId) != null).ClassId;
        var replacement = catalog.EquipAttributeReplaceItemInfoParser.Get(replaceItem);
        int sword = catalog.Items.First(row => row.ItemType == (int)ItemType.Sword && replacement.Quality.Contains(row.Quality)).ClassId;
        long equipmentIndex = profile.Save.Player.Equipment.NextInstanceId;
        Require(profile.ApplyCharacterMutation(player => {
            Stack(player, equipPiece, pieceCount - 1); Stack(player, pet.PetPieceId, pet.PieceMergeCount);
            Stack(player, recipe.ConditionItemId, 2); Stack(player, cardItem, 1); Stack(player, replaceItem, 1);
            player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId = equipmentIndex, ItemId = sword, ItemLevel = 1,
                Terms = new List<OfflineEquipmentTerm> { new OfflineEquipmentTerm { Type = 2, PropertyId = replacement.AttributePropId } } });
            player.Equipment.NextInstanceId++;
        }, "bag-query:seed").IsApplied, "Seed actual isolated profile inventory and original replacement property");
        DateTime now = DateTime.UtcNow;
        var query = new OfflineBagItemQueries(() => profile.Save, catalog, () => now);
        var before = Files(root);
        Require(!query.CanUseRedDot(equipPiece, 0, pieceCount - 1), "Real piece ownership below its configured threshold is unavailable");
        int blueItem = catalog.Items.First(row => row.RedDotType == (int)RedDotType.BlueDot
            && catalog.ShowCanUseRedDotItemTypes.Contains((ItemType)row.ItemType)
            && row.ItemType != (int)ItemType.CommonEquipPiece && row.ItemType != (int)ItemType.CommonItemPiece
            && row.ItemType != (int)ItemType.RandomTreasurePiece && row.ItemType != (int)ItemType.PetPiece).ClassId;
        Require(query.GetBlueDotCount(blueItem, 0, 201) == 201, "Actual configured blue item retains the original record-count query");
        Require(query.CanUseRedDot(pet.PetPieceId, 0, pet.PieceMergeCount), "Unowned pet with enough original pieces is available");
        Require(query.CanUseRedDot(recipe.ConditionItemId, 0, 2), "Unlearned formula reads the original inverted cooking condition");
        Require(query.CanUseRedDot(cardItem, 0, 1), "Missing local periodic-card record retains original successful zero count");
        Require(query.GetRedDotType(replaceItem, 0, 1) == (int)RedDotType.RedDot, "Actual equipment quality/property drives dynamic replacement red dot");
        Require(SameFiles(before, Files(root)), "Eligibility, quantities and dynamic indicators never write the selected save");

        DateTime boundary = OfflineRefreshClock.DayStart(now.ToLocalTime());
        Require(profile.ApplyCharacterMutation(player => {
            Stack(player, equipPiece, pieceCount);
            OfflinePetProgress.AddPet(player.Pets, pet.ClassId, catalog.Item(pet.ClassId).Quality);
            player.CharacterProperties.PeriodicCards.Add(new OfflinePeriodicCard { CardId = cardId,
                ExpiresAtUnixSeconds = new DateTimeOffset(boundary.AddDays(catalog.Pay.PeriodicCardMaxDurationDay + 1)).ToUnixTimeSeconds() });
            player.Equipment.Instances.First(row => row.InstanceId == equipmentIndex).PurifyProps.Add(new OfflineEquipmentPurifyProperty {
                Key = 19, Value = new OfflineEquipmentTerm { Type = 1, PropertyId = 10, Factor = .5f } });
        }, "bag-query:owned-states").IsApplied, "Persist real pet, month-card expiry and separate pending purify property");
        Require(query.CanUseRedDot(equipPiece, 0, pieceCount) && !query.CanUseRedDot(pet.PetPieceId, 0, pet.PieceMergeCount),
            "Queries observe replaced Save and existing pet ownership without rebuilding the owner");
        Require(!query.CanUseRedDot(cardItem, 0, 1), "Month-card expiry above the original maximum suppresses eligibility");
        Require(query.GetRedDotType(replaceItem, 0, 1) == catalog.Item(replaceItem).RedDotType, "Persisted pending purify candidates prevent the equipment replacement override");
        Require(cooking.LearnFormula(recipe.ConditionItemId).IsApplied, "Use the actual cooking transaction to learn the selected formula");
        Require(!query.CanUseRedDot(recipe.ConditionItemId, 0, profile.InventoryCount(recipe.ConditionItemId)), "Formula eligibility follows the stored item-use event");
        Require(profile.ApplyCharacterMutation(player => player.CharacterProperties.PeriodicCards.First(row => row.CardId == cardId).ExpiresAtUnixSeconds
            = new DateTimeOffset(boundary.AddDays(catalog.Pay.PeriodicCardMaxDurationDay)).ToUnixTimeSeconds(), "bag-query:card-maximum").IsApplied,
            "Persist the exact configured month-card duration boundary");
        Require(query.CanUseRedDot(cardItem, 0, 1), "Exact month-card duration maximum remains eligible");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var cold = new OfflineBagItemQueries(() => reopened.Save, catalog, () => now);
        Require(reopened.Save.Player.Equipment.Instances.First(row => row.InstanceId == equipmentIndex).PurifyProps.Single().Key == 19
            && !cold.CanUseRedDot(pet.PetPieceId, 0, pet.PieceMergeCount) && !cold.CanUseRedDot(recipe.ConditionItemId, 0, 1)
            && cold.GetRedDotType(replaceItem, 0, 1) == catalog.Item(replaceItem).RedDotType, "Disk reopen restores pending dictionary key/property and all dependent eligibility states");
        var legacy = JObject.Parse(JsonUtility.ToJson(reopened.Save));
        foreach (JObject row in legacy["Player"]["Equipment"]["Instances"]) row.Remove("PurifyProps");
        var parse = typeof(OfflineMainlineProgressSession).GetMethod("ParseSave", BindingFlags.NonPublic | BindingFlags.Static);
        var validate = typeof(OfflineMainlineProgressSession).GetMethod("IsValidSaveJson", BindingFlags.NonPublic | BindingFlags.Static);
        var restored = (OfflineMainlineSave)parse.Invoke(null, new object[] { legacy.ToString() });
        Require((bool)validate.Invoke(null, new object[] { legacy.ToString() }) && restored.Player.Equipment.Instances.All(row => row.PurifyProps != null && row.PurifyProps.Count == 0), "Legacy saves initialize the absent original pending-purify dictionary empty");
        var invalid = JsonUtility.FromJson<OfflineMainlineSave>(JsonUtility.ToJson(reopened.Save));
        var bad = invalid.Player.Equipment.Instances.First(row => row.InstanceId == equipmentIndex);
        bad.PurifyProps.Add(bad.PurifyProps[0]);
        Require(!(bool)validate.Invoke(null, new object[] { JsonUtility.ToJson(invalid) }), "Duplicate pending-purify keys fail the normal save validator");
        var commerce = OfflineCommerceSession.Open(slots, slot, now.ToLocalTime(), profile);
        Require(commerce.UpdateState(state => state.Bought.Add(new CommerceCounter {
            Key = "periodic-expiry:" + cardId, Count = state.Day + catalog.Pay.PeriodicCardMaxDurationDay + 1 })),
            "Actual commerce owner persists the canonical periodic-card expiry counter");
        Require(profile.ApplyCharacterMutation(player => player.CharacterProperties.PeriodicCards.Clear(), "bag-query:stale-card-projection").IsApplied,
            "Isolate a stale derived card-property projection");
        Require(!query.CanUseRedDot(cardItem, 0, 1), "Embedded commerce ledger remains authoritative when the derived card list is stale");
        Require(commerce.UpdateState(state => state.Bought.First(row => row.Key == "periodic-expiry:" + cardId).Count
            = state.Day + catalog.Pay.PeriodicCardMaxDurationDay), "Actual commerce owner updates the duration boundary");
        Require(query.CanUseRedDot(cardItem, 0, 1), "Canonical purchase state uses the shared local daily boundary");

        // Real production home -> Bag route, using the existing scene input.
        home = HomeFeatureRuntime.Begin(slot, root, null); home.TutorialAutoStartEnabled = false;
        yield return null; yield return null;
        OpenStorage(); yield return null; yield return null;
        var inventory = Object.FindObjectOfType<InventoryFeatureRuntime>();
        Require(inventory, "Actual home storage click opens the formal inventory runtime");
        var panel = inventory.GetComponent<OfficialInventoryPanelView>();
        Require(Field<IOfflineInventoryItemIndicators>(panel, "indicators") is OfflineBagItemQueries, "Formal inventory presentation uses the production local indicator owner");
        SelectTab(panel, recipe.ConditionItemId); yield return null; yield return null;
        var displayed = Field<List<OfflineInventoryItemView>>(panel, "displayedItems");
        var visibleRecipe = displayed.Single(row => row.ItemId == recipe.ConditionItemId);
        Require(visibleRecipe.CanUseRedDot == false && displayed.All(row => row.CanUseRedDot.HasValue), "Actual rendered inventory rows receive complete original indicator decisions");
        Capture(inventory, "storage-bag-local-indicators.png");
        var close = inventory.GetComponentsInChildren<Button>(true).First(button => button.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 && button.gameObject.activeInHierarchy);
        Click(close); yield return null; yield return null;
        Require(!Object.FindObjectOfType<InventoryFeatureRuntime>(), "Original close button returns from the actual Bag page");
        home.Close(); home = null; yield return null; yield return null;
        home = HomeFeatureRuntime.Begin(slot, root, null); home.TutorialAutoStartEnabled = false;
        yield return null; yield return null;
        OpenStorage(); yield return null; yield return null;
        inventory = Object.FindObjectOfType<InventoryFeatureRuntime>(); panel = inventory.GetComponent<OfficialInventoryPanelView>();
        SelectTab(panel, recipe.ConditionItemId); yield return null; yield return null;
        visibleRecipe = Field<List<OfflineInventoryItemView>>(panel, "displayedItems").Single(row => row.ItemId == recipe.ConditionItemId);
        Require(visibleRecipe.Count == 1 && visibleRecipe.CanUseRedDot == false, "Actual home/Bag cold reopen restores the same learned formula and remaining quantity");
        Capture(inventory, "storage-bag-local-indicators-reopened.png");
        home.Close(); home = null; yield return null; yield return null;
    }
    static void Stack(OfflinePlayerProgress player, int id, long count)
    {
        var stack = player.Inventory.FirstOrDefault(row => row.ItemId == id);
        if (stack == null) player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = count }); else stack.Count = count;
    }
    static Dictionary<string, byte[]> Files(string path) => Directory.GetFiles(path, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes);
    static bool SameFiles(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after) => before.Count == after.Count && before.All(row => after.TryGetValue(row.Key, out var bytes) && row.Value.SequenceEqual(bytes));
    static void OpenStorage()
    {
        Require(home.Scene.TryGetFurnitureClickBounds(7, out var bounds), "Home exposes its original storage click bounds");
        Vector2 point = home.Scene.Camera.WorldToScreenPoint(bounds.center);
        var input = Field<object>(home, "input");
        foreach (string method in new[] { "PointerDown", "PointerUp" })
            input.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, new object[] { point, false });
    }
    static void SelectTab(OfficialInventoryPanelView panel, int itemId)
    {
        var itemType = OfflineBagItemQueryCatalog.LoadDefault().Item(itemId).ItemType;
        var tabs = Field<IReadOnlyList<OfficialInventoryTab>>(panel, "tabs");
        var tab = tabs.First(row => row.ItemTypes.Contains(itemType));
        var nodes = Field<Dictionary<string, RectTransform>>(panel, "nodes");
        var toggle = nodes[tab.Path].GetComponent<Toggle>();
        Require(toggle && toggle.gameObject.activeInHierarchy, "Original category toggle is available");
        if (!toggle.isOn) Click(toggle.gameObject);
        Require(Field<OfficialInventoryTab>(panel, "currentTab") == tab, "Original category pointer selects its configured item types");
    }
    static void Click(Button button)
        => Click(button.gameObject);
    static void Click(GameObject target)
    {
        var rect = (RectTransform)target.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        bool top = hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform));
        if (!top) {
            var inventory = Object.FindObjectOfType<InventoryFeatureRuntime>(); if (inventory) Capture(inventory, "pointer-hit-failure.png");
            Debug.Log("POINTER: " + target.name + " at " + point + "; screen=" + Screen.width + "x" + Screen.height + "; first="
                + string.Join(",", hits.Take(6).Select(hit => hit.gameObject.name + " parent=" + hit.gameObject.transform.parent?.name)));
        }
        Require(top, "Original inventory control is the top UI pointer hit: " + target.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static void Capture(InventoryFeatureRuntime inventory, string name)
    {
        var canvas = inventory.GetComponent<Canvas>().rootCanvas; var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
        var canvases = canvas.GetComponentsInChildren<Canvas>(true);
        var layers = canvases.Select(value => value.sortingLayerID).ToArray(); var orders = canvases.Select(value => value.sortingOrder).ToArray();
        var camera = home.Scene.Camera; var priorTarget = camera.targetTexture; var active = RenderTexture.active;
        int width = Screen.width, height = Screen.height;
        var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try {
            foreach (var current in canvases) { current.sortingLayerID = SortingLayer.layers.Last().id; if (current.sortingOrder < 30000) current.sortingOrder += 30000; }
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Require(image.GetPixels32().Count(color => color.r > 90 || color.g > 90 || color.b > 100) > 10000, "Actual Bag page render contains its native UI and item rows");
            File.WriteAllBytes(Path.Combine(Work, name), image.EncodeToPNG());
        } finally {
            canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance; camera.targetTexture = priorTarget;
            for (int i = 0; i < canvases.Length; i++) if (canvases[i]) { canvases[i].sortingLayerID = layers[i]; canvases[i].sortingOrder = orders[i]; }
            RenderTexture.active = active; target.Release(); Object.Destroy(target); Object.Destroy(image); Canvas.ForceUpdateCanvases();
        }
    }
}
