using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;

namespace Zjcs.Presentation.Home
{
    // Reads the already committed grant. Opening an award or its details never
    // grants inventory again. Native controllers own all layout and animation.
    public sealed class OfflineDemonResultItemBindings : IProfessionItemIconContext,
        IProfessionItemColors, IProfessionItemPlayer, IOfficialBattleAwardListHost,
        IBattleAwardIconHost, IItemDetailBindings, IDisposable
    {
        [Serializable] private sealed class TextRow { public string key, text; }
        [Serializable] private sealed class ImageRow { public string path, guid; }
        [Serializable] private sealed class AttachmentRow { public int type; public string guid; }
        [Serializable] private sealed class TextColor { public Color Color; public int EnableGradient; }
        [Serializable] private sealed class ThemeRow { public int ThemeColorType; public int[] TypeList; public TextColor[] TextColors; }
        [Serializable] private sealed class ThemeColors { public ThemeRow[] TextColorLists; }
        [Serializable] private sealed class Palette
        { public Color Green, White; public ThemeColors TextColorConfig; }
        [Serializable] private sealed class DetailData
        {
            public int item_id;
            public string type_key, name_key, description_key, function_description_key;
            public bool show_player_item_count, is_count_limit, show_use_detail, has_output_addition_group;
            public TextColor quality_text_color;
            public SourceAnalysis source_analysis;
        }
        [Serializable] private sealed class SourceAnalysis { public bool has_sources; }
        [Serializable] private sealed class Data
        { public TextRow[] texts; public ImageRow[] images; public AttachmentRow[] attachments; public Palette colors; public DetailData item_detail; }
        private sealed class ItemData : IProfessionItemData
        {
            public int Id => Info.ClassId;
            public long Count { get; }
            public IProfessionItemInfo Info { get; }
            public ItemData(IProfessionItemInfo info, long count) { Info = info; Count = count; }
            public Quality GetQuality() => Info.Quality;
            public bool CanExpire() => false;
        }
        private readonly HomeFeatureRuntime home;
        private readonly OfficialDemonResultAssets assets;
        private readonly Data data;
        private readonly Dictionary<string, string> texts;
        private readonly ItemDetailDefinition definition;
        private readonly OfflineProfessionImageCatalog images;
        private readonly ItemIconAttachmentManager attachments;
        private readonly Func<GameObject, Transform, GameObject> instantiate;
        private readonly Transform popupParent;
        private readonly Action<GameObject> showPopup, closePopup;
        private long previousCount;
        private bool disposed;
        public IProfessionImageResourceLoader Resources { get; }
        public OfflineDemonResultItemBindings(HomeFeatureRuntime home, OfficialDemonResultAssets assets,
            OfflineProfessionImageCatalog sharedImages, Func<GameObject, Transform, GameObject> instantiate,
            Transform popupParent, Action<GameObject> showPopup, Action<GameObject> closePopup)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.instantiate = instantiate ?? throw new ArgumentNullException(nameof(instantiate));
            this.popupParent = popupParent ? popupParent : throw new ArgumentNullException(nameof(popupParent));
            this.showPopup = showPopup ?? throw new ArgumentNullException(nameof(showPopup));
            this.closePopup = closePopup ?? throw new ArgumentNullException(nameof(closePopup));
            data = JsonUtility.FromJson<Data>(assets.Bindings.text);
            if (data.item_detail.item_id != 41021 || data.item_detail.show_use_detail
                || data.item_detail.has_output_addition_group || data.item_detail.is_count_limit
                || data.item_detail.quality_text_color.EnableGradient != 0)
                throw new InvalidOperationException("Reinspect original demon item-detail bindings.");
            if (data.item_detail.source_analysis == null || data.item_detail.source_analysis.has_sources)
                throw new InvalidOperationException("Evaluate all original source agents before displaying this item.");
            texts = data.texts.ToDictionary(row => row.key, row => row.text);
            var info = new OfflinePartnerItemInfo(OfficialPartnerAssetCatalog.LoadDefault(), 41021);
            definition = new ItemDetailDefinition {
                Info = info, NameKey = data.item_detail.name_key, TypeKey = data.item_detail.type_key,
                DescriptionKey = data.item_detail.description_key,
                FunctionDescriptionKey = data.item_detail.function_description_key,
                ShowPlayerItemCount = data.item_detail.show_player_item_count
            };
            images = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
            var own = data.images.Select(row => new OfflineProfessionImageCatalog.Entry {
                Path = "Assets/Image/" + row.path, AssetGuid = row.guid,
                Asset = row.path == info.Icon ? assets.RewardIcon
                    : row.path == "UI/ItemDetailDialog/ImgPurple_PopUps" ? assets.DetailQualityBackground
                    : throw new InvalidOperationException("Unbound original demon item sprite: " + row.path)
            });
            if (data.attachments.Length != assets.AttachmentPrefabs.Length)
                throw new InvalidOperationException("Original demon item attachments are unbound.");
            var attachmentImages = data.attachments.Select((row, index) => new OfflineProfessionImageCatalog.Entry {
                Path = "OriginalDemonAttachment/" + row.type, AssetGuid = row.guid, Asset = assets.AttachmentPrefabs[index]
            });
            images.Entries = own.Concat(attachmentImages).Concat(sharedImages.Entries).GroupBy(row => row.Path).Select(group => group.First())
                .GroupBy(row => row.AssetGuid).Select(group => group.First()).ToArray();
            Resources = new OfflineProfessionImageResourceLoader(images);
            attachments = new ItemIconAttachmentManager(Resources, () => assets.AttachmentConfig, new GAction());
            previousCount = GetItemAmount(41021);
        }
        public void BindTree(GameObject root)
        {
            if (root.activeInHierarchy) throw new InvalidOperationException("Bind original item UI while inactive.");
            foreach (var icon in root.GetComponentsInChildren<ItemIcon>(true)) icon.Bind(this);
            foreach (var attachment in root.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(attachments);
            foreach (var progress in root.GetComponentsInChildren<WorldItemProgress>(true))
                progress.Compact = count => CultureNumberArg.Compact(count).ToString();
            foreach (var dialog in root.GetComponentsInChildren<ItemDetailDialog>(true)) dialog.Bind(this);
            foreach (var button in root.GetComponentsInChildren<OutputAdditionDetailButton>(true)) button.BindNoOutputGroup(definition);
            foreach (var gm in root.GetComponentsInChildren<ItemDetailGmTools>(true)) gm.BindReleaseActor(false, false);
            foreach (var decomposition in root.GetComponentsInChildren<ItemDecomposContent>(true))
                decomposition.Bind(null, () => Close(root));
            foreach (var awards in root.GetComponentsInChildren<OfficialBattleAwardList>(true)) awards.Bind(this);
        }
        public void RefreshInventory()
        {
            long count = GetItemAmount(41021);
            if (count == previousCount) return;
            long old = previousCount; previousCount = count;
            OnItemCountUpdate?.Invoke(new ItemData(definition.Info, count), old, null);
        }
        public string Localize(string key) => texts.TryGetValue(key, out var value) ? value : key;
        public IProfessionItemIconContext Items => this;
        public ItemIconConfig Config => assets.ItemConfig;
        public UnityEngine.Object GrayConfig => null;
        public IProfessionItemColors Colors => this;
        public IProfessionItemPlayer Player => this;
        public IProfessionItemInfo GetItemInfo(int itemId) => Detail(itemId).Info;
        public object Compact(long count) => CultureNumberArg.Compact(count);
        public object CultureText(string key) => new CultureTextArg(Localize(key), Array.Empty<object>(), false);
        public string GetItemNameKey(int itemId) => Detail(itemId).NameKey;
        public bool TryGetSourceData(int itemId, out IReadOnlyList<IProfessionItemSource> sources)
        { Detail(itemId); sources = Array.Empty<IProfessionItemSource>(); return false; }
        public bool CanMerge(int itemId, bool recursion) { Detail(itemId); return false; }
        public object GetLocationInfos(int itemId) => throw new NotSupportedException("This item has no map-resource source.");
        public void SetUIGray(UnityEngine.Object config, GameObject target, bool gray)
            => throw new NotSupportedException("Bind the original UI-gray configuration before requesting a gray item.");
        public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter)
        {
            if (value != null && value.idInternal != 0)
                throw new NotSupportedException("The selected original reward icon has no frame sound switch.");
        }
        public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All)
        {
            var instance = instantiate(prefab, parent);
            if ((flag & EUIInstantiateFlag.Position) != 0) instance.transform.localPosition = Vector3.zero;
            if ((flag & EUIInstantiateFlag.Rotation) != 0) instance.transform.localRotation = Quaternion.identity;
            if ((flag & EUIInstantiateFlag.Scale) != 0) instance.transform.localScale = Vector3.one;
            if ((flag & EUIInstantiateFlag.AutoActive) != 0) instance.SetActive(true);
            return instance;
        }
        public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback)
            => throw new NotSupportedException("The selected original reward has no asynchronous icon effect.");
        public void PopupInfoDialog(IProfessionItemData item, IWindowOperator owner, long required, Action<GameObject> callback)
        {
            if (item.CanExpire()) throw new NotSupportedException("Bind original expiring-item details before opening this inventory object.");
            PopupInfoDialog(item.Info, owner, required, callback);
        }
        public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback)
        {
            Detail(info.ClassId);
            var content = instantiate(assets.ItemDetail, popupParent);
            var windows = new OfflinePartnerWindowService(Close, () => Close(content));
            foreach (var close in content.GetComponentsInChildren<CloseWindowBtn>(true)) close.Init(windows);
            content.GetComponent<ItemDetailDialog>().Init(info.ClassId, required);
            showPopup(content); callback?.Invoke(content);
        }
        public long GetItemAmount(int itemId) { Detail(itemId); return home.ItemCount(itemId); }
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        public bool CanGetItemFromMapLocation(object location) => throw new NotSupportedException("No map-resource source is configured for this item.");
        public Color Green => data.colors.Green;
        public Color White => data.colors.White;
        public Color LightRed => GetTextColor(ThemeColorType.Light, ColorType.Red);
        public Color GetTextColor(ThemeColorType theme, ColorType type)
        {
            var row = data.colors.TextColorConfig.TextColorLists.Single(value => value.ThemeColorType == (int)theme);
            var color = row.TextColors[Array.IndexOf(row.TypeList, (int)type)];
            if (color.EnableGradient != 0) throw new NotSupportedException("Use the original gradient-text color adapter for this theme.");
            return color.Color;
        }
        public bool HasLocalPlayer => home.Profile != null;
        public IReadOnlyList<OfficialBattleAwardEntry> PrepareAwards(IReadOnlyList<OfficialBattleAwardEntry> awards)
        { foreach (var award in awards) VerifyAward(award); return awards; }
        public bool ShouldDisplay(OfficialBattleAwardEntry award) { VerifyAward(award); return true; }
        public void BindAwardItem(Component item, OfficialBattleAwardEntry award) => ((AwardPanelItem)item).Init(this, award);
        public void SetAwardItemVisible(Component item, bool visible)
        { if (visible) ((AwardPanelItem)item).Show(); else ((AwardPanelItem)item).Hide(); }
        private static void VerifyAward(OfficialBattleAwardEntry value)
        {
            if (value == null || value.ItemId != 41021 || value.Count != 300 || value.OriginalData != null)
                throw new InvalidOperationException("Only the original fixed challenge award is bound here.");
        }
        public ItemAwardIcon InstantiateAward(Transform parent, OfficialBattleAwardEntry award)
        {
            VerifyAward(award);
            var icon = instantiate(assets.AwardIcon, parent).GetComponent<ItemAwardIcon>();
            icon.Init(this, award); return icon;
        }
        public ItemIcon GetIcon(int itemId, Transform parent)
        {
            var icon = instantiate(assets.ItemIcon, parent).GetComponent<ItemIcon>();
            icon.Init(GetItemInfo(itemId)); return icon;
        }
        public void RecycleIcon(ItemIcon icon) { if (icon) { icon.Clear(); UnityEngine.Object.Destroy(icon.gameObject); } }
        public ItemDetailDefinition Detail(int itemId) => itemId == 41021 ? definition : throw new ArgumentOutOfRangeException(nameof(itemId));
        public bool ContainsText(string key) => texts.ContainsKey(key);
        public Sprite LoadSprite(string address)
        {
            using var request = Resources.LoadAsset<Sprite>("Assets/Image/" + address);
            return request.Result ? request.Result : throw new InvalidOperationException("Original item-detail sprite is absent: " + address);
        }
        public Sprite QualityBackground(Quality quality, bool small) => quality == Quality.Purple && !small
            ? assets.DetailQualityBackground : throw new NotSupportedException("Bind the selected original quality background for this detail.");
        public Color QualityTextColor(Quality quality) => quality == Quality.Purple
            ? data.item_detail.quality_text_color.Color : throw new NotSupportedException("Bind the original quality color for this detail.");
        public IReadOnlyList<ItemDetailSource> Sources(int itemId) { Detail(itemId); return Array.Empty<ItemDetailSource>(); }
        public GameObject InstantiateUi(GameObject prefab, Transform parent) => instantiate(prefab, parent);
        public void Close(GameObject window) => closePopup(window);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; attachments.Dispose(); UnityEngine.Object.Destroy(images);
        }
    }
}
