using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeOutputAddition;

namespace Zjcs.Offline.HomeUI
{
    // Local policy supplies only the active records at a segment boundary. The
    // existing official selectors, disabled-function checks and numeric formulas
    // still calculate the value; no activity or reward is created here.
    public sealed class OfflineHomeIncomeAdditionSnapshot : IHomeIncomeAdditionSnapshot
    {
        readonly PlayerOutputAdditionDataWrap[] records;
        readonly OutputAdditionInfoParser configuration;
        readonly HomeOutputAdditionSettings settings;
        readonly Func<int?, bool> disabled;
        public bool HasRecords => records.Length != 0;
        public IReadOnlyList<long> ChangeTicks { get; }

        public OfflineHomeIncomeAdditionSnapshot(IEnumerable<PlayerOutputAdditionDataWrap> records,
            IEnumerable<OutputAdditionInfo> rows, HomeOutputAdditionSettings settings, Func<int?, bool> disabled)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (settings?.HomeHangUpAddPercentTypeDict == null || settings.HomeHangUpMaxStoreTypeDict == null)
                throw new ArgumentException("Original income addition mappings are required.", nameof(settings));
            this.disabled = disabled ?? throw new ArgumentNullException(nameof(disabled));
            this.records = records.Select(record => new PlayerOutputAdditionDataWrap {
                PlayerId = record.PlayerId, Id = record.Id, OutputActionType = record.OutputActionType,
                StartTime = record.StartTime, EndTime = record.EndTime,
                OutputAdditionPropDict = record.OutputAdditionPropDict.ToDictionary(pair => pair.Key,
                    pair => new PlayerOutputAdditionPropTypeDataWrap { ClassId = pair.Value.ClassId,
                        StartTime = pair.Value.StartTime, EndTime = pair.Value.EndTime }) }).ToArray();
            // Match the durable reader's unique-record invariant before any settlement.
            this.records.ToDictionary(record => record.Id);
            configuration = new OutputAdditionInfoParser(rows.ToDictionary(row => row.ClassId, row => new OutputAdditionInfo {
                ClassId = row.ClassId, OutputAdditionPropVal = row.OutputAdditionPropVal,
                OutputActionType = row.OutputActionType, OutputGroupType = row.OutputGroupType,
                OutputAdditionPropType = row.OutputAdditionPropType, ShowGroupKey = row.ShowGroupKey, ShowKey = row.ShowKey }));
            this.settings = new HomeOutputAdditionSettings {
                HomeHangUpAddPercentTypeDict = new Dictionary<int, OutputAdditionPropType>(settings.HomeHangUpAddPercentTypeDict),
                HomeHangUpMaxStoreTypeDict = new Dictionary<int, OutputAdditionPropType>(settings.HomeHangUpMaxStoreTypeDict) };
            ChangeTicks = Array.AsReadOnly(this.records.SelectMany(record => new[] { record.StartTime.Ticks, record.EndTime.Ticks }
                .Concat(record.OutputAdditionPropDict.Values.SelectMany(prop => new[] { prop.StartTime.Ticks, prop.EndTime.Ticks })))
                .Distinct().OrderBy(value => value).ToArray());
        }

        HomeOutputAdditionPlayer PlayerAt(long utcTicks)
        {
            var at = new DateTime(utcTicks, DateTimeKind.Utc);
            var active = records.Where(record => at >= record.StartTime && at < record.EndTime)
                .ToDictionary(record => record.Id, record => new PlayerOutputAdditionDataWrap {
                    PlayerId = record.PlayerId, Id = record.Id, OutputActionType = record.OutputActionType,
                    StartTime = record.StartTime, EndTime = record.EndTime,
                    OutputAdditionPropDict = record.OutputAdditionPropDict
                        .Where(pair => at >= pair.Value.StartTime && at < pair.Value.EndTime)
                        .ToDictionary(pair => pair.Key, pair => pair.Value) });
            return new HomeOutputAdditionPlayer(active, configuration, settings, () => at, disabled);
        }

        public int AddPercent(int hangUpId, long utcTicks)
        {
            var player = PlayerAt(utcTicks);
            return Math.Max(0, player.GetOutputAdditionPropValue(player.GetOutputAdditionAddPercentType(hangUpId)));
        }
        public long ExtraCapacity(int hangUpId, long capacity, long utcTicks)
            => PlayerAt(utcTicks).CalcOutputAdditionExtStore(hangUpId, capacity);
        public long ExtraCount(int hangUpId, double count, long utcTicks)
            => PlayerAt(utcTicks).CalcOutputAdditionExtCount(hangUpId, count);
    }
}
