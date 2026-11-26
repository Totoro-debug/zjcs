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
using Zjcs.Offline.GachaUI;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyPetGachaControls(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool passed = false;
            var roots = new List<GameObject>();
            var pending = new Stack<IEnumerator>();
            pending.Push(VerifyPetGachaControlSteps(runtime, value => passed = value, roots));
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
                    foreach (var root in roots) if (root) root.SetActive(false);
                    while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
                    foreach (var root in roots) if (root) Destroy(root);
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

        private IEnumerator VerifyPetGachaControlSteps(HomeFeatureRuntime runtime, Action<bool> completed, List<GameObject> roots)
        {
#if UNITY_EDITOR
            var proof = JObject.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("ZJCS_PET_GACHA_CONTROL_FIXTURE")));
            var services = new GachaControlFixtureServices(runtime, proof);
            var nativeCosts = PrivateField<IHomeCurrencyWindowServices>(runtime.Hud, "nativeCurrencyServices");
            var images = ScriptableObject.CreateInstance<Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog>();
            images.Entries = new[] { new Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog.Entry {
                Path = (string)proof["cost_sprite"]["path"], AssetGuid = (string)proof["cost_sprite"]["asset_guid"],
                Asset = LoadGachaControlAsset<Sprite>(proof["cost_sprite"]["pointer"]) } }
                .Concat(((JObject)proof["score_reward_sprites"]).Properties().Select(row =>
                    new Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog.Entry { Path = (string)row.Value["path"],
                        AssetGuid = (string)row.Value["asset_guid"], Asset = LoadGachaControlAsset<Sprite>(row.Value["pointer"]) })).ToArray();
            nativeCosts = new OfflineHomeCurrencyImageServices(nativeCosts, images);
            var quantities = PrivateField<OfflineHomeCurrencyPlayer>(runtime.Hud, "nativeCurrencyPlayer");
            var font = OfflineHomeHudAssets.LoadDefault().defaultFont;
            var texts = (JObject)JObject.Parse(LoadGachaControlAsset<TextAsset>(proof["texts"]).text)["texts"];
            OfflinePetScoreRewardServices scoreServices = null;
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
                foreach (var score in go.GetComponentsInChildren<ScoreRewardUI>(true)) score.Services = scoreServices;
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
                    if (ancestor["safe_area"] is JObject safeAreaFields)
                    {
                        var safeArea = container.AddComponent<Zjcs.Official.HomeScreenUI.SafeArea>();
                        safeArea.PaddingTop = (float)safeAreaFields["PaddingTop"];
                        safeArea.PaddingBottom = (float)safeAreaFields["PaddingBottom"];
                        safeArea.DisableTopAdapt = (bool)safeAreaFields["DisableTopAdapt"];
                        safeArea.DisableBottomAdapt = (bool)safeAreaFields["DisableBottomAdapt"];
                    }
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
            Assert(runtime.Profile.ApplyInventoryTransaction("acceptance:score-single-map14-lock",
                save => save.Player.UnlockedMapClassIds.Remove(14)).IsApplied, "isolated original map14 lock fixture");
            var ruleConfiguration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
            var conditionPlayer = new OfflineHomeConditionPlayer(ruleConfiguration, () => runtime.Profile.Save, runtime.HasFurnitureForSkin);
            var scoreConfiguration = new OfflineScoreRewardConfiguration(ruleConfiguration, OfficialPartnerAssetCatalog.LoadDefault().Conditions, () => conditionPlayer);
            var scorePresentation = new ScoreRewardFixturePresentation(runtime, proof);
            using (scoreServices = new OfflinePetScoreRewardServices(runtime.PetGacha,
                scoreConfiguration.BatchAchievementTypes, () => scoreConfiguration.IsBatchLimited, scorePresentation))
            {
                foreach (var expansion in ((JObject)proof["mesh_bindings"]).Properties().Select(row => row.Value))
                {
                    var mesh = LoadGachaControlAsset<Mesh>(expansion["pointer"]);
                    Assert(mesh.vertexCount == (int)expansion["vertices"] && mesh.triangles.Length == (int)expansion["indices"],
                        "original UI mesh loads the source vertex and triangle counts");
                    using var vertices = new MemoryStream();
                    using var indices = new MemoryStream();
                    using var vertexWriter = new BinaryWriter(vertices);
                    using var indexWriter = new BinaryWriter(indices);
                    var positions = mesh.vertices; var uv = mesh.uv;
                    for (int index = 0; index < positions.Length; index++)
                    { vertexWriter.Write(positions[index].x); vertexWriter.Write(positions[index].y); vertexWriter.Write(positions[index].z); vertexWriter.Write(uv[index].x); vertexWriter.Write(uv[index].y); }
                    foreach (int index in mesh.triangles) indexWriter.Write((ushort)index);
                    string Hash(byte[] data) => BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                    Assert(Hash(vertices.ToArray()) == (string)expansion["vertex_stream_sha256"] && Hash(indices.ToArray()) == (string)expansion["index_stream_sha256"],
                        "actual Unity mesh vertices, UVs and triangles match independently decoded original packed samples");
                }
                GameObject scoreRoot = Create("PetGachaScoreReward");
                var rewardAttachments = Resources.Load<Zjcs.Offline.ProfessionUI.OfflineProfessionImageCatalog>("Home/OfflineHomeTaskRewardAttachments");
                var iconImages = HomeFeatureRuntime.BuildPartnerImageCatalog(images.Entries,
                    OfficialPartnerProfileAssetCatalog.Load().Common.Images.Entries, rewardAttachments.Entries);
                var iconLoader = new Zjcs.Offline.ProfessionUI.OfflineProfessionImageResourceLoader(iconImages);
                using var scoreScope = OfflineUiBindingModule.Register(scoreRoot.transform, new OfflineUiBindings {
                    Font = () => font, Images = _ => iconLoader,
                    Pool = (scope, prefab, parent, keepWorld) => UnityEngine.Object.Instantiate(prefab, parent, keepWorld)
                });
                var scoreUI = scoreRoot.GetComponent<ScoreRewardUI>();
                var key = new ScoreRewardKey(runtime.PetGacha.Catalog.Gacha.ScoreAchievementType, 16);
                var rule = new GachaTextKeyArgWrap(runtime.PetGacha.Catalog.Gacha.ScoreRuleText,
                    runtime.PetGacha.Catalog.Chest.Rewards[0].Count);
                scoreUI.Init(key, rule);
                int blackboardNotifications = 0;
                var recordedIndices = new List<int>();
                scoreServices.OnRecordAlreadyGotAchievementReward += index => recordedIndices.Add(index);
                scoreServices.OnUpdateBlackboardData += data => { if (data.BlackboardDataType == 45 && data.ParamsKey == "ALL") blackboardNotifications++; };
                passed &= Check("pet-gacha-controls-score-initial", () =>
                {
                    Assert(scoreServices.IsScoreRewardBatchGetLimited && scoreServices.CanBatchGetScoreReward(key), "original map14 batch gate and configured score type");
                    Assert(scoreUI.AchievementInfo.Index == 0 && scoreUI.StateSwitcher.State && scoreUI.ProgressImage.fillAmount == 1,
                        "111 actual draws reach the first original score target");
                    var update = typeof(ScoreRewardUI).GetMethod("MainPlayer_OnUpdateBlackboardData", BindingFlags.Instance | BindingFlags.NonPublic);
                    int beforeIcons = scorePresentation.IconRequests;
                    update.Invoke(scoreUI, new object[] { new ScoreBlackboardKey(44, "ALL") });
                    update.Invoke(scoreUI, new object[] { new ScoreBlackboardKey(45, "other") });
                    Assert(scorePresentation.IconRequests == beforeIcons, "other original blackboard type or parameter cannot refresh score rewards");
                    update.Invoke(scoreUI, new object[] { ScoreBlackboardKey.PetGachaScoreCount });
                    Assert(scorePresentation.IconRequests == beforeIcons + 1, "matching original blackboard key refreshes its native reward");
                    return "complete native score subtree shows the real first reward and original ready state";
                });
                yield return Capture("pet-gacha-controls-score-ready.png");
                int firstItem = runtime.PetGacha.GetAchievement(16, 0).Rewards[0].ItemId;
                long firstBefore = runtime.Profile.InventoryCount(firstItem);
                ClickPartnerUi(scoreUI.GetScoreRewardBtn);
                passed &= Check("pet-gacha-controls-score-single", () =>
                {
                    Assert(runtime.PetGacha.GetAchievement(16, 0).IsClaimed && !runtime.PetGacha.GetAchievement(16, 1).IsClaimed
                        && runtime.Profile.InventoryCount(firstItem) == firstBefore + 1 && scorePresentation.Commits == 1,
                        "locked batch sends exactly Count1/ReceiveSameTypefalse and grants the original item");
                    Assert(scoreUI.AchievementInfo.Index == 1 && scoreUI.StateSwitcher.State, "single claim advances to the next available original stage");
                    Assert(recordedIndices.SequenceEqual(new[] { 0 }), "original single-claim event carries the reward index");
                    return "native single-claim input commits one original reward and selects the next stage";
                });
                var unlock = runtime.Profile.ApplyInventoryTransaction("acceptance:score-batch-map14", save =>
                {
                    if (!save.Player.UnlockedMapClassIds.Contains(14)) save.Player.UnlockedMapClassIds.Add(14);
                    save.Player.PetGacha.Score = Math.Max(save.Player.PetGacha.Score, 260);
                });
                Assert(unlock.IsApplied, "isolated original map unlock and score fixture commit: " + unlock.Status + " " + unlock.Message);
                scoreServices.Synchronize();
                var ready = runtime.PetGacha.Achievements(16, 0, 20).Where(row => row.IsReady && !row.IsClaimed).ToArray();
                var expected = ready.SelectMany(row => row.Rewards).GroupBy(row => row.ItemId)
                    .ToDictionary(group => group.Key, group => runtime.Profile.InventoryCount(group.Key) + group.Sum(row => row.Count));
                int receiptsBeforeBatch = runtime.Profile.Save.Player.AppliedInventoryEventIds.Count;
                ClickPartnerUi(scoreUI.GetScoreRewardBtn);
                passed &= Check("pet-gacha-controls-score-batch", () =>
                {
                    Assert(!scoreServices.IsScoreRewardBatchGetLimited && ready.Length > 1 && scorePresentation.Commits == 2,
                        "original map14 unlock enables the actual single-group batch request");
                    Assert(blackboardNotifications == 1, "local score changes publish the original blackboard identity");
                    Assert(recordedIndices.SequenceEqual(new[] { 0 }.Concat(ready.Select(row => row.Index))), "original batch notifications preserve ascending reward indices");
                    Assert(ready.All(row => runtime.PetGacha.GetAchievement(16, row.Index).IsClaimed)
                        && expected.All(row => runtime.Profile.InventoryCount(row.Key) == row.Value)
                        && runtime.Profile.Save.Player.AppliedInventoryEventIds.Count == receiptsBeforeBatch + 1,
                        "one transaction receipt contains every configured available original reward and claim record");
                    var next = runtime.PetGacha.GetAchievement(16, scoreUI.AchievementInfo.Index);
                    long previousTarget = runtime.PetGacha.GetAchievement(16, next.Index - 1).Target;
                    Assert(!scoreUI.StateSwitcher.State && Mathf.Approximately(scoreUI.ProgressImage.fillAmount,
                        (float)(next.CurrentValue - previousTarget) / (next.Target - previousTarget)), "next stage subtracts the previous score target");
                    return "batch commits original rewards atomically and renders progress within the next stage";
                });
                yield return Capture("pet-gacha-controls-score-next-stage.png");
                int claimsBefore = runtime.PetGacha.State.AchievementClaims.Count;
                Assert(!scoreUI.GetScoreRewardBtn.gameObject.activeInHierarchy,
                    "original unready state hides the native claim input");
                scoreUI.GetScoreRewardBtn.onClick.Invoke();
                scoreUI.SetAdditionInfoData(() => true, new GachaTextKeyArgWrap("common_format_args1", "score-tip"));
                Assert(scoreRoot.GetComponentInChildren<AdditionInfoTip>(true).gameObject.activeSelf, "original additional tip predicate shows its native text");
                scoreUI.ClearAdditionInfoData();
                Assert(!scoreRoot.GetComponentInChildren<AdditionInfoTip>(true).gameObject.activeSelf, "original clear hides the native additional tip");
                typeof(ScoreRewardUI).GetField("mAchievementInfo", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(scoreUI, scoreServices.GetAchievementInfoByGroupAndIndex(16, 0));
                typeof(ScoreRewardUI).GetMethod("RefreshReward", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(scoreUI, null);
                Assert(scoreUI.ScoreText.Text == runtime.PetGacha.State.Score.ToString() && scoreUI.ProgressImage.fillAmount == 1,
                    "original completed selected-reward branch shows total score and retains the claimed overlay fill order");
                Assert(scoreUI.RewardSlot.GetComponentInChildren<Zjcs.Official.ProfessionUI.ItemIcon>()
                    .GetAttachment(Zjcs.Official.ProfessionUI.ItemIconAttachmentType.GotTip).gameObject.activeSelf,
                    "original completed reward attaches the native claimed indicator");
                scoreUI.RefreshInfo();
                ClickPartnerUi(scoreUI.ScoreRewardInfoBtn);
                scoreRoot.SetActive(false);
                yield return null;
                int iconRequestsAfterDisable = scorePresentation.IconRequests;
                Assert(runtime.Profile.ApplyInventoryTransaction("acceptance:score-after-disable",
                    save => save.Player.PetGacha.Score++).IsApplied, "isolated score notification after closing");
                scoreServices.Synchronize();
                runtime.Profile.Reload();
                passed &= Check("pet-gacha-controls-score-restore", () =>
                {
                    Assert(scorePresentation.Commits == 2 && scorePresentation.LastMessage == "AchievementPanel.CanNotGetReward"
                        && runtime.PetGacha.State.AchievementClaims.Count == claimsBefore && expected.All(row => runtime.Profile.InventoryCount(row.Key) == row.Value),
                        "unready click cannot grant twice and reload preserves actual reward records and inventory");
                    Assert(scorePresentation.IconRequests == iconRequestsAfterDisable,
                        "disabled native score UI no longer refreshes on local score notifications");
                    Assert(scorePresentation.DetailKey.GroupId == key.GroupId && scorePresentation.DetailRule.Key == rule.Key
                        && scorePresentation.DetailRule.Args.SequenceEqual(rule.Args)
                        && scoreUI.RewardSlot.GetComponentsInChildren<Zjcs.Official.ProfessionUI.ItemIcon>(true).All(icon => !icon.gameObject.activeSelf),
                        "detail request carries original key/rule and disable recycles native reward icons");
                    return "claim guard, additional tip, detail request, native icon release and profile restore verified; full detail modal remains pending";
                });
                Destroy(iconImages);
            }
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
        private sealed class ScoreRewardFixturePresentation : IOfflineScoreRewardPresentation
        {
            readonly HomeFeatureRuntime home;
            readonly JObject proof;
            public int Commits;
            public int IconRequests;
            public string LastMessage;
            public ScoreRewardKey DetailKey;
            public GachaTextKeyArgWrap DetailRule;
            public ScoreRewardFixturePresentation(HomeFeatureRuntime home, JObject proof) { this.home = home; this.proof = proof; }
            public Zjcs.Official.ProfessionUI.ItemIcon GetIcon(int item, Transform parent)
            {
                IconRequests++;
                var icon = home.PartnerGiftBindings.CreateGiftIcon(item, parent, OfficialPartnerProfileAssetCatalog.Load().RewardIconPrefab);
                if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
                Assert(icon.IconSprite == LoadGachaControlAsset<Sprite>(proof["score_reward_sprites"][item.ToString()]["pointer"]),
                    "native default ItemIcon loads the exact original score reward sprite");
                return icon;
            }
            public void RecycleIcon(Zjcs.Official.ProfessionUI.ItemIcon icon)
            { home.PartnerGiftBindings.RecycleIcon(icon); icon.gameObject.SetActive(false); Destroy(icon.gameObject); }
            public void ShowSystemMessage(string key) { LastMessage = key; }
            public void ShowFailure(PetGachaResult result) => throw new InvalidOperationException(result.Message);
            public void OnAchievementCommitted() { Commits++; }
            public void PopupScoreRewardDialog(string path, bool completed, ScoreRewardKey key, GachaTextKeyArgWrap rule)
            { Assert(string.IsNullOrEmpty(path) && !completed, "original default detail request"); DetailKey = key; DetailRule = rule; }
        }

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
