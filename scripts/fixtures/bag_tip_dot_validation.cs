using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.BagTipDots;
using Zjcs.Official.HomeUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using ItemType = Zjcs.Official.BagTipDots.ItemType;
using EquipPos = Zjcs.Official.BagTipDots.EquipPos;
using FunctionType = Zjcs.Official.BagTipDots.FunctionType;
using RedDotType = Zjcs.Official.BagTipDots.RedDotType;
using SourceOperate = Zjcs.Official.BagTipDots.SourceOperate;
using TipDotFurnitureCanRepairConditionNode = Zjcs.Official.BagTipDots.TipDotFurnitureCanRepairConditionNode;
using TipDotHasFurnitureConditionNode = Zjcs.Official.BagTipDots.TipDotHasFurnitureConditionNode;

[InitializeOnLoad]
public static class BagTipDotValidation
{
    const string Active = "Zjcs.BagTipDotValidation.Active";
    static BagTipDotValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("OriginalBagTipChecks").AddComponent<BagTipDotChecks>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Bag tip graph validation timed out"));
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_BAG_TIP_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Use the explicitly selected independent project");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ValidateNativeStorageAsset();
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 180);
        EditorApplication.EnterPlaymode();
    }
    static void ValidateNativeStorageAsset()
    {
        const string path = "Assets/Official/Home/StorageBox/Native/StorageBox.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab) throw new Exception("Original storage prefab is unavailable");
        var stage = new GameObject("StorageAssetRender"); stage.SetActive(false);
        var instance = UnityEngine.Object.Instantiate(prefab, stage.transform, false);
        var components = instance.GetComponentsInChildren<Component>(true);
        if (components.Any(value => !value) || instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 40)
            throw new Exception("Every original storage component must bind to its actual migrated type");
        var typed = instance.GetComponentsInChildren<MonoBehaviour>(true).Select(value => value.GetType().FullName).Distinct().ToArray();
        if (typed.Length != 21) throw new Exception("Original storage component-type coverage differs");
        var sound = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(value => value.GetType().FullName == "Zjcs.Official.HomeFurniture.EntityClickSound");
        var clip = sound.GetType().GetField("Sound").GetValue(sound) as AudioClip;
        if (!clip || clip.channels != 2 || clip.frequency != 48000 || clip.samples != 7200)
            throw new Exception("Original storage click event must bind its verified decoded audio clip");
        stage.SetActive(true);
        var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(value => value.enabled && value.gameObject.activeInHierarchy).ToArray();
        foreach (var renderer in renderers)
            foreach (var material in renderer.sharedMaterials)
                if (!material || !material.shader || !material.shader.isSupported)
                    throw new Exception("Unsupported native storage material: " + renderer.name);
        var sprites = renderers.OfType<SpriteRenderer>().Where(value => value.sprite).ToArray();
        if (sprites.Length == 0) throw new Exception("Native storage has no visible sprites");
        Bounds bounds = sprites[0].bounds;
        foreach (var sprite in sprites.Skip(1)) bounds.Encapsulate(sprite.bounds);
        var cameraRoot = new GameObject("StorageAssetCamera", typeof(Camera));
        var camera = cameraRoot.GetComponent<Camera>();
        camera.orthographic = true; camera.aspect = 720f / 1280f;
        camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / camera.aspect) * 1.12f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z - 20);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .11f, .15f, 1);
        camera.nearClipPlane = .01f; camera.farClipPlane = 100;
        var target = new RenderTexture(720, 1280, 24); camera.targetTexture = target;
        var image = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
        var prior = RenderTexture.active;
        try {
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0); image.Apply();
            var pixels = image.GetPixels32();
            if (pixels.Count(value => value.r > 220 && value.b > 220 && value.g < 30) > pixels.Length / 1000)
                throw new Exception("Native storage asset render contains shader-error pixels");
            if (pixels.Count(value => value.r > 60 || value.g > 60 || value.b > 70) < 1000)
                throw new Exception("Native storage asset render is empty");
            File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_BAG_TIP_OUTPUT"), "native-storage-asset.png"), image.EncodeToPNG());
        } finally {
            camera.targetTexture = null; RenderTexture.active = prior;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(cameraRoot);
        }
        Debug.Log("HOME_STORAGE_NATIVE_ASSET_PASSED: original hierarchy/component bindings and actual asset render. Editor asset scope; native home-host gameplay remains separate.");
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("BAG_TIP_DOTS_PASSED: original complete Bag graph, six condition families, player notifications, scope lifetime and teardown. Component scope only.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class BagTipDotChecks : MonoBehaviour
{
    TipDotNodeManager manager;
    int errors;
    IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        var stack = new Stack<IEnumerator>(); stack.Push(Check());
        Exception failure = null;
        while (stack.Count != 0)
        {
            object current;
            try {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                current = stack.Peek().Current;
                if (current is IEnumerator child) { stack.Push(child); continue; }
            } catch (Exception error) { failure = error; break; }
            yield return current;
        }
        manager?.Dispose(); manager = null;
        yield return null;
        Application.logMessageReceived -= OnLog;
        BagTipDotValidation.Finish(failure ?? (errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors)));
    }
    static void Require(bool value, string text) { if (!value) throw new Exception(text); Debug.Log("CHECK: " + text); }
    void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    static IEnumerator Frames() { yield return null; yield return null; yield return null; yield return null; }

    IEnumerator Check()
    {
        const string root = "Assets/Official/Home/StorageBox/Native";
        var graph = AssetDatabase.FindAssets("t:TipDotNodeGraph", new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TipDotNodeGraph>).Single();
        Require(graph.name == "Bag" && graph.nodes.Count == 31, "Actual native Bag graph and all authored nodes imported");
        var authored = graph.nodes.Cast<TipDotGraphNode>().ToArray();
        Require(authored.All(node => node && node.TipDotNode != null && node.graph == graph), "All managed node types and original graph references deserialize");
        Require(authored.Select(node => node.TipDotNode.GetType()).Distinct().Count() == 7, "Six original condition families and common aggregate nodes");
        var services = new Services();
        services.SettingsValue.ShowCanUseRedDotItemTypes.AddRange(authored.Select(node => node.TipDotNode)
            .OfType<TipDotBagCanUseItemConditionNode>().SelectMany(node => node.CheckTypes).Distinct());
        services.SettingsValue.EquipTypes.Add((ItemType)200);
        Action<object> scopeAdd = null; Action scopeRemove = null;
        var definitions = new[] { new PartnerTipDotGraphInfo { GraphName = graph.name, GraphType = graph.Type, GraphScope = graph.Scope } };
        var bindings = new PartnerTipDotRuntimeBindings(() => true, () => definitions,
            name => name == graph.name ? graph.Type : (TipDotGraphType?)null,
            (add, remove) => { scopeAdd += add; scopeRemove += remove; return new Subscription(() => { scopeAdd -= add; scopeRemove -= remove; }); },
            action => new Subscription(() => {}));
        var graphs = new TipDotGraphHandler(name => name == graph.name ? graph : null);
        var factory = new TipDotNodeFactory(graphs.GetNodeGraph, node => {
            node.NodeManager = manager;
            if (node is IBagTipDotNode bag) bag.Services = services;
        });
        manager = new TipDotNodeManager(new TipDotNodeDataRepository(), factory, graphs, bindings);
        manager.Init();
        Require(manager.CreateGraphNode(graph.name, BaseNodeInstanceInfo.kDefault) == 31, "Original factory clones the complete serialized graph");
        var controller = new TipDotSystemController(manager);
        TipDotNodeKey Key(int id) => authored.Single(node => node.TipDotNodeId == id).GetNodeKey();
        TipDotNodeBase Node(int id) => manager.FindNode(Key(id));
        Action<TipDotStateInfo> observe = _ => {};
        Require(controller.LinkNodeStateChange(Key(6), observe), "Original unlocked-storage aggregate activates through its actual graph key");
        Require(controller.LinkNodeStateChange(Key(2), observe), "Original storage repair aggregate activates through its actual graph key");
        yield return Frames();
        Require(!Node(6).IsShowTip && !Node(2).IsShowTip, "Owned empty storage has no use-item or repair tip");

        var blue = (TipDotBagCanUseItemConditionNode)Node(10);
        var item = services.PlayerValue.Add(1, blue.CheckTypes[0]);
        services.PlayerValue.Usable.Add(item.ItemIndex);
        services.PlayerValue.Dots[item.ItemIndex] = RedDotType.BlueDot;
        services.PlayerValue.BlueCounts[item.ItemIndex] = 7;
        services.PlayerValue.Item(item, new BagTipAward());
        yield return Frames();
        Require(blue.IsShowTip && blue.TipDotInfo.Data == "7" && Node(6).IsShowTip, "Original blue-dot quantity propagates into the actual storage aggregate");
        services.PlayerValue.Dots[item.ItemIndex] = RedDotType.RedDot;
        services.PlayerValue.Item(item, new BagTipAward()); yield return Frames();
        Require(!blue.IsShowTip && Node(9).IsShowTip, "Red-dot and blue-dot branches remain distinct");
        var clone = (TipDotBagCanUseItemConditionNode)blue.Clone(); clone.CheckTypes.Clear();
        Require(blue.CheckTypes.Count > 0 && clone.Type == blue.Type && clone.ShowNum == blue.ShowNum, "Original clone copies settings without sharing mutable type lists");
        services.PlayerValue.Usable.Clear(); services.SettingsValue.NewDayRefreshRedDotItemType.Add(item.Info.ItemType);
        services.NewDay(); yield return Frames();
        Require(!Node(9).IsShowTip && !Node(6).IsShowTip, "New-day subscription refreshes present item types without an inventory mutation");
        Require(!TipDotBagUtils.IsExcludedFromShowCanUseRedDot(services.SettingsValue, (ItemType)200) &&
            TipDotBagUtils.IsExcludedFromShowCanUseRedDot(services.SettingsValue, (ItemType)int.MaxValue), "Original equipment inclusion and excluded-item filter");

        services.ActiveGems = services.Gems; scopeAdd(services.Gems);
        Require(services.Gems.Listeners.Count == 1, "Gem condition subscribes to the current scope collector");
        services.PlayerValue.RecycleGems.Add(item); services.PlayerValue.Limited.Add(FunctionType.RecycleGem);
        services.PlayerValue.Rank(); yield return Frames(); Require(!Node(5).IsShowTip, "Gem recycle function gate precedes available gems");
        services.PlayerValue.Limited.Clear(); services.Gems.Refresh(); yield return Frames();
        Require(Node(5).IsShowTip, "Scoped gem notification dirties the original recycle condition");
        scopeRemove(); services.ActiveGems = null;
        Require(services.Gems.Listeners.Count == 0, "Scope removal unsubscribes the same gem collector");
        services.PlayerValue.RecycleGems.Clear(); services.PlayerValue.Rank(); yield return Frames();
        Require(!Node(5).IsShowTip, "Subrank event refreshes gem recycling after collector removal");

        services.PlayerValue.Doorkeys.Add(item);
        services.PlayerValue.Item(item, new BagTipAward { SourceOperate = SourceOperate.GiftRecycle });
        yield return Frames(); Require(Node(29).IsShowTip && services.PlayerValue.Gifts.Count == 0, "Door-key recycling participates without mutating the owner's gift collection");
        services.PlayerValue.Doorkeys.Clear(); services.PlayerValue.Friendship(); yield return Frames();
        Require(!Node(29).IsShowTip, "Friendship event refreshes original gift and door-key recycling");

        var equip = services.PlayerValue.Add(2, (ItemType)200);
        services.PlayerValue.Purifying.Add(equip.ItemIndex); services.PlayerValue.Equip(); yield return Frames();
        Require(Node(31).IsShowTip, "Backpack purify condition includes unworn equipment in purify");
        services.PlayerValue.Worn.Add(equip.ItemIndex); services.PlayerValue.Purify(equip.ItemIndex); yield return Frames();
        Require(!Node(31).IsShowTip, "Worn equipment is excluded from backpack purify tips");
        services.PlayerValue.Worn.Clear(); services.PlayerValue.Limited.Add(FunctionType.EquipPurify);
        services.PlayerValue.Split(); yield return Frames(); Require(!Node(31).IsShowTip, "Equipment purify function gate remains authoritative");
        services.PlayerValue.Limited.Clear(); services.PlayerValue.Purifying.Clear(); services.PlayerValue.Split(); yield return Frames();
        var slot = new TipDotEquipPurifyCondtionNode { Services = services, NodeManager = manager,
            NodeKey = new TipDotNodeKey("SlotFixture", "one", new EquipSlotNodeInstanceInfo(EquipPos.Helmet)), EquipPosList = new List<EquipPos>() };
        slot.Init(); Require(slot.EquipPosList.SequenceEqual(new[] { EquipPos.Helmet }), "Original slot instance overrides the configured equipment positions");
        services.PlayerValue.Equipped[EquipPos.Helmet] = equip; services.PlayerValue.Purifying.Add(equip.ItemIndex);
        Require(slot.CheckShowTip(), "Worn-slot purify query uses the exact selected slot");
        slot.Release(); services.PlayerValue.Purifying.Clear();

        services.PlayerValue.Owned = false; services.Unlock.Met = false; services.PlayerValue.Repair(EFurniture.StorageBox); yield return Frames();
        var firstRepair = new TipDotFurnitureCanRepairConditionNode { Services = services, NodeManager = manager, mFurniture = EFurniture.StorageBox };
        var firstOwned = new TipDotHasFurnitureConditionNode { Services = services, NodeManager = manager, mFurniture = EFurniture.StorageBox };
        firstRepair.Init(); firstOwned.Init();
        Require(services.ConditionSubscriptions == 2, "Unowned furniture nodes subscribe to their original unlock condition");
        services.PlayerValue.Repair(EFurniture.Cart);
        Require(services.ConditionSubscriptions == 2, "Another building's repair preserves storage condition subscriptions");
        services.PlayerValue.Repair(EFurniture.StorageBox);
        Require(services.ConditionSubscriptions == 0, "Matching repair disposes both unlock registrations exactly once");
        firstRepair.Release(); firstOwned.Release();
        var repair = (TipDotFurnitureCanRepairConditionNode)Node(4);
        Require(!repair.CheckShowTip() && !Node(27).IsShowTip, "Unowned furniture checks its original unlock condition");
        services.Unlock.Met = true; services.PlayerValue.Funds = false;
        Require(!repair.CheckShowTip(), "Furniture repair tip requires the configured cost");
        services.PlayerValue.Funds = true; Require(repair.CheckShowTip(), "Furniture repair tip allows an unlocked affordable unowned building");
        services.PlayerValue.Owned = true; services.PlayerValue.Repair(EFurniture.StorageBox); yield return Frames();
        Require(!repair.CheckShowTip() && Node(27).IsShowTip, "Matching repair event switches ownership and repair branches");
        controller.UnlinkNodeStateChange(Key(6), observe); controller.UnlinkNodeStateChange(Key(2), observe);
        manager.Dispose(); manager = null; yield return null;
        Require(services.PlayerValue.SubscriberCount == 0 && services.DaySubscribers == 0 && services.ConditionSubscriptions == 0 &&
            services.Gems.Listeners.Count == 0 && scopeAdd == null && scopeRemove == null, "Full graph release removes player, condition, new-day and scope subscriptions");
    }

    sealed class Subscription : IDisposable
    {
        Action action; public Subscription(Action value) { action = value; }
        public void Dispose() { var value = action; action = null; value?.Invoke(); }
    }
    sealed class Condition : IBagTipCondition { public bool Met = true; public bool IsMet(IBagTipDotPlayer player) => Met; }
    sealed class Settings : IBagTipDotSettings
    {
        public List<ItemType> EquipTypes { get; } = new List<ItemType>();
        public List<ItemType> ShowCanUseRedDotItemTypes { get; } = new List<ItemType>();
        public List<ItemType> NewDayRefreshRedDotItemType { get; } = new List<ItemType>();
    }
    sealed class Gems : IBagGemMergeManager
    {
        public readonly HashSet<IBagGemSubscriber> Listeners = new HashSet<IBagGemSubscriber>();
        public void Subscribe(IBagGemSubscriber value) { Require(Listeners.Add(value), "Gem collector subscription is not duplicated"); }
        public void Unsubscribe(IBagGemSubscriber value) => Listeners.Remove(value);
        public void Refresh() { foreach (var value in Listeners.ToArray()) value.Refresh(); }
    }
    sealed class Services : IBagTipDotServices
    {
        public readonly Player PlayerValue = new Player(); public IBagTipDotPlayer Player => PlayerValue;
        public readonly Settings SettingsValue = new Settings(); public IBagTipDotSettings Settings => SettingsValue;
        public readonly Gems Gems = new Gems(); public IBagGemMergeManager ActiveGems;
        public IBagGemMergeManager GemMergeManager => ActiveGems;
        public readonly Condition Unlock = new Condition();
        Action day; public int DaySubscribers => day?.GetInvocationList().Length ?? 0;
        public int ConditionSubscriptions;
        public IReadOnlyCollection<int> ItemTagKeys => new[] { 1 };
        public BagTipFurnitureInfo GetFurnitureInfo(EFurniture type) => new BagTipFurnitureInfo { UnlockCondition = Unlock, RepairCost = new Dictionary<int, long> { [1] = 4 } };
        public IDisposable SubscribeCondition(IBagTipCondition condition, Action action) { ConditionSubscriptions++; return new Subscription(() => ConditionSubscriptions--); }
        public IDisposable SubscribeEnterNewDay(Action action) { day += action; return new Subscription(() => day -= action); }
        public void NewDay() => day?.Invoke();
        public bool IsFunctionLimit(IBagTipDotPlayer player, FunctionType type) => player.IsFunctionLimit(type);
    }
    sealed class Player : IBagTipDotPlayer
    {
        public bool Owned = true, Funds = true;
        public readonly List<BagTipItem> Items = new List<BagTipItem>(), RecycleGems = new List<BagTipItem>(), Gifts = new List<BagTipItem>(), Doorkeys = new List<BagTipItem>();
        public readonly HashSet<ulong> Usable = new HashSet<ulong>(), Purifying = new HashSet<ulong>(), Worn = new HashSet<ulong>();
        public readonly HashSet<FunctionType> Limited = new HashSet<FunctionType>();
        public readonly Dictionary<ulong, RedDotType> Dots = new Dictionary<ulong, RedDotType>();
        public readonly Dictionary<ulong, long> BlueCounts = new Dictionary<ulong, long>();
        public readonly Dictionary<EquipPos, BagTipItem> Equipped = new Dictionary<EquipPos, BagTipItem>();
        public event Action<BagTipItem, long, BagTipAward> OnItemCountUpdate;
        public event Action<int, int, int> OnNpcFriendshipLevelUpdate;
        public event Action<int> OnSubRankUpdate;
        public event Action<EFurniture> OnRepairFurniture;
        public event Action<EquipPos, ulong, ulong> OnEquipUpdate;
        public event Action<IDictionary<int, long>> OnSplitItem;
        public event Action<ulong> OnEquipAttribuatePurifyStateChange;
        public int SubscriberCount => Count(OnItemCountUpdate) + Count(OnNpcFriendshipLevelUpdate) + Count(OnSubRankUpdate) + Count(OnRepairFurniture) + Count(OnEquipUpdate) + Count(OnSplitItem) + Count(OnEquipAttribuatePurifyStateChange);
        static int Count(Delegate value) => value?.GetInvocationList().Length ?? 0;
        public BagTipItem Add(ulong index, ItemType type) { var item = new BagTipItem { Info = new BagTipItemInfo { ClassId = (int)index, ItemType = type }, ItemIndex = index }; Items.Add(item); return item; }
        public void Item(BagTipItem item, BagTipAward source) => OnItemCountUpdate?.Invoke(item, 0, source);
        public void Friendship() => OnNpcFriendshipLevelUpdate?.Invoke(1, 0, 1);
        public void Rank() => OnSubRankUpdate?.Invoke(1);
        public void Repair(EFurniture type) => OnRepairFurniture?.Invoke(type);
        public void Equip() => OnEquipUpdate?.Invoke(EquipPos.Weapon, 0, 2);
        public void Split() => OnSplitItem?.Invoke(new Dictionary<int, long>());
        public void Purify(ulong index) => OnEquipAttribuatePurifyStateChange?.Invoke(index);
        public IReadOnlyCollection<BagTipItem> FindItemsByType(ItemType type) => Items.Where(item => item.Info.ItemType == type).ToArray();
        public IReadOnlyCollection<BagTipItem> FindItemsByType(List<ItemType> types) => Items.Where(item => types.Contains(item.Info.ItemType)).ToArray();
        public bool ShowBagCanUseItemRedDot(BagTipItem item) => Usable.Contains(item.ItemIndex);
        public RedDotType GetItemRedDotType(BagTipItem item) => Dots.TryGetValue(item.ItemIndex, out var value) ? value : RedDotType.None;
        public long GetCanUseBlueDotCount(BagTipItem item) => BlueCounts.TryGetValue(item.ItemIndex, out var value) ? value : 0;
        public IReadOnlyCollection<BagTipItem> GetCanRecycleGem() => RecycleGems;
        public List<BagTipItem> GetCanRecycleGift() => new List<BagTipItem>(Gifts);
        public List<BagTipItem> GetCanRecycleDoorkey() => new List<BagTipItem>(Doorkeys);
        public bool HasFurniture(EFurniture type) => Owned;
        public bool HasItems(IReadOnlyDictionary<int, long> cost) => Funds;
        public bool IsFunctionLimit(FunctionType type) => Limited.Contains(type);
        public bool IsEquipWorn(ulong index) => Worn.Contains(index);
        public bool IsEquipInPurify(ulong index) => Purifying.Contains(index);
        public bool TryGetEquip(EquipPos pos, out BagTipItem item) => Equipped.TryGetValue(pos, out item);
    }
}
