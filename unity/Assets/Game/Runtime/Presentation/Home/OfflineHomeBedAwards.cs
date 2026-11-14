using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Object = UnityEngine.Object;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using ItemType = Zjcs.Official.HomeAwardUI.ItemType;
using Formula = Zjcs.Official.HomeIncomeRules.BattleFormulaHandler;
using FormulaProp = Zjcs.Official.HomeIncomeRules.PropType;

namespace Zjcs.Presentation.Home
{
    // This owner presents committed home receipts. It never changes either save.
    public sealed class OfflineHomeBedAwards : IHomeAwardQueueServices, IHomeAwardItemServices,
        IHomeAwardIconServices, IItemIconAttachmentManager, IProfessionItemIconContext,
        IProfessionItemPlayer, IProfessionSoundHost, IDisposable
    {
        [Serializable] sealed class TextRow { public string key, text; }
        [Serializable] sealed class Data { public string manifest; public Info item; public Palette global_colors; public TextRow[] texts; }
        [Serializable] sealed class Info { public int ClassId, Quality, ItemType, Rank; public string RawIcon, IconEffect; public bool CanOverlap; }
        [Serializable] sealed class IncomeData { public string manifest; public IncomeTables tables; }
        [Serializable] sealed class IncomeTables { public Info[] item; public IncomeSettings game_settings; }
        [Serializable] sealed class IncomeSettings { public int[] IgnoreShowAwardId; }
        [Serializable] sealed class TextColor { public Color Color; }
        [Serializable] sealed class ColorList { public int ThemeColorType; public int[] TypeList; public TextColor[] TextColors; }
        [Serializable] sealed class ColorConfig { public ColorList[] TextColorLists; }
        [Serializable] sealed class Palette : IProfessionItemColors
        {
            public Color Green, White;
            public ColorConfig TextColorConfig;
            Color IProfessionItemColors.Green => Green;
            Color IProfessionItemColors.White => White;
            public Color LightRed => GetTextColor(ThemeColorType.Light, ColorType.Red);
            public Color GetTextColor(ThemeColorType theme, ColorType type)
            {
                var row = TextColorConfig.TextColorLists.Single(value => value.ThemeColorType == (int)theme);
                int index = Array.IndexOf(row.TypeList, (int)type);
                if (index < 0) throw new InvalidOperationException("Original award color is unbound: " + type);
                return row.TextColors[index].Color;
            }
        }
        sealed class IncomeItem : ItemInfo
        {
            internal Info Source;
            public override int ClassId => Source.ClassId;
            public override string Icon => Source.RawIcon;
            public override string IconEffect => Source.IconEffect;
            public override Quality Quality => (Quality)Source.Quality;
            public override bool CanOverlap => Source.CanOverlap;
            public override ItemType ItemType => (ItemType)Source.ItemType;
            public override int Rank => Source.Rank;
            public override object PartInfo => null;
            public override bool IsEquip => false;
            public override bool IsSkill => false;
            public override bool IsSkillPiece => false;
            public override object GetFakeIcon() => null;
        }
        sealed class Asset<T> : IHomeAwardAsset<T>
        {
            T value;
            internal Asset(T value) { this.value = value; }
            public T Result => value;
            public void Dispose() { value = default; }
        }
        sealed class Done : IHomeAwardPopupOperation { public bool IsDone => true; }
        readonly HomeFeatureRuntime home;
        readonly Canvas canvas;
        readonly Action<AudioClip, GameObject> postSound;
        readonly OfficialHomeAwardQueueAssetCatalog assets;
        readonly Data data;
        readonly IncomeItem experience;
        readonly Dictionary<int, IncomeItem> incomeItems;
        readonly HashSet<int> ignoredAwards;
        readonly Dictionary<string, string> texts;
        readonly FontAssetFontService fonts;
        readonly Transform staging;
        readonly OfflineProfessionImageCatalog imageCatalog;
        readonly IProfessionImageResourceLoader images;
        readonly Formula formula = new Formula(Zjcs.Official.HomeIncomeRules.HomeIncomePropertyCatalog.Get);
        readonly Dictionary<ItemIcon, IDisposable> iconScopes = new Dictionary<ItemIcon, IDisposable>();
        IDisposable scope;
        AwardListPanel panel;
        bool disposed;
        uint playingId;

        public OfflineHomeBedAwards(HomeFeatureRuntime home, Canvas canvas, Action<AudioClip, GameObject> postSound)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.canvas = canvas ? canvas : throw new ArgumentNullException(nameof(canvas));
            this.postSound = postSound ?? throw new ArgumentNullException(nameof(postSound));
            try
            {
                assets = OfficialHomeAwardQueueAssetCatalog.LoadDefault();
                var configuration = UnityEngine.Resources.Load<TextAsset>("Home/home-award-queue-data");
                data = configuration ? JsonUtility.FromJson<Data>(configuration.text) : null;
                if (data?.manifest != "884_156692" || data.item?.ClassId != 50
                    || data.item.ItemType != 50 || !data.item.CanOverlap || !string.IsNullOrEmpty(data.item.IconEffect))
                    throw new InvalidOperationException("Selected bed XP award data is unbound.");
                experience = new IncomeItem { Source = data.item };
                texts = data.texts.ToDictionary(row => row.key, row => row.text);
                var incomeAssets = Zjcs.Official.HomeFastHangUpUI.OfficialHomeFastHangUpAssetCatalog.LoadDefault();
                var incomeData = JsonUtility.FromJson<IncomeData>(incomeAssets.rules.text);
                if (incomeData?.manifest != data.manifest || incomeData.tables?.item == null
                    || incomeData.tables.game_settings?.IgnoreShowAwardId == null)
                    throw new InvalidOperationException("Selected home income reward data is unbound.");
                incomeItems = incomeData.tables.item.ToDictionary(item => item.ClassId,
                    item => new IncomeItem { Source = item });
                if (!incomeItems.TryGetValue(experience.ClassId, out var selectedExperience)
                    || JsonUtility.ToJson(selectedExperience.Source) != JsonUtility.ToJson(data.item))
                    throw new InvalidOperationException("Selected income XP definition differs from the bed definition.");
                ignoredAwards = new HashSet<int>(incomeData.tables.game_settings.IgnoreShowAwardId);
                foreach (var row in JsonUtility.FromJson<Data>(incomeAssets.texts.text).texts)
                {
                    if (texts.TryGetValue(row.key, out string prior) && prior != row.text)
                        throw new InvalidOperationException("Selected income reward translations disagree: " + row.key);
                    texts[row.key] = row.text;
                }
                var hud = OfflineHomeHudAssets.LoadDefault();
                fonts = hud.CreateFontService(); fonts.Initialize();
                staging = new GameObject("HomeAwardInactiveBinding", typeof(RectTransform)).transform;
                staging.SetParent(canvas.transform, false); staging.gameObject.SetActive(false);
                var bed = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
                if (bed.bedRewardImagePath != experience.Icon || !bed.bedRewardImage)
                    throw new InvalidOperationException("Selected bed reward sprite differs from its item data.");
                imageCatalog = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
                imageCatalog.Entries = incomeAssets.images.Select(image => new OfflineProfessionImageCatalog.Entry {
                    Path = "Assets/Image/" + image.path, AssetGuid = image.assetGuid, Asset = image.sprite }).ToArray();
                foreach (var item in incomeItems.Values)
                    if (string.IsNullOrEmpty(item.IconEffect) && !imageCatalog.Entries.Any(image =>
                        image.Path == "Assets/Image/" + item.Icon && image.Asset))
                        throw new InvalidOperationException("Selected income reward image is unbound: " + item.ClassId);
                images = new OfflineProfessionImageResourceLoader(imageCatalog);
                var root = Clone(assets.queuePrefab, canvas.transform);
                panel = root.GetComponent<AwardListPanel>(); panel.Services = this; panel.BindSoundHost(this);
                scope = OfflineUiBindingModule.Register(root.transform, new OfflineUiBindings {
                    Localize = Text, Font = () => hud.defaultFont, Images = _ => images,
                    Pool = (_, prefab, parent, world) => Object.Instantiate(prefab, parent, world) });
            }
            catch { Dispose(); throw; }
        }

