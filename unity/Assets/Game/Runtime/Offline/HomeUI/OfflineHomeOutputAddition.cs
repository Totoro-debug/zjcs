using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeOutputAddition;

namespace Zjcs.Offline.HomeUI
{
    // A projection of the single durable home save, never a second activity ledger.
    public sealed class OfflineHomeOutputAddition
    {
        [Serializable]
        public sealed class State
        {
            public List<PlayerOutputAdditionDataWrap> Records = new List<PlayerOutputAdditionDataWrap>();
        }
        private sealed class Configuration
        {
            public string manifest;
            public OutputAdditionInfo[] rows;
            public HomeOutputAdditionSettings settings;
        }
        private readonly OfflineHomeIncomeSession session;
        private readonly Configuration configuration;
        private readonly Func<DateTime> now;
        private readonly Func<int?, bool> disabled;
        public event Action Changed;

        public OfflineHomeOutputAddition(OfflineHomeIncomeSession session, Func<DateTime> now,
            Func<int?, bool> disabled)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.disabled = disabled ?? throw new ArgumentNullException(nameof(disabled));
            var asset = Resources.Load<TextAsset>("Home/output-addition-rules");
            if (!asset) throw new InvalidOperationException("The original output-addition configuration is unavailable.");
            configuration = JsonConvert.DeserializeObject<Configuration>(asset.text);
            if (configuration?.manifest != "884_156692" || configuration.rows == null
                || configuration.settings?.HomeHangUpAddPercentTypeDict == null
                || configuration.settings.HomeHangUpMaxStoreTypeDict == null)
                throw new InvalidOperationException("The selected output-addition configuration is unbound.");
        }

        // New games deliberately have no activity records. Missing state is read
        // without writing, changing a timer or silently creating an activity.
        public State ReadState()
        {
            var json = session.Save.OutputAdditionStateJson;
            var state = string.IsNullOrEmpty(json) ? new State() : JsonConvert.DeserializeObject<State>(json);
            if (state?.Records == null || state.Records.Any(record => record == null || record.OutputAdditionPropDict == null
                || record.OutputAdditionPropDict.Values.Any(value => value == null))
                || state.Records.Select(record => record.Id).Distinct().Count() != state.Records.Count)
                throw new InvalidOperationException("Invalid local output-addition records.");
            return state;
        }

        private HomeOutputAdditionPlayer Player()
        {
            var state = ReadState();
            return new HomeOutputAdditionPlayer(state.Records.ToDictionary(record => record.Id),
                new OutputAdditionInfoParser(configuration.rows.ToDictionary(row => row.ClassId)),
                configuration.settings, now, disabled);
        }

        // Only a migrated activity owner supplies a real record. No schedule or
        // reward is synthesized here; its transaction receipt remains durable.
        public bool ApplyRecord(PlayerOutputAdditionDataWrap record, string receipt)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record.OutputAdditionPropDict == null || record.OutputAdditionPropDict.Values.Any(value => value == null
                || !configuration.rows.Any(row => row.ClassId == value.ClassId)))
                throw new ArgumentException("Activity properties require selected original configuration.", nameof(record));
            var state = ReadState();
            var index = state.Records.FindIndex(value => value.Id == record.Id);
            if (index < 0) state.Records.Add(record); else state.Records[index] = record;
            var json = JsonConvert.SerializeObject(state);
            bool committed = session.Mutate(receipt, save => save.OutputAdditionStateJson = json);
            if (committed) Changed?.Invoke();
            return committed;
        }

        public bool IsDisabled => disabled(null);
        public int PropertyValue(OutputAdditionPropType type) => Player().GetOutputAdditionPropValue(type);
        public List<(List<OutputAdditionInfo> propList, DateTime endTime)> Groups(OutputGroupType type)
            => OutputAdditionUtils.GetCurrOutputGroupPropList(Player(), type);
        public List<int> ActionClassIds(OutputActionType type)
            => OutputAdditionUtils.GetCurrOutputActionClassIdList(Player(), type);
        public List<OutputActionType> ActiveActions() => OutputAdditionUtils.GetActivityOutputActionTypeList(Player());
        public long ExtraCapacity(int hangUpId, long capacity) => Player().CalcOutputAdditionExtStore(hangUpId, capacity);
        public long ExtraCount(int hangUpId, double count) => Player().CalcOutputAdditionExtCount(hangUpId, count);
    }
}
