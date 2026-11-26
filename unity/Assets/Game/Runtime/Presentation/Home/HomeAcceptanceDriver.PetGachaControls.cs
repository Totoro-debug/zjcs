using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.GachaUI;
using Zjcs.Official.HomeCurrencyWindowsUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeNativeAssets;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyPetGachaControls(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool passed = false;
            var pending = new Stack<IEnumerator>();
            pending.Push(VerifyPetGachaControlSteps(runtime, value => passed = value));
            while (pending.Count > 0)
            {
                bool next = false;
                object current = null;
                Exception failure = null;
                try { next = pending.Peek().MoveNext(); if (next) current = pending.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Check("pet-gacha-controls-execution", () => throw failure);
                    while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
                    runtime.Close();
                    completed(false);
                    yield break;
                }
                if (!next) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) pending.Push(nested);
                else yield return current;
            }
            completed(passed);
        }

        private IEnumerator VerifyPetGachaControlSteps(HomeFeatureRuntime runtime, Action<bool> completed)
        {
#if UNITY_EDITOR
            var proof = JObject.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("ZJCS_PET_GACHA_CONTROL_FIXTURE")));
            var services = new GachaControlFixtureServices(runtime, proof);
            var nativeCosts = PrivateField<IHomeCurrencyWindowServices>(runtime.Hud, "nativeCurrencyServices");
            var images = ScriptableObject.CreateInstance<Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog>();
            images.Entries = new[] { new Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog.Entry {
                Path = (string)proof["cost_sprite"]["path"], AssetGuid = (string)proof["cost_sprite"]["asset_guid"],
                Asset = LoadGachaControlAsset<Sprite>(proof["cost_sprite"]["pointer"]) } };
            nativeCosts = new OfflineHomeCurrencyImageServices(nativeCosts, images);
            var quantities = PrivateField<OfflineHomeCurrencyPlayer>(runtime.Hud, "nativeCurrencyPlayer");
            var font = OfflineHomeHudAssets.LoadDefault().defaultFont;
            var texts = (JObject)JObject.Parse(LoadGachaControlAsset<TextAsset>(proof["texts"]).text)["texts"];
            var roots = new List<GameObject>();
            using var sound = new OfflinePartnerUiSoundScope(id => ((JObject)proof["audio"]).Properties()
                .Select(row => row.Value).Where(row => (uint)row["event"]["id"] == id)
                .Select(row => LoadGachaControlAsset<AudioClip>(row["pointer"])).SingleOrDefault());
            using var factory = new HomeNativePrefabFactory(go =>
            {
                go.AddComponent<HomeHudResourceScope>().Loader = services;
                foreach (var text in go.GetComponentsInChildren<WorldMultiLanguageText>(true))
                { text.Localizer = key => (string)texts[key] ?? key; text.FontProvider = () => font; }
                foreach (var cost in go.GetComponentsInChildren<PlayerCostMoneyContent>(true)) cost.Services = nativeCosts;
                foreach (var button in go.GetComponentsInChildren<PlayerOpenSkillChestBtn>(true)) button.Services = services;
                foreach (var count in go.GetComponentsInChildren<GachaTodayCountLimitUI>(true)) count.Services = services;
            });
            GameObject Create(string name)
            {
                Transform parent = runtime.Hud.Canvas.transform;
                foreach (JToken ancestor in ((JArray)proof["selections"][name]["original_parent_chain"]).Reverse())
                {
                    var container = new GameObject((string)ancestor["name"], typeof(RectTransform));
                    if (parent == runtime.Hud.Canvas.transform) roots.Add(container);
                    var rect = (RectTransform)container.transform;
                    rect.SetParent(parent, false);
                    var source = ancestor["transform"];
                    Vector2 V2(string field) => new Vector2((float)source[field]["x"], (float)source[field]["y"]);
                    rect.anchorMin = V2("m_AnchorMin"); rect.anchorMax = V2("m_AnchorMax");
                    rect.pivot = V2("m_Pivot"); rect.sizeDelta = V2("m_SizeDelta");
                    rect.anchoredPosition = V2("m_AnchoredPosition");
                    rect.localScale = new Vector3((float)source["m_LocalScale"]["x"], (float)source["m_LocalScale"]["y"], (float)source["m_LocalScale"]["z"]);
                    rect.localRotation = new Quaternion((float)source["m_LocalRotation"]["x"], (float)source["m_LocalRotation"]["y"],
                        (float)source["m_LocalRotation"]["z"], (float)source["m_LocalRotation"]["w"]);
                    parent = rect;
                }
                var instance = factory.Instantiate(LoadGachaControlAsset<GameObject>(proof["roots"][name]), parent, false);
                Assert(instance.GetComponentsInChildren<MonoBehaviour>(true).All(value => value), "all original control scripts resolve");
                return instance;
            }
            GameObject buttons = Create("PetGachaButtons"), dailyRoot = Create("PetGachaDailyCount");
            var mode = buttons.GetComponent<GachaBtnContent>();
            var daily = dailyRoot.GetComponent<GachaTodayCountLimitUI>();
            var label = dailyRoot.GetComponentInChildren<WorldMultiLanguageText>(true);
            var controls = buttons.GetComponentsInChildren<PlayerOpenSkillChestBtn>(true).ToDictionary(value =>
                value.name.Contains("Hundred") ? 100 : value.name.Contains("Ten") ? 10 : 1);
            int costId = runtime.PetGacha.Catalog.Gacha.CostItemId;
            var draws = new List<int>();
            Action<int> draw = count =>
            {
                var result = runtime.PetGacha.Draw(count, "acceptance:gacha-control:" + Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(), confirmHundred: true);
                Assert(result.IsApplied, "actual local draw commits: " + result.Code);
                draws.Add(count);
                quantities.SynchronizeCommittedPlayer();
                services.PublishDraw(count);
            };
            int quickOffers = 0;
            GachaLackItemQuickBuyInfo offer = default;
            foreach (var pair in controls)
                pair.Value.Init(runtime.PetGacha.Catalog.Chest.ClassId, pair.Key, costId, draw,
                    (held, required, info) => { quickOffers++; offer = info; });
            void Balance(long count)
            {
                var result = runtime.Profile.ApplyInventoryTransaction("acceptance:gacha-balance:" + Guid.NewGuid().ToString("N"), save =>
                {
                    var stack = save.Player.Inventory.FirstOrDefault(value => value.ItemId == costId);
                    if (stack == null) save.Player.Inventory.Add(new OfflineItemStack { ItemId = costId, Count = count });
                    else stack.Count = count;
                });
                Assert(result.IsApplied, "isolated material boundary commits");
                quantities.SynchronizeCommittedPlayer();
            }
            Balance(111);
            mode.SetState(GachaBtnContent.BtnMode.TenMode);
            yield return new WaitForSeconds(.6f);
            bool passed = Check("pet-gacha-controls-native", () =>
            {
                Assert(controls.Count == 3 && controls.All(pair => pair.Value.CostMoneyContent.CostItemCount == pair.Key),
                    "all three original chest bindings use the actual configured per-draw cost");
                Assert(controls.Values.All(button => button.CostMoneyContent.CostItemIcon.sprite
                    == LoadGachaControlAsset<Sprite>(proof["cost_sprite"]["pointer"])), "original cost widgets load the actual selected material icon");
                Assert(!controls[100].gameObject.activeSelf && services.Subscribers == 1,
                    "original ten mode hides the hundred button and daily counter subscribes once");
                return "original hierarchy, costs, state animation and daily-count binding are active";
            });
            yield return Capture("pet-gacha-controls-ten-mode.png");
            string before = label.Text;
            ClickPartnerUi(controls[1].CostMoneyContent.Btn);
            passed &= Check("pet-gacha-controls-one", () =>
            {
                Assert(draws.SequenceEqual(new[] { 1 }) && runtime.Profile.InventoryCount(costId) == 110
                    && runtime.PetGacha.DailyCount(DateTime.Now) == 1 && label.Text == before,
                    "one actual draw commits while original non-instant daily display waits for the performer");
                daily.RefreshTodayGachaCount();
                Assert(label.Text != before, "original explicit completion refresh shows committed count");
                return "native single input uses the actual draw transaction and deferred daily refresh";
            });
            typeof(GachaTodayCountLimitUI).GetField("mIsInstantRefresh", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(daily, true);
            before = label.Text;
            ClickPartnerUi(controls[10].CostMoneyContent.Btn);
            passed &= Check("pet-gacha-controls-ten", () =>
            {
                Assert(draws.SequenceEqual(new[] { 1, 10 }) && runtime.Profile.InventoryCount(costId) == 100
                    && runtime.PetGacha.DailyCount(DateTime.Now) == 11 && label.Text != before,
                    "native ten input and instant refresh use committed inventory and daily count");
                return "native ten input commits once and refreshes the original counter";
            });
            mode.SetState(GachaBtnContent.BtnMode.HundredMode);
            yield return new WaitForSeconds(.6f);
            yield return Capture("pet-gacha-controls-hundred-mode.png");
            ClickPartnerUi(controls[100].CostMoneyContent.Btn);
            passed &= Check("pet-gacha-controls-hundred", () =>
            {
                Assert(draws.SequenceEqual(new[] { 1, 10, 100 }) && runtime.Profile.InventoryCount(costId) == 0
                    && runtime.PetGacha.DailyCount(DateTime.Now) == 111, "hundred callback preserves selected open count");
                return "native hundred input commits costs, awards and counts in the actual isolated profile";
            });
            ClickPartnerUi(controls[100].CostMoneyContent.Btn);
            passed &= Check("pet-gacha-controls-insufficient", () =>
            {
                Assert(quickOffers == 1 && offer.ItemId == costId && offer.BuyCount == 100 && offer.GetItemCount == 100
                    && offer.Price == OfflineHomeCurrencyAssets.LoadDefault().QuickBuy(costId).PriceList[0] * 100,
                    "native insufficient cost forwards the actual original quick-buy payload without spending");
                Assert(draws.Count == 3, "insufficient materials never dispatch another draw");
                return "original insufficient-cost branch carries the configured offer and leaves the profile unchanged";
            });
            yield return Capture("pet-gacha-controls-count-after-draws.png");
            dailyRoot.SetActive(false);
            passed &= Check("pet-gacha-controls-release", () =>
            {
                Assert(services.Subscribers == 0, "original disable releases its draw subscription");
                runtime.Profile.Reload();
                Assert(runtime.PetGacha.DailyCount(DateTime.Now) == 111 && runtime.Profile.InventoryCount(costId) == 0,
                    "actual profile reload restores the native button transactions");
                return "original subscription lifetime and actual local count restore are verified";
            });
            foreach (var root in roots) Destroy(root);
            yield return null;
            services.Dispose();
            Destroy(images);
            runtime.Close();
            completed(passed);
#else
            completed(false);
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static T LoadGachaControlAsset<T>(JToken reference) where T : UnityEngine.Object
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath((string)reference["guid"]);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            Assert(asset, "original gacha control asset is bound: " + reference);
            return asset;
        }

        private sealed class GachaControlFixtureServices : IGachaChestButtonServices, IGachaDailyCountServices, IHomeHudResourceLoader, IDisposable
        {
            readonly HomeFeatureRuntime home;
            readonly JObject proof;
            Action<GachaType, int> draw;
            public int Subscribers { get; private set; }
            public GachaControlFixtureServices(HomeFeatureRuntime home, JObject proof) { this.home = home; this.proof = proof; }
            public long GetChestCostItemCount(int chest, int item)
            {
                Assert(chest == home.PetGacha.Catalog.Chest.ClassId, "selected original chest identity");
                return home.PetGacha.Catalog.Chest.CostItems.Single(value => value.ItemId == item).Count;
            }
            public bool TryGetQuickBuyInfo(int item, long count, out GachaLackItemQuickBuyInfo info)
            {
                info = default;
                var assets = OfflineHomeCurrencyAssets.LoadDefault();
                var rule = assets.QuickBuy(item);
                if (rule == null || count <= 0) return false;
                var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
                var player = new OfflineHomeConditionPlayer(configuration, () => home.Profile.Save, home.HasFurnitureForSkin);
                var condition = assets.QuickBuyRow(item)["Condition"];
                if (condition != null && condition.Type != JTokenType.Null && !OfficialPartnerAssetCatalog.LoadDefault().Conditions
                    .IsMet(condition.ToObject<Dictionary<string, object>>(), player)) return false;
                long unit = rule.GetAwardItemCount(home.Profile.Save.Player.Level, home.Profile.Save.Player.SubRank);
                long batches = Math.DivRem(count, unit, out long remainder);
                if (remainder > 0) batches++;
                var counters = new OfflineHomeQuickBuyCounters(assets.CreateLocalQuickBuyCalendar());
                long bought = counters.GetBuyCount(home.Profile.Save.Player, item, rule.RefreshType, DateTime.UtcNow);
                if (!rule.GetPrice(bought, batches, out long price)) return false;
                info = new GachaLackItemQuickBuyInfo { ItemId = item, CurrencyId = rule.CurrencyId, Price = price, BuyCount = batches, GetItemCount = batches * unit };
                return true;
            }
            public long GetGachaDailyCount(GachaClassType type) => home.PetGacha.DailyCount(DateTime.Now);
            public long GetDailyGachaMaxCount(GachaClassType type) => home.PetGacha.Catalog.Settings.DailyLimit;
            public event Action<GachaType, int> OnGacha { add { draw += value; Subscribers++; } remove { draw -= value; Subscribers--; } }
            public void PublishDraw(int count) => draw?.Invoke(GachaType.Pet, count);
            public IHomeHudAssetHandle<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
                => new GachaControlHandle<T>(LoadGachaControlAsset<T>(proof["dynamic_sprites"][guid]["pointer"]));
            public IHomeHudAssetHandle<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => LoadAssetByGuid<T>(guid);
            public void Dispose() { draw = null; }
        }
        private sealed class GachaControlHandle<T> : IHomeHudAssetHandle<T> where T : UnityEngine.Object
        {
            public GachaControlHandle(T asset) { Result = asset; }
            public bool IsValid { get; private set; } = true;
            public int Status => 2;
            public T Result { get; }
            public T WaitForAsyncComplete() => Result;
            public event Action<IHomeHudAssetHandle<T>> Completed { add { value(this); } remove { } }
            public void Dispose() { IsValid = false; }
        }
#endif
    }
}