        public void Show(OfflineHomeCollectedReward reward)
            => Show(new[] { reward });

        public void Show(IEnumerable<OfflineHomeCollectedReward> rewards)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfflineHomeBedAwards));
            var awards = new List<AwardData>();
            foreach (var reward in rewards ?? throw new ArgumentNullException(nameof(rewards)))
            {
                if (reward == null || reward.Count <= 0)
                    throw new ArgumentException("Positive committed home rewards are required.", nameof(rewards));
                GetItemInfo(reward.ItemId);
                awards.Add(new AwardData { Catalog = this, AwardId = reward.ItemId, Amount = reward.Count });
            }
            AwardListPanel.PopupAwardListPanel(this, awards);
        }
        GameObject Clone(GameObject prefab, Transform parent)
        {
            var clone = Object.Instantiate(prefab, staging, false); clone.SetActive(false);
            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) fonts.SetupFont(text.font);
            foreach (var text in clone.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = Text;
            clone.transform.SetParent(parent, false); return clone;
        }
        public bool HasPlayer => !disposed && home.Profile != null;
        public event Action<GameObject> OnWindowOpened;
        public void NotifyWindowOpened(GameObject window) => OnWindowOpened?.Invoke(window);
        public bool IsFullScreen(GameObject window) => window && window.GetComponent<WorldPopupWindow>()?.FullScreenDisplay == true;
        public void Close(GameObject window) { if (window) window.SetActive(false); }
        public bool IsIgnoreShowAward(int id) { GetItemInfo(id); return ignoredAwards.Contains(id); }
        public IHomeAwardAsset<GameObject> LoadAssetByGuid(string guid)
            => new Asset<GameObject>(assets.itemPrefabs.Single(row => row.originalGuid == guid).prefab);
        public GameObjectPool<AwardListPanelItem> CreatePool(Transform root, AwardListPanelItem prefab)
            => new GameObjectPool<AwardListPanelItem>(root, (parent, _, active) => {
                var clone = Clone(prefab.gameObject, parent);
                clone.GetComponent<AwardListPanelItem>().Services = this;
                clone.SetActive(active); return clone;
            }, prefab.name);
        public GameObject GetWindow() => panel && panel.gameObject.activeInHierarchy ? panel.gameObject : null;
        public IHomeAwardPopupOperation Popup(IWindowOperator owner, Action<AwardListPanel> initialized,
            bool instant, bool local, bool autoAdjustByWindowContext)
        {
            panel.gameObject.SetActive(true); initialized(panel); return new Done();
        }
        public ItemInfo GetItemInfo(int id) => incomeItems.TryGetValue(id, out var item) ? item
            : throw new ArgumentOutOfRangeException(nameof(id), "Bind the selected original reward catalog before displaying another item.");
        public HomeAwardIcon InstantiateAward(Transform slot, AwardData award, Action<HomeAwardIcon, AwardData> init,
            bool autoTransition, bool isSelf, bool showOutputAddition)
        {
            GetItemInfo(award.AwardId);
            var icon = Clone(assets.defaultIconPrefab, slot).GetComponent<HomeAwardIcon>();
            icon.Services = this; icon.gameObject.SetActive(true);
            icon.Init(award, init, autoTransition, isSelf, showOutputAddition); return icon;
        }
        public string GetItemNameKey(int id) { GetItemInfo(id); return "item_" + id + "_name"; }
        public object CultureText(string key) => new CultureTextArg(Text(key), Array.Empty<object>(), false);
        public string Text(string key) => texts.TryGetValue(key, out var text) ? text : key;
        public object Compact(long count) => CultureNumberArg.Compact(count);
        public object DecorateExtra(object text, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">" + text + "</color>";
        public int MoneyId => 1;
        public int PlayerExpItemId => experience.ClassId;
        public double GetMoneyAdditionPercent() => formula.GetProp(home.PlayerProperties(), FormulaProp.LingShiAdditionAwardPercent);
        public double GetExperienceAdditionPercent() => formula.GetProp(home.PlayerProperties(), FormulaProp.ExpAdditionAwardPercent);
        public IHomeAwardAsset<Sprite> LoadSprite(string path)
        {
            using var handle = images.LoadAsset<Sprite>("Assets/Image/" + path);
            if (!handle.Result) throw new InvalidOperationException("Original award sprite is unbound: " + path);
            return new Asset<Sprite>(handle.Result);
        }
        public ItemIcon GetIcon(ItemInfo info, Transform parent)
        {
            GetItemInfo(info.ClassId);
            var icon = Clone(assets.itemIconPrefab, parent).GetComponent<ItemIcon>(); icon.Bind(this);
            iconScopes.Add(icon, OfflineUiBindingModule.Register(icon.transform, new OfflineUiBindings {
                Localize = Text, Font = () => OfflineHomeHudAssets.LoadDefault().defaultFont,
                Images = _ => images,
                Pool = (_, prefab, owner, world) => Object.Instantiate(prefab, owner, world) }));
            foreach (var attachment in icon.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(this);
            icon.gameObject.SetActive(true); icon.Init(info); return icon;
        }
        public ItemIcon GetIcon(IProfessionItemData item, Transform parent) => throw OutsideBed("indexed reward");
        public void RecycleIcon(ItemIcon icon)
        {
            if (ReferenceEquals(icon, null)) return;
            if (icon) { icon.Clear(); icon.gameObject.SetActive(false); Object.Destroy(icon.gameObject); }
            if (iconScopes.TryGetValue(icon, out var binding)) { binding.Dispose(); iconScopes.Remove(icon); }
        }
        public IProfessionItemData GetItem(ulong index) => throw OutsideBed("indexed reward");
        public IHomeAwardOutputAddition GetOutputAddition(ItemIcon icon) => throw OutsideBed("activity output attachment");
        public GameObject GetAttachment(ItemIconAttachmentType type, Transform parent, bool active)
        {
            var prefab = type == ItemIconAttachmentType.HintTip ? assets.hintPrefab
                : type == ItemIconAttachmentType.ExtraGetTip ? assets.extraGetPrefab : null;
            if (!prefab) throw OutsideBed("attachment " + type);
            var clone = Clone(prefab, parent); clone.SetActive(active); return clone;
        }
        public void RecycleAttachment(ItemIconAttachmentType type, ItemIconAttachmentCell value)
        { if (value) { value.gameObject.SetActive(false); Object.Destroy(value.gameObject); } }
        public uint PostEvent(uint eventId, GameObject emitter)
        {
            var clip = assets.sounds.FirstOrDefault(row => row.eventId == eventId)?.clip
                ?? OfflineHomeHudAssets.LoadDefault().Sound(eventId);
            if (!clip)
            {
                var bed = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
                if (eventId == bed.fastHangUpEventId) clip = bed.fastHangUpClip;
            }
            if (!clip) throw new InvalidOperationException("Original award sound is unbound: " + eventId);
            postSound(clip, emitter); return ++playingId;
        }
        public ItemIconConfig Config => assets.itemIconConfig;
        public Object GrayConfig => null;
        public IProfessionItemColors Colors => data.global_colors;
        public IProfessionItemPlayer Player => this;
        internal IProfessionImageResourceLoader Images => images;
        IProfessionItemInfo IProfessionItemIconContext.GetItemInfo(int id) => GetItemInfo(id);
        public bool TryGetSourceData(int id, out IReadOnlyList<IProfessionItemSource> sources) { GetItemInfo(id); sources = Array.Empty<IProfessionItemSource>(); return false; }
        public bool CanMerge(int id, bool recursion) { GetItemInfo(id); return false; }
        public object GetLocationInfos(int id) => throw OutsideBed("item sources");
        public void SetUIGray(Object config, GameObject target, bool gray) { if (gray) throw OutsideBed("gray icon"); }
        public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter)
        { if (value != null && value.idInternal != 0) throw OutsideBed("frame sound switch"); }
        public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All)
        {
            var clone = Clone(prefab, parent);
            if ((flag & EUIInstantiateFlag.Position) != 0) clone.transform.localPosition = Vector3.zero;
            if ((flag & EUIInstantiateFlag.Rotation) != 0) clone.transform.localRotation = Quaternion.identity;
            if ((flag & EUIInstantiateFlag.Scale) != 0) clone.transform.localScale = Vector3.one;
            if ((flag & EUIInstantiateFlag.AutoActive) != 0) clone.SetActive(true);
            return clone;
        }
        public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback) => throw OutsideBed("item effect");
        public void PopupInfoDialog(IProfessionItemData item, IWindowOperator owner, long required, Action<GameObject> callback) => throw OutsideBed("indexed item details");
        public void PopupInfoDialog(IProfessionItemInfo item, IWindowOperator owner, long required, Action<GameObject> callback) => throw OutsideBed("independent item details");
        public long GetItemAmount(int id)
        {
            GetItemInfo(id);
            return id == experience.ClassId ? home.Profile.Save.Player.Experience
                : Zjcs.Offline.Progression.OfflineItemAutoIncrease.Count(
                    home.Profile.Save.Player.Inventory.FirstOrDefault(item => item.ItemId == id), DateTime.UtcNow);
        }
#pragma warning disable 0067, 0414
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate;
#pragma warning restore 0067, 0414
        public bool CanGetItemFromMapLocation(object location) => throw OutsideBed("map item sources");
        static NotSupportedException OutsideBed(string branch) => new NotSupportedException("The selected home income queue does not consume " + branch + ".");
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (panel) { panel.gameObject.SetActive(false); Object.Destroy(panel.gameObject); }
            foreach (var icon in iconScopes.Keys.ToArray()) RecycleIcon(icon);
            panel = null; OnWindowOpened = null; OnItemCountUpdate = null;
            scope?.Dispose(); scope = null; fonts?.Dispose();
            if (staging) Object.Destroy(staging.gameObject);
            Object.Destroy(imageCatalog);
        }
    }
}
