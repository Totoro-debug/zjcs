// Native prefab integration probe. Popup data/host endpoints remain isolated.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

public sealed class PartnerGiftPreferenceProbe : IProfessionItemIconContext, IProfessionItemColors, IProfessionItemPlayer, IDisposable
{
    [Serializable] private sealed class Reference { public string guid; }
    [Serializable] private sealed class ImageRow { public int item_id; public string address; public Reference pointer, manifest_asset; }
    [Serializable] private sealed class Root { public string name; public Reference pointer; }
    [Serializable] private sealed class Preference { public int id; public double multiplier; }
    [Serializable] private sealed class Item : IProfessionItemInfo
    {
        public int ClassId, Quality, ItemType; public string RawIcon, IconEffect; public bool CanOverlap;
        int IProfessionItemInfo.ClassId => ClassId;
        string IProfessionItemInfo.Icon => RawIcon;
        string IProfessionItemInfo.IconEffect => IconEffect;
        Zjcs.Official.ProfessionUI.Quality IProfessionItemInfo.Quality => (Zjcs.Official.ProfessionUI.Quality)Quality;
        bool IProfessionItemInfo.CanOverlap => CanOverlap;
        public object GetFakeIcon() => throw Unexpected();
    }
    [Serializable] private sealed class Fixture { public Item[] items; public ImageRow[] images; public Root[] roots; public Reference config, attachment; public Preference[] preferences; public string countFormat; }
    private readonly Fixture fixture;
    private readonly Dictionary<int, Item> items;
    private readonly Dictionary<int, Sprite> sprites;
    private readonly OfflineProfessionImageResourceLoader loader;
    private readonly ItemIconAttachmentManager attachments;
    private readonly List<ItemIcon> borrowed = new List<ItemIcon>();
    private readonly Stack<ItemIcon> returned = new Stack<ItemIcon>();
    private readonly Transform pool;
    private int subscribers, popupCalls;
    public IReadOnlyDictionary<int, double> Preferences { get; }
    public string CountFormat => fixture.countFormat;
    public PartnerGiftPreferenceProbe()
    {
        fixture = JsonUtility.FromJson<Fixture>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Editor/gift-preferences-fixture.json").text);
        items = fixture.items.ToDictionary(row => row.ClassId);
        sprites = fixture.images.ToDictionary(row => row.item_id, row => Load<Sprite>(row.pointer));
        Require(items.Count == 107 && sprites.Count == 107, "all available preference items");
        Preferences = fixture.preferences.ToDictionary(row => row.id, row => row.multiplier);
        var catalog = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
        var entries = fixture.images.Select(row => new OfflineProfessionImageCatalog.Entry {
            Path = "Assets/Image/" + row.address, AssetGuid = row.manifest_asset.guid, Asset = sprites[row.item_id] }).ToList();
        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = "ReduceDot", AssetGuid = "a1a733aad02670d46814b30ac75aabc8", Asset = Prefab("ReduceDot") });
        var hint = fixture.roots.Single(row => row.name != "ItemIconDefault" && row.name != "ItemIconNpcGift" && row.name != "ReduceDot");
        entries.Add(new OfflineProfessionImageCatalog.Entry { Path = hint.name, AssetGuid = "d019ea2d9f143514ebc8c30f545dca16", Asset = Prefab(hint.name) });
        catalog.Entries = entries.ToArray(); loader = new OfflineProfessionImageResourceLoader(catalog);
        ProfessionImageResourceBridge.Provider = image => loader;
        attachments = new ItemIconAttachmentManager(loader, () => Load<ItemIconAttachmentConfig>(fixture.attachment), new GAction());
        Config = Load<ItemIconConfig>(fixture.config);
        Require(Config.QualityBgImages.Count == 8 && Config.QualityBgImages.All(sprite => sprite != null), "original quality backgrounds");
        var root = new GameObject("IsolatedGiftIconPool", typeof(RectTransform)); root.SetActive(false); pool = root.transform;
    }
    private static T Load<T>(Reference reference) where T : Object
    { var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(reference.guid)); Require(asset != null, "explicit source asset " + reference.guid); return asset; }
    private GameObject Prefab(string name)
    {
        var value = Load<GameObject>(fixture.roots.Single(row => row.name == name).pointer);
        Require(value.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "native component bindings " + name);
        return value;
    }
    private ItemIcon Create(string name, Transform slot)
    {
        var go = Object.Instantiate(Prefab(name), slot, false);
        var icon = go.GetComponent<ItemIcon>(); icon.Bind(this);
        foreach (var cell in go.GetComponentsInChildren<ItemIconAttachment>(true)) cell.Bind(attachments);
        icon.Clear(); return icon;
    }
    public ItemIcon Borrow(int id, Transform slot)
    {
        var icon = returned.Count > 0 ? returned.Pop() : Create("ItemIconDefault", pool);
        icon.transform.SetParent(slot, false); icon.Init(items[id]); icon.gameObject.SetActive(true);
        borrowed.Add(icon); return icon;
    }
    public void Recycle(ItemIcon icon)
    { Require(borrowed.Remove(icon), "one recycle per borrowed preference"); icon.Clear(); icon.gameObject.SetActive(false); icon.transform.SetParent(pool, false); returned.Push(icon); }
    // Source ItemComparerBase.RewardItemComparer: designated exp item, quality
    // descending, class ID ascending. All selected gift types are non-exp items.
    public int Compare(int a, int b) { int quality = items[b].Quality.CompareTo(items[a].Quality); return quality == 0 ? a.CompareTo(b) : quality; }
    public void AssertPopulated()
    {
        var expected = Preferences.Keys.OrderBy(id => id, Comparer<int>.Create(Compare)).ToArray();
        Require(borrowed.Select(icon => icon.myInfo.ClassId).SequenceEqual(expected), "native rule icon order");
        Require(borrowed.All(icon => icon.IconSprite == sprites[icon.myInfo.ClassId] && icon.Button.enabled), "native sprite/button binding");
    }
    public void ClickFirst(EventSystem events, Camera camera)
    { Click(borrowed[0].gameObject, events, camera); Require(popupCalls == 1, "native item popup arguments dispatched once"); }
    public void AssertRecycled() { Require(borrowed.Count == 0 && returned.Count == 7 && subscribers == 0, "window disable recycles icons and player subscriptions"); }
    public IEnumerator VerifyAll(Transform canvas, EventSystem events, Camera camera, Action capture)
    {
        var slot = new GameObject("IsolatedGiftProbeSlot", typeof(RectTransform)); slot.transform.SetParent(canvas, false);
        var icon = (ItemIconNpcGift)Create("ItemIconNpcGift", slot.transform);
        var font = WorldMultiLanguageText.LanguageFont();
        foreach (var label in icon.GetComponentsInChildren<TMP_Text>(true)) label.font = font;
        foreach (var item in items.Values.OrderBy(row => row.ClassId))
        {
            icon.Init(item); icon.SetCount(3); icon.SetClickPopupInfoDialog();
            yield return null;
            Require(icon.IconSprite == sprites[item.ClassId] && icon.QualityImg.sprite == Config.QualityBgImages[item.Quality], "native preference rendering " + item.ClassId);
            icon.Clear();
        }
        icon.Init(items[1130]); icon.SetCount(3); icon.SetPreferenceGoActive(true); icon.SetSelectedGoActive(true);
        icon.SetAttachment(ItemIconAttachmentType.ReduceTip, true);
        int reductions = 0; icon.SetReduceBtnClickListener(value => { Require(value == icon, "reduce passes original owner"); reductions++; icon.SetCount(2); });
        yield return null;
        var reduce = icon.GetAttachment(ItemIconAttachmentType.ReduceTip);
        Require(reduce != null && reduce.GetComponent<Button>() != null, "original manager resolves ReduceDot");
        capture(); Click(reduce.gameObject, events, camera);
        Require(reductions == 1 && icon.myText.Text == "2", "simulated mouse reaches original ReduceTip listener");
        int identity = reduce.GetInstanceID(); icon.Clear(); icon.Init(items[1130]);
        icon.SetAttachment(ItemIconAttachmentType.ReduceTip, true); icon.SetReduceBtnClickListener(value => reductions++);
        yield return null;
        Require(icon.GetAttachment(ItemIconAttachmentType.ReduceTip).GetInstanceID() == identity, "original attachment pool reuse");
        Click(icon.GetAttachment(ItemIconAttachmentType.ReduceTip).gameObject, events, camera);
        Require(reductions == 2, "reused reduce button has exactly one listener");
        icon.Clear(); Require(subscribers == 0, "all gift-count subscriptions released");
        Object.Destroy(slot); yield return null;
    }
    private static void Click(GameObject go, EventSystem events, Camera camera)
    {
        Canvas.ForceUpdateCanvases(); var rect = (RectTransform)go.transform;
        var pointer = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == go, "native click is unobscured: " + go.name);
        ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(go, pointer, ExecuteEvents.pointerClickHandler);
    }
    public ItemIconConfig Config { get; }
    public Object GrayConfig => throw Unexpected();
    public IProfessionItemColors Colors => this;
    public IProfessionItemPlayer Player => this;
    public IProfessionItemInfo GetItemInfo(int id) => items[id];
    public object Compact(long count) => count.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public object CultureText(string key) => throw Unexpected();
    public string GetItemNameKey(int id) => throw Unexpected();
    public bool TryGetSourceData(int id, out IReadOnlyList<IProfessionItemSource> sources) => throw Unexpected();
    public bool CanMerge(int id, bool recursion) => throw Unexpected();
    public object GetLocationInfos(int id) => throw Unexpected();
    public void SetUIGray(Object config, GameObject target, bool gray) => throw Unexpected();
    public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) => throw Unexpected();
    public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All) => throw Unexpected();
    public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw Unexpected();
    public void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long required, Action<GameObject> callback) => throw Unexpected();
    public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback)
    { Require(owner is ItemIcon && required == 0 && items[info.ClassId] == info, "original popup boundary arguments"); popupCalls++; }
    public Color Green => throw Unexpected(); public Color White => throw Unexpected(); public Color LightRed => throw Unexpected();
    public Color GetTextColor(ThemeColorType theme, ColorType type) => Color.white; // Fixture palette endpoint only.
    public long GetItemAmount(int id) => 3;
    public event Action<IProfessionItemData, long, object> OnItemCountUpdate { add { subscribers++; } remove { subscribers--; } }
    public bool CanGetItemFromMapLocation(object location) => throw Unexpected();
    public void Dispose() { attachments.Dispose(); Object.Destroy(pool.gameObject); }
    private static Exception Unexpected() => new InvalidOperationException("Gift prefab probe entered an unbound endpoint");
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Native gift preferences: " + message); }
}
