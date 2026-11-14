using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;
using OriginalSubRank = Zjcs.Official.HomeExperienceRules.SubRank;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using OutputProp = Zjcs.Official.HomeOutputAddition.OutputAdditionPropType;
using OutputGroup = Zjcs.Official.HomeOutputAddition.OutputGroupType;

namespace Zjcs.Presentation.Home
{
    // The original controller consumes the real home/profile owners through this
    // local request/response boundary. Presentation never grants a second reward.
    public sealed class OfflineHomeBedServices : IHomeHangUpWindowServices, IHomeHangUpWindowPlayer, IDisposable
    {
        sealed class Response : IHomeWindowAwardMessage
        {
            internal readonly ArrayList Rewards = new ArrayList();
            public IList DataList => Rewards;
            public object SourceData => this;
        }
        sealed class Subscription : IDisposable
        {
            Action release;
            internal Subscription(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        readonly HomeFeatureRuntime home;
        readonly OfficialCurrentHomeRuleAssetCatalog configuration;
        readonly OfficialHomeHangUpWindowAssetCatalog assets;
        readonly OfficialHomeRepairAssetCatalog repair;
        readonly HomeExperienceTables experience;
        readonly Func<int?, bool> outputDisabled;
        readonly Action<string> message;
        readonly Action<OfflineHomeCollectedReward> popupAward;
        readonly Func<string, string> outputText;
        readonly Func<int, int, bool> mapPublic;
        readonly Dictionary<object, List<Action>> conditions = new Dictionary<object, List<Action>>();
        readonly List<OfflineHomeCollectedReward> pending = new List<OfflineHomeCollectedReward>();
        readonly OfflineProfessionImageCatalog imageCatalog;
        Action outputFunctionChanged;
        Action<object> newDay;
        float nextPoll;
        int observedLevel;
        long observedDay;
        bool observedDisabled, disposed;
        public OfflineHomeHangUpWindows Windows { private get; set; }
        public Action<int, GameObject> OpenFastWindow;
        public Action<int, bool, Dictionary<int, long>, Action> FastOperation;
        public Action<HomeWindowText, HomeWindowText?, Action> ConfirmWindow;
        public Action<OutputGroupType> OpenOutputWindow;
        public Action OpenBlessingWindow;
        public OfflineHomeFurnitureOutput Output { get; }
        public OfflineHomeOutputAddition OutputAddition { get; }

        public OfflineHomeBedServices(HomeFeatureRuntime home, Func<int?, bool> outputDisabled,
            Action<string> message, Action<OfflineHomeCollectedReward> popupAward,
            Func<string, string> outputText, Func<int, int, bool> mapPublic)
        {
            this.home = home ?? throw new ArgumentNullException(nameof(home));
            this.outputDisabled = outputDisabled ?? throw new ArgumentNullException(nameof(outputDisabled));
            this.message = message ?? throw new ArgumentNullException(nameof(message));
            this.popupAward = popupAward ?? throw new ArgumentNullException(nameof(popupAward));
            this.outputText = outputText ?? throw new ArgumentNullException(nameof(outputText));
            this.mapPublic = mapPublic ?? throw new ArgumentNullException(nameof(mapPublic));
            configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
            assets = OfficialHomeHangUpWindowAssetCatalog.LoadDefault();
            repair = OfficialHomeRepairAssetCatalog.LoadDefault();
            experience = OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
            Output = new OfflineHomeFurnitureOutput(home.Rules, home.HomeIncomeProperties,
                GetHangUpLevel, home.FormatTopBarCurrency, outputText);
            Output.Refresh();
            OutputAddition = new OfflineHomeOutputAddition(home.Session, () => DateTime.UtcNow, outputDisabled);
            OutputAddition.Changed += OnOutputChanged;
            imageCatalog = ScriptableObject.CreateInstance<OfflineProfessionImageCatalog>();
            imageCatalog.Entries = new[] {
                new OfflineProfessionImageCatalog.Entry { Path = assets.bedRewardImagePath,
                    AssetGuid = "home-native-bed-xp", Asset = assets.bedRewardImage },
                new OfflineProfessionImageCatalog.Entry { Path = GetFurnitureIconPrefabPath(EFurniture.Bed),
                    AssetGuid = "home-native-bed-icon", Asset = repair.IconPrefab(EFurniture.Bed).gameObject }
            };
            Resources = new OfflineProfessionImageResourceLoader(imageCatalog);
            observedLevel = Level;
            observedDay = OfflineRefreshClock.DayStart(DateTime.Now).Ticks;
            observedDisabled = IsOutputAdditionDisabled;
        }
        public void Tick()
        {
            if (disposed || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .2f;
            if (observedLevel != Level)
            {
                observedLevel = Level; Output.Refresh();
                foreach (var callback in conditions.Values.SelectMany(value => value).ToArray()) callback();
            }
            long day = OfflineRefreshClock.DayStart(DateTime.Now).Ticks;
            if (day != observedDay) { observedDay = day; newDay?.Invoke(null); }
            bool disabled = IsOutputAdditionDisabled;
            if (disabled != observedDisabled) { observedDisabled = disabled; outputFunctionChanged?.Invoke(); }
        }
        void OnOutputChanged() => OnAddOutputAdditionData?.Invoke(null);
        public IHomeHangUpWindowPlayer Player => this;
        public IProfessionImageResourceLoader Resources { get; }
        public int HomeBedSpeedConfirmFunctionId => 163;
        public int PlayerExpItemId => 50;
        public int Level => home.Profile.Save.Player.Level;
        public int SubRank => home.Profile.Save.Player.SubRank;
        public IReadOnlyDictionary<PropType, long> NonBattleProps => Output.NonBattleProps;
        public IHomeWindowPropertyCenter PlayerPropCenter => Output.PropertyCenter;
        public int GetHangUpLevel(int id) => home.Rules.HangUp(id).useBlessingLevel
            ? home.BlessingLevel : home.Session.HangUpLevel(id);
        public int GetBlessingLevel() => home.BlessingLevel;
        public int GetLostGoddessActiveCount()
        {
            var regions = configuration.Rows("map_region");
            return home.Profile.Save.World.ActivatedGoddessRegions.Where(value => value != null)
                .GroupBy(value => (value.MapClassId, value.Region)).Count(group => regions.Any(row =>
                    Number(row, "ClassId") == group.Key.MapClassId && Number(row, "Region") == group.Key.Region
                    && row["GoddessActivePropAwardDict"] is Dictionary<string, object> awards && awards.Count > 0));
        }
        public bool CanFreeFastHangUp(int id) => home.Session.CanFreeFastForward(id);
        public bool CanGetExp() => home.CanGetHangUpExperience(experience);
        public bool IsFunctionLimit(int id) => home.Rules.IsFunctionLimited(id);
        public bool GetHangUpQuickReceiveState(EFurniture type) => home.GetHangUpQuickReceiveState(type);
        public int GetOutputAdditionPropValue(int type) => OutputAddition.PropertyValue((OutputProp)type);
        public bool IsMapRegionPublic(int classId, int region)
            => mapPublic(classId, region);
        public event Action<int> OnFastHangUp;
        public event Action<object> OnAddOutputAdditionData;
        public HomeWindowHangUpInfo GetHangUpInfo(int id) => Output.GetHangUpInfo(id);
        public bool TryGetMainHangUpInfo(EFurniture type, out HomeWindowHangUpInfo info)
        {
            var row = home.Rules.HangUpsOf((int)type).FirstOrDefault(value => value.needRecordData);
            info = row == null ? null : GetHangUpInfo(row.id);
            return info != null;
        }
        public HomeWindowFastHangUpInfo GetFastHangUpInfo(int id)
        {
            var row = home.Rules.FastHangUp(id) ?? throw new InvalidOperationException("Selected fast income row is unavailable: " + id);
            return new HomeWindowFastHangUpInfo { FastHangUpId = id, AutoReceive = row.autoReceive,
                FastHangUpItemDic = row.items?.ToDictionary(value => value.itemId, value => checked((int)value.hours)),
                HangUpIdList = row.hangUpIdList?.ToList() };
        }
        public HomeWindowFurnitureInfo GetFurnitureInfo(EFurniture type)
            => new HomeWindowFurnitureInfo { ClassId = repair.FurnitureItemId(type) };
        public HomeWindowFunctionLimitInfo GetFunctionLimitInfo(int id)
        {
            var row = home.Rules.Data.functionLimits.FirstOrDefault(value => value.type == id && value.key0 == 0 && value.key1 == 0);
            return row == null ? null : new HomeWindowFunctionLimitInfo { Condition = row.condition };
        }
        public string GetFurnitureTypeNameKey(EFurniture type) => repair.FurnitureTypeNameKey(type);
        public string GetFurnitureIconPrefabPath(EFurniture type) => "UI/Home/Furniture/" + type;
        public string GetItemNameKey(int id) => "item_" + id + "_name";
        public string GetItemDescKey(int id) => "item_" + id + "_desc";
        public string GetHangUpBaseOutput(int id) => Output.GetHangUpBaseOutput(id);
        public string Text(string key)
        {
            string text = assets.Text(key);
            return text == key ? outputText(key) : text;
        }
        public string TextFormatted(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, Text(key), args);
        public string FormatCount(long count) => home.FormatTopBarCurrency(count);
        public double GetHangUpReward(int id, double hours, int level = -1, bool isBase = false)
            => Output.GetHangUpReward(id, hours, level, isBase);
        public long GetHangUpMaxRewardStore(int id, int level = -1) => Output.GetHangUpMaxRewardStore(id, level);
        long Unsettled(int id, out double remainder)
        {
            remainder = 0;
            if (!home.Session.Definitions.Any(value => value.HangUpId == id && value.Unlocked)) return 0;
            var saved = home.Session.Save.Income.FirstOrDefault(value => value.HangUpId == id);
            double elapsed = home.Session.PendingIncomeSeconds(DateTimeOffset.UtcNow);
            return Output.GetUnsettledRewardCount(id, elapsed, saved?.StoredSeconds ?? 0, 0, out remainder);
        }
        public long GetHangUpAwardCount(int id, out double seconds)
            => Zjcs.Official.HomeIncomeRules.HangUpUtils.ClampAwardCount(
                home.Session.Save.Income.FirstOrDefault(value => value.HangUpId == id)?.SettledCount ?? 0,
                Unsettled(id, out seconds), GetHangUpMaxRewardStore(id));
        public long CalcOutputAdditionExtStore(int id, long capacity) => OutputAddition.ExtraCapacity(id, capacity);
        public long CalcOutputAdditionUnsettleExtRewardCount(int id)
        { long count = Unsettled(id, out _); return count <= 0 ? 0 : OutputAddition.ExtraCount(id, count); }
        public double CalcOutputAdditionExtCount(int id, double count) => OutputAddition.ExtraCount(id, count);
        public bool CanGetHangUpReward(int id) => home.Session.Definitions.Any(value => value.HangUpId == id && value.Unlocked)
            && ((home.Session.Save.Income.FirstOrDefault(value => value.HangUpId == id)?.SettledCount ?? 0) > 0 || Unsettled(id, out _) > 0);
        public HomeWindowItemInfo GetHangUpRewardItem(int id)
            => id == 1 ? new HomeWindowItemInfo { Icon = assets.bedRewardImagePath }
                : throw new NotSupportedException("Bind the selected cart item before opening it from the bed services.");
        public double GetProp(IReadOnlyDictionary<PropType, long> props, PropType type) => Output.GetProp(props, type);
        public void GetHangUpProp(HomeWindowHangUpInfo info, IReadOnlyDictionary<PropType, long> props, out double addPercent, out double addFixed)
            => Output.GetHangUpProp(info, props, out addPercent, out addFixed);
        public int GetRankHighSubRank(int subRank) => (int)((Zjcs.Official.HomeExperienceRules.SubRank)subRank).ToRank().GetHighSubRank();
        // Confirmed offline policy removes server/world rank gates; the original
        // controller still checks the personal level and displays its confirmation.
        public bool IsRankLimited(int subRank) => false;
        public void GetMaxPlayerLevel(int subRank, out int baseLevel, out int blessLevel)
        {
            var value = (Zjcs.Official.HomeExperienceRules.SubRank)subRank;
            baseLevel = experience.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(value);
            blessLevel = experience.SystemLevelLimitInfoParser.GetPlayerBlessLevelLimit(value.ToRank());
        }
        public bool TryGetFirstMainMapRegion(int subRank, out HomeWindowMapRegionInfo region)
        {
            var rank = ((Zjcs.Official.HomeExperienceRules.SubRank)subRank).ToRank();
            var row = configuration.Rows("map_region").FirstOrDefault(value => Number(value, "RegionTag") == 1
                && Number(value, "Region") == 0 && ((Zjcs.Official.HomeExperienceRules.SubRank)Number(value, "SubRank")).ToRank() == rank
                && Number(value, "SubRank") == (int)LowSubRank(rank));
            region = row == null ? null : new HomeWindowMapRegionInfo { ClassId = Number(row, "ClassId"), Region = Number(row, "Region") };
            return region != null;
        }
        public bool TryGetFastHangUpTip(int id, long hours, out int itemId, out HomeWindowText title, out HomeWindowText content)
        {
            itemId = -1; title = default; content = default;
            var definition = home.Session.Definitions.FirstOrDefault(value => value.HangUpId == id);
            if (definition == null || !definition.Unlocked || home.Rules.HangUp(id).rewardType != 0) return false;
            itemId = definition.OutputItemIds?.FirstOrDefault() ?? definition.OutputItemId;
            long count = (long)(GetHangUpReward(id, 1) * hours) + GetHangUpAwardCount(id, out _);
            if (count <= GetHangUpMaxRewardStore(id)) return false;
            title = new HomeWindowText(itemId == PlayerExpItemId ? "ui_fast_hang_up_exp_exceed_tip_title" : "ui_fast_hang_up_exceed_tip_title");
            content = new HomeWindowText(itemId == PlayerExpItemId ? "ui_fast_hang_up_exp_exceed_tip_content" : "ui_fast_hang_up_exceed_tip_content");
            return true;
        }
        public int GetTipLevel(EFurniture type) => home.HangUpHud.TipLevel(type);
        public IDisposable SubscribeTips(Action<HangUpTipLevelChangeData> callback) => home.HangUpHud.SubscribeHangUpTips(callback);
        public void RegisterCondition(object condition, Action callback)
        {
            if (!conditions.TryGetValue(condition, out var callbacks)) conditions.Add(condition, callbacks = new List<Action>());
            callbacks.Add(callback);
        }
        public void DeregisterCondition(object condition, Action callback)
        { if (conditions.TryGetValue(condition, out var callbacks)) { callbacks.Remove(callback); if (callbacks.Count == 0) conditions.Remove(condition); } }
        public void SwitchQuickReceive(EFurniture type, bool enabled, Action<object, int> onError)
        { if (!home.SetHangUpQuickReceiveState(type, enabled)) onError(null, -1); }
        public void Collect(IReadOnlyList<int> ids, Action<IHomeWindowAwardMessage> onSuccess)
        {
            var response = new Response();
            foreach (int id in ids)
            {
                if (id != 1) throw new NotSupportedException("The bed collection owner consumes its selected XP entry.");
                var reward = home.PrepareHomeCollection(id);
                if (reward != null) { pending.Add(reward); response.Rewards.Add(reward); }
            }
            onSuccess(response);
        }
        public void Award(IList data, object source)
        {
            if (!(source is Response response) || !ReferenceEquals(data, response.Rewards))
                throw new InvalidOperationException("A committed local bed response is required.");
            home.AwardPreparedHomeCollections();
        }
        public void MergeAward(IList data) { foreach (OfflineHomeCollectedReward reward in data) if (!pending.Contains(reward)) throw new InvalidOperationException("Unknown bed receipt."); }
        public void PopupAwardList(IList data) { foreach (OfflineHomeCollectedReward reward in data) popupAward(reward); }
        public void PublishCollected()
        { var published = pending.ToArray(); pending.Clear(); foreach (var reward in published) home.PublishHomeCollection(reward); Output.Refresh(); }
        public void SystemMessage(string text) => message(text);
        public void Confirm(string key, Action confirmed) => ConfirmWindow(new HomeWindowText(key), null, confirmed);
        public void Confirm(HomeWindowText key, HomeWindowText title, Action confirmed) => ConfirmWindow(key, title, confirmed);
        public void OpenFastHangUp(int id, GameObject parent) => OpenFastWindow(id, parent);
        public void Close(GameObject window) => Windows.Close(window);
        public void OperFastHangUp(int id, bool autoReceive, Dictionary<int, long> itemCost, Action onComplete)
            => FastOperation(id, autoReceive, itemCost, () => { OnFastHangUp?.Invoke(id); onComplete?.Invoke(); });
        public IDisposable SubscribeOutputAdditionFunctionState(Action callback)
        { outputFunctionChanged += callback; return new Subscription(() => outputFunctionChanged -= callback); }
        public IDisposable SubscribeNewDay(Action<object> callback)
        { newDay += callback; return new Subscription(() => newDay -= callback); }
        public bool IsOutputAdditionDisabled => outputDisabled(null);
        public List<(List<HomeWindowOutputAdditionInfo>, DateTime)> GetCurrOutputGroupPropList(OutputGroupType type)
            => OutputAddition.Groups((OutputGroup)type).Select(group => (group.propList.Select(row =>
                new HomeWindowOutputAdditionInfo { OutputAdditionPropType = (int)row.OutputAdditionPropType }).ToList(), group.endTime)).ToList();
        public void OpenOutputAddition(OutputGroupType type) => OpenOutputWindow(type);
        public void OpenBlessingRule() => OpenBlessingWindow();
        static int Number(IReadOnlyDictionary<string, object> row, string key) => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);
	static OriginalSubRank LowSubRank(Rank rank)
	{
		switch (rank)
		{
		case Rank.None:
			return OriginalSubRank.None;
		case Rank.Norank:
			return OriginalSubRank.Norank;
		case Rank.Blackiron:
			return OriginalSubRank.Blackiron1;
		case Rank.Bronze:
			return OriginalSubRank.Bronze1;
		case Rank.Silver:
			return OriginalSubRank.Silver1;
		case Rank.Gold:
			return OriginalSubRank.Gold1;
		case Rank.Saint:
			return OriginalSubRank.Saint1;
		case Rank.Legend:
			return OriginalSubRank.Legend1;
		case Rank.Angel:
			return OriginalSubRank.Angel1;
		case Rank.Godtouched1:
			return OriginalSubRank.Godtouched1_1;
		case Rank.Godtouched2:
			return OriginalSubRank.Godtouched2_1;
		case Rank.Godtouched3:
			return OriginalSubRank.Godtouched3_1;
		case Rank.Demigod1:
			return OriginalSubRank.Demigod1_1;
		case Rank.Demigod2:
			return OriginalSubRank.Demigod2_1;
		case Rank.Demigod3:
			return OriginalSubRank.Demigod3_1;
		case Rank.Stargod1:
			return OriginalSubRank.Stargod1_1;
		case Rank.Stargod2:
			return OriginalSubRank.Stargod2_1;
		case Rank.Stargod3:
			return OriginalSubRank.Stargod3_1;
		case Rank.Lordgod1:
			return OriginalSubRank.Lordgod1_1;
		case Rank.Lordgod2:
			return OriginalSubRank.Lordgod2_1;
		case Rank.Lordgod3:
			return OriginalSubRank.Lordgod3_1;
		case Rank.Eternal1:
			return OriginalSubRank.Eternal1_1;
		case Rank.Eternal2:
			return OriginalSubRank.Eternal2_1;
		case Rank.Eternal3:
			return OriginalSubRank.Eternal3_1;
		case Rank.Nihility1:
			return OriginalSubRank.Nihility1_1;
		case Rank.Nihility2:
			return OriginalSubRank.Nihility2_1;
		case Rank.Nihility3:
			return OriginalSubRank.Nihility3_1;
		case Rank.Chaos1:
			return OriginalSubRank.Chaos1_1;
		case Rank.Chaos2:
			return OriginalSubRank.Chaos2_1;
		case Rank.Chaos3:
			return OriginalSubRank.Chaos3_1;
		default:
		{
			UnityEngine.Debug.LogError("GetLowSubRank Unsupported Rank:" + rank);
			return OriginalSubRank.None;
		}
		}
	}

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            OutputAddition.Changed -= OnOutputChanged;
            conditions.Clear(); pending.Clear();
            outputFunctionChanged = null; newDay = null; OnFastHangUp = null; OnAddOutputAdditionData = null;
            Object.Destroy(imageCatalog);
        }
    }
}
