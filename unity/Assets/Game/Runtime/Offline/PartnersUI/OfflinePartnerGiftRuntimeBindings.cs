using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    // The gift prefab consumes the profession upgrade interfaces through its
    // red-dot and ItemIcon children. The gift page does not perform profession
    // upgrades, so unsupported upgrade actions remain inert while inventory
    // reads and item events stay bound to the real character save.
    public sealed class OfflinePartnerGiftRuntimeBindings : IDisposable, IProfessionUpgradeBindings,
        IProfessionUpgradePlayer, IProfessionTipDotSystem, IProfessionItemIconContext, IProfessionItemPlayer,
        IProfessionItemColors
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialPartnerCatalog catalog;
        private readonly OfficialPartnerGiftPageAssetCatalog assets;
        private readonly Func<int, IProfessionItemInfo> getItemInfo;
        private readonly Func<string, string> text;
        private readonly Dictionary<int, IProfessionItemInfo> infos = new Dictionary<int, IProfessionItemInfo>();
        private readonly SafeAction playerUpdated = new SafeAction();
        private readonly SafeAction tipInitialized = new SafeAction();
        private readonly SafeAction tipPreClear = new SafeAction();
        private readonly ItemIconAttachmentManager attachments;
        private readonly MainPlayerProxy player;
        public OfflinePartnerGiftRuntimeBindings(OfflineMainlineProgressSession session,
            OfficialPartnerCatalog catalog, OfficialPartnerGiftPageAssetCatalog assets,
            Func<int, IProfessionItemInfo> getItemInfo, Func<string, string> text)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.getItemInfo = getItemInfo ?? throw new ArgumentNullException(nameof(getItemInfo));
            this.text = text ?? (key => key);
            attachments = new ItemIconAttachmentManager(new OfflineProfessionImageResourceLoader(assets.Images),
                () => assets.AttachmentConfig, new GAction());
            player = new MainPlayerProxy(this);
        }
        public void Dispose() => attachments.Dispose();
        public void BindGiftIcon(ItemIcon icon)
        {
            if (icon == null) return;
            icon.Bind(this);
            foreach (var attachment in icon.GetComponentsInChildren<ItemIconAttachment>(true)) attachment.Bind(attachments);
            foreach (var progress in icon.GetComponentsInChildren<WorldItemProgress>(true))
                progress.Compact = count => CultureNumberArg.Compact(count).ToString();
        }

        public ItemIcon CreateGiftIcon(int itemId, Transform parent, GameObject prefab)
        {
            if (prefab == null) return null;
            ItemIcon icon = UnityEngine.Object.Instantiate(prefab, parent, false).GetComponent<ItemIcon>();
            if (icon == null) return null;
            BindGiftIcon(icon);
            icon.Init(new OfflinePartnerItemData(itemId, GetItemInfo(itemId), GetItemAmount(itemId)));
            return icon;
        }

        public IProfessionUpgradePlayer MainPlayer => this;
        public bool ManagerAvailable => true;
        public SafeAction OnPlayerUpdate => playerUpdated;
        public IProfessionTipDotSystem TipDotSystem => this;
        public ItemIconConfig Config => assets.IconConfig;
        public UnityEngine.Object GrayConfig => null;
        public IProfessionItemColors Colors => this;
        public IProfessionItemPlayer Player => player;
        public LanguageType Language => LanguageType.zh_CN;
        public bool IsShowTipDot => true;
        public bool TempLimitShowTip => false;
        public Action OnTipDotShowChanged { get; set; }
        public SafeAction OnTipDotSystemInited { get => tipInitialized; set { } }
        public SafeAction OnTipDotSystemPreClear { get => tipPreClear; set { } }
        public ProfessionDetailDefinition GetProfession(int profession) => null;
        public int GetLevelPropId(int profession) => 0;
        public bool TryGetLevelInfo(int profession, int level, out ProfessionUpgradeCostDefinition info) { info = null; return false; }
        public IReadOnlyDictionary<int, long> GetProps(int levelPropId, int level) => new Dictionary<int, long>();
        public Dictionary<int, long> GetProfessionLevelPropDiff(int profession, int fromLevel, int toLevel) => new Dictionary<int, long>();
        public string GetPropNameText(int propType) => text("prop_" + propType);
        public string FormatPropValue(int propType, long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public void InitPropItem(ProfessionPlayerPropItem item, int propType, long value, long nextValue) { }
        public int GetItemType(IProfessionItemInfo item) => item == null ? 0 : ItemType(item.ClassId);
        public long CalcAwardBadge(ProfessionDetailDefinition profession, IProfessionUpgradePlayer player) => 0;
        public ItemIcon GetIcon(int itemId, Transform parent) => null;
        public void RecycleIcon(ItemIcon icon) { if (icon != null) icon.Clear(); }
        public void Upgrade(int profession, Action onSuccess) { }
        public int MoneyId => 0;
        public void ShowLackOfItem(int itemId, long amount) { }
        public bool IsItemSourceFromMerge(IProfessionUpgradePlayer player, int itemId) => false;
        public long CalcCanMergeAmount(IProfessionUpgradePlayer player, int itemId, bool recursion) => 0;
        public TipDotGraphType? GetGraphType(string graphName) => null;
        public IProfessionItemInfo GetItemInfo(int itemId)
        {
            if (!infos.TryGetValue(itemId, out var value)) infos[itemId] = value = getItemInfo(itemId);
            return value;
        }
        public object Compact(long count) => CultureNumberArg.Compact(count);
        public object CultureText(string key) => text(key);
        public string GetItemNameKey(int itemId) => "item_name_" + itemId;
        public bool TryGetSourceData(int itemId, out IReadOnlyList<IProfessionItemSource> sources) { sources = null; return false; }
        public bool CanMerge(int itemId, bool recursion) => false;
        public object GetLocationInfos(int itemId) => null;
        public void SetUIGray(UnityEngine.Object config, GameObject target, bool gray) { }
        public void SetSoundSwitch(ProfessionItemSoundSwitch value, GameObject emitter) { }
        public GameObject Instantiate(GameObject prefab, Transform parent, EUIInstantiateFlag flag = EUIInstantiateFlag.All)
            => prefab == null ? null : UnityEngine.Object.Instantiate(prefab, parent, false);
        public void InstantiateAsync(string prefab, Transform parent, Action<GameObject> callback)
        {
            GameObject source = string.IsNullOrEmpty(prefab) ? null : UnityEngine.Resources.Load<GameObject>(prefab);
            callback?.Invoke(source == null ? null : UnityEngine.Object.Instantiate(source, parent, false));
        }
        public void PopupInfoDialog(IProfessionItemData data, IWindowOperator owner, long required, Action<GameObject> callback) { }
        public void PopupInfoDialog(IProfessionItemInfo info, IWindowOperator owner, long required, Action<GameObject> callback) { }
        public long GetItemAmount(int itemId) => session.Save.Player.Inventory.Find(value => value.ItemId == itemId)?.Count ?? 0;
        public event Action<IProfessionItemData, long, object> OnItemCountUpdate;
        public bool CanGetItemFromMapLocation(object location) => false;
        public Color Green => new Color(0.25f, 0.85f, 0.35f, 1f);
        public Color White => Color.white;
        public Color LightRed => new Color(1f, 0.35f, 0.35f, 1f);
        public Color GetTextColor(ThemeColorType theme, ColorType type) => Color.white;
        public int Profession => session.Save.Player.Wardrobe.Profession;
        public bool IsProfessionFunctionLimit => false;
        public bool IsProfessionUnlocked(int profession) => true;
        public int GetProfessionLevel(int profession) => 1;
        public event Action<int, int> OnProfessionUpgrade;
        public event Action<int, int> OnSkillRankRewardUpdate;
        public event Action<IProfessionItemData, int, int> OnSkillRankUp;
        public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;

        private int ItemType(int itemId)
        {
            var row = catalog.Rows("item").FirstOrDefault(value => Convert.ToInt32(value["ClassId"]) == itemId);
            return row != null && row.TryGetValue("ItemType", out var value) ? Convert.ToInt32(value) : 0;
        }

        private sealed class MainPlayerProxy : IProfessionUpgradePlayer, IProfessionItemPlayer
        {
            private readonly OfflinePartnerGiftRuntimeBindings owner;
            public MainPlayerProxy(OfflinePartnerGiftRuntimeBindings owner) { this.owner = owner; }
            public int Profession => owner.Profession;
            public bool IsProfessionFunctionLimit => owner.IsProfessionFunctionLimit;
            public bool IsProfessionUnlocked(int profession) => owner.IsProfessionUnlocked(profession);
            public int GetProfessionLevel(int profession) => owner.GetProfessionLevel(profession);
            public long GetItemAmount(int itemId) => owner.GetItemAmount(itemId);
            public event Action<IProfessionItemData, long, object> OnItemCountUpdate;
            public event Action<int, int> OnProfessionUpgrade;
            public event Action<int, int> OnSkillRankRewardUpdate;
            public event Action<IProfessionItemData, int, int> OnSkillRankUp;
            public event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
            public bool CanGetItemFromMapLocation(object location) => false;
        }

        private sealed class OfflinePartnerItemData : IProfessionItemData
        {
            public OfflinePartnerItemData(int id, IProfessionItemInfo info, long count)
            { Id = id; Info = info; Count = count; }
            public int Id { get; }
            public long Count { get; }
            public IProfessionItemInfo Info { get; }
            public Quality GetQuality() => Info.Quality;
            public bool CanExpire() => false;
        }
    }
}
