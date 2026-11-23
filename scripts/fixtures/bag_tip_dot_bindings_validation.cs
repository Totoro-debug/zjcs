using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.BagItemQueries;
using Zjcs.Offline.BagTipDots;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Cooking;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.BagTipDots;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.Inventory;
using Object = UnityEngine.Object;
using BagItemType = Zjcs.Official.BagTipDots.ItemType;
using BagEquipPos = Zjcs.Official.BagTipDots.EquipPos;
using BagFunctionType = Zjcs.Official.BagTipDots.FunctionType;

[InitializeOnLoad]
public static class BagTipDotBindingsValidation
{
    const string Active = "Zjcs.BagTipDotBindingsValidation.Active";
    static IEnumerator checks; static int frame = -1, errors;
    static HomeFeatureRuntime home;
    static GameObject tipStage;
    static BagTipDotBindingsValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false)) {
                AudioListener.volume = 0; Application.runInBackground = true;
                Application.logMessageReceived += OnLog; checks = Check();
            }
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Complete Bag binding validation timed out"));
            if (checks == null || !EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (!checks.MoveNext()) { checks = null; Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)); } }
            catch (Exception error) { checks = null; Finish(error); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_BAG_TIP_BINDINGS_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 600);
        EditorApplication.EnterPlaymode();
    }
    static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false); if (tipStage) Object.Destroy(tipStage);
        home?.Close(); home = null; Application.logMessageReceived -= OnLog;
        if (error == null) Debug.Log("BAG_TIP_DOT_BINDINGS_PASSED: complete original graph in the formal Home owner, selected local inventory/formula/equipment/furniture queries, native storage tip subtree, rendered changes, actual Bag entry/close, separate-session cooking transaction, monotonic daily notifications, disk reopen and original graph teardown. Full native building/room composition and Windows Player remain separate.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    static string Work => Environment.GetEnvironmentVariable("ZJCS_BAG_TIP_BINDINGS_OUTPUT");
    static IEnumerator Check()
    {
        string root = Path.Combine(Work, "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(root); string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "红点验证";
        Require(creation.TryCreateCharacter(slot, draft, out var error), error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var assets = OfflineBagTipDotAssets.LoadDefault();
        Require(assets.Graph.name == "Bag" && assets.Graph.nodes.Count == 31, "Resources uses the complete restored original Bag graph with every authored node");
        var authored = assets.Graph.nodes.Cast<TipDotGraphNode>().ToArray();
        var blue = (TipDotBagCanUseItemConditionNode)authored.Single(node => node.TipDotNodeId == 10).TipDotNode;
        var catalog = OfflineBagItemQueryCatalog.LoadDefault();
        var oracle = new OfflineBagItemQueries(() => profile.Save, catalog);
        int blueId = catalog.Items.First(row => row.RedDotType == 1 && blue.CheckTypes.Contains((BagItemType)row.ItemType)
            && oracle.CanUseRedDot(row.ClassId, 0, 7) && oracle.GetBlueDotCount(row.ClassId, 0, 7) == 7).ClassId;
        var cooking = new OfflineCookingService(profile, OfficialCookingCatalog.LoadDefault());
        var recipe = catalog.Cookings.First(row => row.ConditionType == "UsedItemCondition" && row.UseSourceOperate == 89
            && cooking.GetRecipe(row.Id)?.formulaItemId == row.ConditionItemId);
        Require(profile.ApplyCharacterMutation(player => {
            player.Inventory.Add(new OfflineItemStack { ItemId = blueId, Count = 7 });
            player.Inventory.Add(new OfflineItemStack { ItemId = recipe.ConditionItemId, Count = 2 });
        }, "bag-tip-bindings:seed").IsApplied, "Seed real isolated quantities and an unlearned original formula");
        home = HomeFeatureRuntime.Begin(slot, root, null); home.TutorialAutoStartEnabled = false;
        profile = home.Profile;
        yield return null; yield return null;
        var services = home.BagTipServices;
        Require(services != null && services.HasFurniture(Zjcs.Official.HomeUI.EFurniture.StorageBox), "Formal Home creates the actual local Bag owner and original default storage ownership");
        var linker = CreateNativeTip();
        yield return null; yield return null; yield return null; yield return null;
        var manager = Field<TipDotNodeManager>(home.PartnerTipDots, "manager");
        TipDotNodeBase Node(int id) => manager.FindNode(authored.Single(node => node.TipDotNodeId == id).GetNodeKey());
        Require(authored.All(node => manager.FindNode(node.GetNodeKey()) != null), "Existing Home graph factory creates all 31 original Bag nodes");
        Require(authored.Select(node => manager.FindNode(node.GetNodeKey())).OfType<IBagTipDotNode>().All(node => ReferenceEquals(node.Services, services)), "Every cloned original condition receives the formal Home data owner");
        Require(Node(10).IsShowTip && Node(10).TipDotInfo.Data == "7" && Node(6).IsShowTip && linker.IsShowTip,
            "Actual selected quantities drive the original blue count, storage aggregate and native visual linker");
        Capture("native-storage-tip-active.png");
        var files = Files(root);
        var records = services.FindItemsByType((BagItemType)catalog.Item(blueId).ItemType).Where(row => row.Info.ClassId == blueId).ToArray();
        Require(records.Length == 1 && services.ShowBagCanUseItemRedDot(records[0]) && services.GetCanUseBlueDotCount(records[0]) == 7
            && Same(files, Files(root)), "Native node queries retain the actual record quantity without changing any save file");
        var detached = JsonUtility.FromJson<OfflineMainlineSave>(JsonUtility.ToJson(profile.Save));
        detached.Player.Inventory.RemoveAll(row => row.ItemId == blueId);
        detached.Player.Inventory.Add(new OfflineItemStack { ItemId = blueId, Count = 2 });
        detached.Player.Inventory.Add(new OfflineItemStack { ItemId = blueId, Count = 5 });
        var recordOwner = new OfflineBagTipDotServices(() => detached, home.Rules, type => home.Session.OwnsFurniture(type), assets);
        var distinctRecords = recordOwner.FindItemsByType((BagItemType)catalog.Item(blueId).ItemType).Where(row => row.Info.ClassId == blueId).ToArray();
        Require(distinctRecords.Length == 2 && distinctRecords.Select(recordOwner.GetCanUseBlueDotCount).SequenceEqual(new long[] { 2, 5 }),
            "Distinct original record quantities remain distinct without inventing or sharing equipment indices");
        recordOwner.Dispose();

        long index = profile.Save.Player.Equipment.NextInstanceId;
        int sword = catalog.Items.First(row => row.ItemType == (int)BagItemType.Sword && row.Quality == 5).ClassId;
        int purifyEvents = 0, equipEvents = 0, splitEvents = 0;
        Action<ulong> purify = _ => purifyEvents++;
        Action<BagEquipPos, ulong, ulong> equip = (position, before, after) => equipEvents++;
        Action<IDictionary<int, long>> split = _ => splitEvents++;
        services.OnEquipAttribuatePurifyStateChange += purify; services.OnEquipUpdate += equip; services.OnSplitItem += split;
        Require(profile.ApplyCharacterMutation(player => {
            player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId = index, ItemId = sword, ItemLevel = 1,
                PurifyProps = new List<OfflineEquipmentPurifyProperty> { new OfflineEquipmentPurifyProperty {
                    Key = 19, Value = new OfflineEquipmentTerm { Type = 1, PropertyId = 10, Factor = .5f } } } });
            player.Equipment.NextInstanceId++;
        }, "bag-tip-bindings:pending").IsApplied, "Commit actual equipment with separate original pending purify state");
        services.Synchronize();
        yield return null; yield return null;
        Require(services.IsEquipInPurify((ulong)index) && !services.IsEquipWorn((ulong)index) && purifyEvents == 1
            && Node(31).IsShowTip == !services.IsFunctionLimit(BagFunctionType.EquipPurify), "Original backpack purify node consumes actual pending state and keeps its original function gate");
        Require(profile.ApplyCharacterMutation(player => player.Equipment.Instances.First(row => row.InstanceId == index).Locked = true,
            "bag-tip-bindings:lock").IsApplied, "Commit an unrelated equipment lock change");
        services.Synchronize();
        Require(purifyEvents == 1 && splitEvents == 0, "The bridge never invents purify or split events for unrelated equipment changes");
        Require(profile.ApplyCharacterMutation(player => {
            var slotState = player.Equipment.Equipped.FirstOrDefault(row => row.Position == (int)BagEquipPos.Weapon);
            if (slotState == null) player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = (int)BagEquipPos.Weapon, InstanceId = index });
            else slotState.InstanceId = index;
        }, "bag-tip-bindings:equip").IsApplied, "Commit the original weapon slot identity");
        services.Synchronize(); yield return null; yield return null;
        Require(services.TryGetEquip(BagEquipPos.Weapon, out var equipped) && equipped.ItemIndex == (ulong)index
            && services.IsEquipWorn((ulong)index) && !Node(31).IsShowTip && equipEvents == 1,
            "Exact equipment slot/index and actual wear event exclude the candidate from backpack tips");
        Require(profile.ApplyCharacterMutation(player => {
            player.Equipment.Instances.First(row => row.InstanceId == index).PurifyProps.Clear();
            player.Equipment.Equipped.RemoveAll(row => row.Position == (int)BagEquipPos.Weapon);
        }, "bag-tip-bindings:clear-pending").IsApplied, "Commit the real pending-state and wear changes");
        services.Synchronize();
        Require(!services.IsEquipInPurify((ulong)index) && !services.IsEquipInPurify(ulong.MaxValue)
            && !services.IsEquipWorn(0) && !services.TryGetEquip(BagEquipPos.Weapon, out _)
            && !services.TryGetEquip((BagEquipPos)int.MaxValue, out _) && purifyEvents == 2 && splitEvents == 0,
            "Equipment queries retain missing/zero index branches and publish only actual changes");
        services.OnEquipAttribuatePurifyStateChange -= purify; services.OnEquipUpdate -= equip; services.OnSplitItem -= split;

        DateTime now = DateTime.Now.Date.AddHours(6).AddMinutes(-1);
        var dayOwner = new OfflineBagTipDotServices(() => profile.Save, home.Rules, type => home.Session.OwnsFurniture(type), assets, () => now.ToUniversalTime());
        int days = 0; var daySub = dayOwner.SubscribeEnterNewDay(() => days++); files = Files(root);
        now = now.AddMinutes(2); dayOwner.Synchronize(); dayOwner.Synchronize();
        now = now.AddDays(-1); dayOwner.Synchronize(); now = now.AddDays(1); dayOwner.Synchronize();
        Require(days == 1 && Same(files, Files(root)), "Shared local 06:00 invalidation fires once and does not repeat on clock rollback or write a save");
        daySub.Dispose(); dayOwner.Dispose(); Require(dayOwner.ReleaseSubscriberCount == 0, "Independent daily subscription releases its owner cleanly");

        OpenStorage(); yield return null; yield return null;
        var inventory = Object.FindObjectOfType<InventoryFeatureRuntime>();
        Require(inventory, "Formal Home storage click still opens the actual Bag page");
        var inventorySession = Field<OfflineMainlineProgressSession>(inventory, "session");
        var inventoryCooking = new OfflineCookingService(inventorySession, OfficialCookingCatalog.LoadDefault());
        Require(inventoryCooking.LearnFormula(recipe.ConditionItemId).IsApplied, "Actual separate Bag session commits the original formula-learning transaction");
        Require(inventorySession.ApplyCharacterMutation(player => player.Inventory.RemoveAll(row => row.ItemId == blueId),
            "bag-tip-bindings:remove-blue").IsApplied, "Persist the removal of the isolated blue-count witness");
        var close = inventory.GetComponentsInChildren<Button>(true).First(button => button.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 && button.gameObject.activeInHierarchy);
        Click(close.gameObject); yield return null; yield return null; yield return null; yield return null;
        Require(!Object.FindObjectOfType<InventoryFeatureRuntime>() && home.Profile.InventoryCount(recipe.ConditionItemId) == 1,
            "Original Bag close pointer reloads the committed transaction into the existing Home session");
        Require(!Node(10).IsShowTip && Node(10).TipDotInfo.Data == "0" && !Node(6).IsShowTip && !linker.IsShowTip,
            "Existing native storage tip responds to learned formula and removed inventory without recreating the Home owner");
        Capture("native-storage-tip-inactive.png");
        Object.Destroy(tipStage); tipStage = null; yield return null; yield return null;
        home.Close(); home = null; yield return null; yield return null;
        Require(services.ReleaseSubscriberCount == 0, "Original graph teardown removes all player, daily, condition and scope subscriptions before owner disposal");

        home = HomeFeatureRuntime.Begin(slot, root, null); home.TutorialAutoStartEnabled = false;
        yield return null; yield return null;
        linker = CreateNativeTip(); yield return null; yield return null; yield return null; yield return null;
        Require(home.Profile.InventoryCount(recipe.ConditionItemId) == 1 && !linker.IsShowTip,
            "Cold Home reopen restores inventory/formula state and the same native storage visual decision");
        Object.Destroy(tipStage); tipStage = null; yield return null; yield return null;
        services = home.BagTipServices; home.Close(); home = null; yield return null; yield return null;
        Require(services.ReleaseSubscriberCount == 0, "Repeated Home lifetime leaves no original Bag graph subscriptions");
    }
    static TipDotNodeLinker CreateNativeTip()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/StorageBox/Native/StorageBox.prefab");
        var source = prefab.GetComponentsInChildren<TipDotNodeLinker>(true).Single(value => value.ConfiguredNodeKey.GraphName == "Bag" && value.ConfiguredNodeKey.NodeName == "6");
        tipStage = new GameObject("NativeStorageTipScope"); tipStage.SetActive(false);
        // The actual native visual subtree is the validation subject. Its
        // enclosing furniture/room controllers are verified in their own scope.
        var clone = Object.Instantiate(source.gameObject, tipStage.transform, true); clone.SetActive(true);
        home.BindBagTipNativeTree(clone); tipStage.SetActive(true);
        return clone.GetComponent<TipDotNodeLinker>();
    }
    static void OpenStorage()
    {
        Require(home.Scene.TryGetFurnitureClickBounds(7, out var bounds), "Formal Home exposes storage input bounds");
        Vector2 point = home.Scene.Camera.WorldToScreenPoint(bounds.center); var input = Field<object>(home, "input");
        foreach (string method in new[] { "PointerDown", "PointerUp" })
            input.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, new object[] { point, false });
    }
    static void Click(GameObject target)
    {
        var rect = (RectTransform)target.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)), "Original close control is the top pointer hit");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    static void Capture(string name)
    {
        var renderers = tipStage.GetComponentsInChildren<Renderer>(true);
        Require(renderers.Length > 0, "Original native tip subtree contains actual renderable geometry");
        Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        var transforms = tipStage.GetComponentsInChildren<Transform>(true); var layers = transforms.Select(value => value.gameObject.layer).ToArray();
        var cameraRoot = new GameObject("NativeBagTipCapture", typeof(Camera)); var camera = cameraRoot.GetComponent<Camera>();
        camera.orthographic = true; camera.aspect = 1;
        camera.orthographicSize = Mathf.Max(.05f, Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.3f);
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 20);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .10f, .12f, 1);
        camera.cullingMask = 1 << 30; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
        var target = new RenderTexture(512, 512, 24); var image = new Texture2D(512, 512, TextureFormat.RGBA32, false); var active = RenderTexture.active;
        try {
            foreach (var transform in transforms) transform.gameObject.layer = 30;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
            var pixels = image.GetPixels32();
            Require(pixels.Count(color => color.r > 220 && color.b > 220 && color.g < 30) < 20,
                "Native source-tip render has no shader-error pixels");
            if (tipStage.GetComponentInChildren<TipDotNodeLinker>(true).IsShowTip)
                Require(pixels.Count(color => color.r > 90 || color.g > 90 || color.b > 100) > 100,
                    "Active native source-tip state renders actual visible pixels");
            File.WriteAllBytes(Path.Combine(Work, name), image.EncodeToPNG());
        } finally {
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            camera.targetTexture = null; RenderTexture.active = active; target.Release(); Object.Destroy(target); Object.Destroy(image); Object.Destroy(cameraRoot);
        }
    }
    static Dictionary<string, byte[]> Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
    static bool Same(Dictionary<string, byte[]> first, Dictionary<string, byte[]> second) => first.Count == second.Count && first.All(row => second.TryGetValue(row.Key, out var data) && row.Value.SequenceEqual(data));
}
