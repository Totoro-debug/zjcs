using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.ProfessionUI;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using Formula = Zjcs.Official.HomeIncomeRules.BattleFormulaHandler;
using FormulaProp = Zjcs.Official.HomeIncomeRules.PropType;
using FormulaInfo = Zjcs.Official.HomeIncomeRules.HangUpInfo;

namespace Zjcs.Offline.HomeUI
{
    // A read-only projection of the same committed rules/property chunks used by
    // the home ledger. The original FurnitureOutputInfo owns every displayed row.
    public sealed class OfflineHomeFurnitureOutput : IHomeFurnitureOutputServices, IHomeWindowPropertyCenter
    {
        readonly OfficialHomeRules rules;
        readonly Func<CharacterPropSnapshot> readProperties;
        readonly Func<int, int> readLevel;
        readonly Func<long, string> format;
        readonly Func<string, string> localize;
        readonly Formula formula = new Formula(Zjcs.Official.HomeIncomeRules.HomeIncomePropertyCatalog.Get);
        CharacterPropSnapshot snapshot;
        IReadOnlyDictionary<PropType, long> nonBattle;

        public OfflineHomeFurnitureOutput(OfficialHomeRules rules, Func<CharacterPropSnapshot> readProperties,
            Func<int, int> readLevel, Func<long, string> format, Func<string, string> localize)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.readProperties = readProperties ?? throw new ArgumentNullException(nameof(readProperties));
            this.readLevel = readLevel ?? throw new ArgumentNullException(nameof(readLevel));
            this.format = format ?? throw new ArgumentNullException(nameof(format));
            this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
        }
        public void Refresh()
        {
            snapshot = readProperties() ?? throw new InvalidOperationException("Home property snapshot is unavailable.");
            if (snapshot.NonBattleProps == null || snapshot.Chunks == null)
                throw new InvalidOperationException("The original home output requires complete property chunks.");
            nonBattle = Convert(snapshot.NonBattleProps);
        }
        public IHomeWindowPropertyCenter PropertyCenter => this;
        public IReadOnlyDictionary<PropType, long> NonBattleProps => nonBattle
            ?? throw new InvalidOperationException("Refresh the actual home properties before showing output.");
        public HomeWindowHangUpInfo GetHangUpInfo(int id)
        {
            var row = Row(id);
            return new HomeWindowHangUpInfo { Id = row.id, FastHangUpId = row.fastHangUpId,
                BindFurniture = (Zjcs.Official.HomeUI.EFurniture)row.bindFurniture,
                EffectFixedPropType = (PropType)row.effectFixedProp,
                EffectPercentPropType = (PropType)row.effectPercentProp, MaxStorePropType = (PropType)row.maxStoreProp };
        }
        HomeHangUpRecord Row(int id) => rules.HangUp(id)
            ?? throw new InvalidOperationException("Selected home income configuration is unavailable: " + id);
        public string GetHangUpBaseOutput(int id) => "hang_up_" + id + "_base_output";
        public string FormatCount(long count) => format(count);
        public string TextFormatted(string key, params object[] args)
            => string.Format(CultureInfo.CurrentCulture, localize(key), args);
        public double GetHangUpReward(int id, double hours, int level = -1, bool isBase = false)
        {
            var row = Row(id);
            if (level < 0) level = readLevel(id);
            double levelNum = rules.LevelNumber(row.levelRuleId, level);
            if (levelNum <= 0) return 0;
            var reward = rules.HangUpReward(row.rewardId)
                ?? throw new InvalidOperationException("Selected home reward configuration is unavailable: " + row.rewardId);
            // Original CalRewardCount's base branch ignores the hour argument.
            if (isBase) return (double)(float)reward.hourlyOutput * levelNum;
            return Zjcs.Official.HomeIncomeRules.HangUpUtils.CalcRewardCount(Info(row),
                new Zjcs.Official.HomeIncomeRules.HangUpRewardInfo { HourlyOutput = (float)reward.hourlyOutput },
                formula, snapshot.NonBattleProps, hours, levelNum);
        }
        public long GetHangUpMaxRewardStore(int id, int level = -1)
        {
            var row = Row(id);
            if (level < 0) level = readLevel(id);
            double hours = rules.IncomeMaxStoreHours(row.levelRuleId, level);
            return (long)GetHangUpReward(id, hours, level);
        }
        public double GetProp(IReadOnlyDictionary<PropType, long> props, PropType type)
            => formula.GetProp(props?.ToDictionary(row => (int)row.Key, row => row.Value), (FormulaProp)type);
        public void GetHangUpProp(HomeWindowHangUpInfo info, IReadOnlyDictionary<PropType, long> props,
            out double addPercent, out double addFixed)
        {
            formula.GetHangUpProp(Info(Row(info.Id)), props?.ToDictionary(row => (int)row.Key, row => row.Value),
                out addPercent, out float value);
            addFixed = value;
        }
        static FormulaInfo Info(HomeHangUpRecord row) => new FormulaInfo {
            BindFurniture = (Zjcs.Official.HomeIncomeRules.EFurniture)row.bindFurniture,
            EffectFixedPropType = (FormulaProp)row.effectFixedProp,
            EffectPercentPropType = (FormulaProp)row.effectPercentProp,
            FinallyEffectPercentPropType = (FormulaProp)row.finallyEffectPercentProp };
        static Dictionary<PropType, long> Convert(IReadOnlyDictionary<int, long> values)
            => values.ToDictionary(row => (PropType)row.Key, row => row.Value);
        public Dictionary<PropType, long> GetChunckProps(EPropHandleType type)
        {
            if (snapshot == null) throw new InvalidOperationException("Refresh home properties first.");
            return snapshot.Chunks.TryGetValue((int)type, out var values) ? Convert(values) : null;
        }
        public void GetProps(List<EPropHandleType> types, Dictionary<PropType, long> result)
        {
            foreach (var type in types)
            {
                var values = GetChunckProps(type);
                if (values == null) continue;
                foreach (var row in values)
                { result.TryGetValue(row.Key, out long previous); result[row.Key] = previous + row.Value; }
            }
        }
    }
}
