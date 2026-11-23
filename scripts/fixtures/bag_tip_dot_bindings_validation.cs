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
using Zjcs.Official.HomeFurniture;
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
    static readonly HomeStoragePointerValidation pointer = new HomeStoragePointerValidation();
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
            try
            {
                if (!checks.MoveNext())
                {
                    checks = null;
                    Finish(errors == 0 ? null : new Exception($"Unexpected Unity errors: {errors}"));
                }
            }
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
        SessionState.SetBool(Active, false);
        home?.Close(); home = null; Application.logMessageReceived -= OnLog;
        if (error == null) Debug.Log("BAG_TIP_DOT_BINDINGS_PASSED: native default storage construction, actual room slot transform, native collider click, rendered storage and red dot, original day strip, complete Bag graph and queries, entry/close, isolated-save recovery and teardown.");
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
        home = HomeFeatureRuntime.BeginWithNativePointer(slot, root, null, pointer); home.TutorialAutoStartEnabled = false;
        profile = home.Profile;
        yield return null; yield return null;
        var services = home.BagTipServices;
        Require(services != null && services.HasFurniture(Zjcs.Official.HomeUI.EFurniture.StorageBox), "Formal Home creates the actual local Bag owner and original default storage ownership");
        var linker = LocateNativeTip();
        yield return null; yield return null; yield return null; yield return null;
        var manager = Field<TipDotNodeManager>(home.PartnerTipDots, "manager");
        TipDotNodeBase Node(int id) => manager.FindNode(authored.Single(node => node.TipDotNodeId == id).GetNodeKey());
        Require(authored.All(node => manager.FindNode(node.GetNodeKey()) != null), "Existing Home graph factory creates all 31 original Bag nodes");
        Require(authored.Select(node => manager.FindNode(node.GetNodeKey())).OfType<IBagTipDotNode>().All(node => ReferenceEquals(node.Services, services)), "Every cloned original condition receives the formal Home data owner");
        Require(Node(10).IsShowTip && Node(10).TipDotInfo.Data == "7" && Node(6).IsShowTip && linker.IsShowTip,
            "Actual selected quantities drive the original blue count, storage aggregate and native visual linker");
        Capture("native-storage-tip-active.png");
        CheckStorageBuilding();
        var skinChecks = CheckStorageSkinsAndModes();
        while (skinChecks.MoveNext()) yield return skinChecks.Current;
        linker = LocateNativeTip();
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
        home.Close(); home = null; yield return null; yield return null;
        Require(services.ReleaseSubscriberCount == 0, "Original graph teardown removes all player, daily, condition and scope subscriptions before owner disposal");

        home = HomeFeatureRuntime.BeginWithNativePointer(slot, root, null, pointer); home.TutorialAutoStartEnabled = false;
        yield return null; yield return null;
        linker = LocateNativeTip(); yield return null; yield return null; yield return null; yield return null;
        Require(home.Profile.InventoryCount(recipe.ConditionItemId) == 1 && !linker.IsShowTip,
            "Cold Home reopen restores inventory/formula state and the same native storage visual decision");
        services = home.BagTipServices; home.Close(); home = null; yield return null; yield return null;
        Require(services.ReleaseSubscriberCount == 0, "Repeated Home lifetime leaves no original Bag graph subscriptions");
    }
    static TipDotNodeLinker LocateNativeTip()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Official/Home/StorageBox/Native/StorageBox.prefab");
        var live = home.Scene.Root.GetComponentInChildren<StoreBoxFurniture>();
        Require(live && live.gameObject.name == prefab.name + "(Clone)"
            && live.transform.childCount == prefab.transform.childCount
            && live.transform.parent.name == "StorageBoxSlot"
            && live.GetState() == FurnitureBase.EFurnitureState.Functional, "Formal Home mounts the complete original default storage in the authored room slot");
        var linker = live.GetComponentsInChildren<TipDotNodeLinker>(true)
            .Single(value => value.ConfiguredNodeKey.GraphName == "Bag" && value.ConfiguredNodeKey.NodeName == "6");
        tipStage = linker.gameObject;
        return linker;
    }
    static void OpenStorage()
    {
        Require(home.Scene.TryGetFurnitureClickBounds(7, out var bounds), "Formal Home exposes storage input bounds");
        pointer.Click(home);
    }

    static void CheckStorageBuilding()
    {
        var storage = pointer.Center(home);
        var sceneOwner = Field<object>(home, "storage");
        var tick = sceneOwner.GetType().GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
        var pad = storage.transform.Find("Repaired/StorageBox_Light/StorageBox_Light_Pad").GetComponent<SpriteRenderer>();
        tick.Invoke(sceneOwner, new object[] { 8f / 24f });
        Require(pad.gameObject.activeSelf && Mathf.Approximately(pad.color.a, 1f), "Original storage daylight clip enables its light pad at 08:00");
        CaptureStorageHome("native-storage-home-0800");
        tick.Invoke(sceneOwner, new object[] { 18f / 24f });
        Require(!pad.gameObject.activeSelf && Mathf.Approximately(pad.color.a, 0f), "Original storage daylight clip disables its light pad at 18:00");
        CaptureStorageHome("native-storage-home-1800");
        var rack = storage.GetComponentInChildren<HomeWeaponRack>(true);
        Require(rack && rack.PSlot.childCount == 1 && rack.PSlot.GetComponentInChildren<MeshRenderer>() != null,
            "Original rack displays the actual local character primary weapon");
        Require(storage.GetComponent<EntityClickSound>().Sound != null, "Original click sound is bound on the same native controller");
    }

    static IEnumerator CheckStorageSkinsAndModes()
    {
        var rack = home.NativeStorageInstance.GetComponentInChildren<HomeWeaponRack>(true);
        var register = home.NativeStorageInstance.GetComponentInChildren<Zjcs.Official.StoryNodes.StoryNodeRegister>(true);
        var switcher = home.NativeStorageInstance.GetComponentInChildren<HomeSwitcherActionEventHandler>(true);
        Require(rack != null && register != null && register.StoryNodeKeys.Count > 0
            && home.NativeStorageStoryNodes?.GetNode(register.StoryNodeKeys[0]) != null
            && switcher != null && switcher.EventName == "Weapon",
            "Original storage registers story nodes and binds the Weapon action event handler");
        home.PublishHomeActionEvent(new ActionEventData { EventName = "Weapon", BoolParam = false });
        Require(!rack.PSlot.gameObject.activeSelf && rack.SSlot.gameObject.activeSelf,
            "Original storage Weapon action event hides only the primary weapon slot on false BoolParam");
        home.PublishHomeActionEvent(new ActionEventData { EventName = "Weapon", BoolParam = true });
        Require(rack.PSlot.gameObject.activeSelf,
            "Original storage Weapon action event restores the primary weapon slot on true BoolParam");

        var slots = rack.transform.Find("Slots");
        var frame = rack.transform.Find("Rack");
        home.SetHomeViewMode(HomeViewMode.TakePhoto);
        Require(slots != null && frame != null && !slots.gameObject.activeSelf && frame.gameObject.activeSelf,
            "Original storage photo view mode hides weapon slots while retaining the rack");
        home.SetHomeViewMode(HomeViewMode.Owner);
        Require(slots.gameObject.activeSelf,
            "Original storage owner view mode restores weapon slots");

        var skinCases = new (int skinId, string prefabName)[]
        {
            (4220, "StorageBoxChristmas"),
            (4226, "StorageBoxChineseStyle"),
            (4238, "StorageBox1stAnniversary"),
            (4248, "StorageBoxChineseOdyssey"),
            (4261, "StorageBoxHalloween"),
            (4210, "StorageBox"),
        };
        foreach (var (skinId, prefabName) in skinCases)
        {
            Require(home.HomeProgression.EquipSkin(home.Session, 0, (int)Zjcs.Official.HomeUI.EFurniture.StorageBox,
                skinId, _ => 1, "bag-tip-bindings:storage-skin:" + skinId),
                "Equip original storage skin " + skinId);
            home.Scene.ApplyHomeSkins(home.Catalog.HomeSkins, home.Session.Skins, home.Rules);
            home.RefreshNativeFurnitureSkins();
            yield return null; yield return null;
            var live = home.NativeStorageInstance;
            var switchedRack = live ? live.GetComponentInChildren<HomeWeaponRack>(true) : null;
            var switchedRegister = live ? live.GetComponentInChildren<Zjcs.Official.StoryNodes.StoryNodeRegister>(true) : null;
            var linker = live ? live.GetComponentsInChildren<TipDotNodeLinker>(true)
                .SingleOrDefault(value => value.ConfiguredNodeKey.GraphName == "Bag" && value.ConfiguredNodeKey.NodeName == "6") : null;
            Require(live && live.activeInHierarchy && live.name == prefabName + "(Clone)"
                && switchedRack && switchedRack.PSlot.childCount == 1
                && switchedRegister && switchedRegister.StoryNodeKeys.Count > 0
                && home.NativeStorageStoryNodes.GetNode(switchedRegister.StoryNodeKeys[0]) != null
                && linker != null && linker.IsShowTip,
                "Switched native storage skin " + prefabName + " mounts weapon rack, story nodes and active Bag tip");
            if (skinId == 4220) CaptureStorageHome("native-storage-skin-christmas");
        }
    }

    static void CaptureStorageHome(string name)
    {
        int magenta = CaptureHome(name + ".png");
        if (magenta <= 20) return;
        var beds = home.Scene.NativeBedActors.Where(actor => actor && actor.gameObject.activeInHierarchy).ToArray();
        try
        {
            foreach (var bed in beds) bed.gameObject.SetActive(false);
            Require(CaptureHome(name + "-bed-diagnostic.png") <= 20,
                "Isolating the active native bed accounts for the main scene shader error pixels");
        }
        finally
        {
            foreach (var bed in beds) bed.gameObject.SetActive(true);
        }
    }

    static int CaptureHome(string name)
    {
        var camera = home.Scene.Camera;
        var target = new RenderTexture(720, 1280, 24);
        var image = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
        var priorTarget = camera.targetTexture;
        var priorActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(Work, name), image.EncodeToPNG());
            int magenta = image.GetPixels32().Count(pixel => pixel.r > 230 && pixel.b > 230 && pixel.g < 25);
            Debug.Log(name + " magenta pixels: " + magenta);
            return magenta;
        }
        finally
        {
            camera.targetTexture = priorTarget;
            RenderTexture.active = priorActive;
            target.Release();
            Object.Destroy(target); Object.Destroy(image);
        }
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
