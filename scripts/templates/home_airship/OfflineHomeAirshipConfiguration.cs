using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeFurniture;

namespace Zjcs.Offline.HomeAirship
{
    // The table contains possible banners; it does not itself open an activity.
    public sealed class OfflineHomeAirshipConfiguration
    {
        [Serializable] private sealed class Data { public Tables tables; }
        [Serializable] private sealed class Tables
        {
            public Banner[] activity_airship_banner;
            public Function[] function_limit;
        }
        [Serializable] private sealed class Banner
        {
            public int EntryType, Id, Order;
            public string PrefabPath;
        }
        [Serializable] private sealed class Function
        {
            public int Type, Key0, Key1;
            public HomeConditionNode Condition;
        }
        private readonly Dictionary<(int, int), Banner> banners = new Dictionary<(int, int), Banner>();
        private readonly Function trade;

        private OfflineHomeAirshipConfiguration(Data data)
        {
            if (data?.tables?.activity_airship_banner == null || data.tables.function_limit == null ||
                data.tables.function_limit.Length != 1 || data.tables.function_limit[0]?.Condition == null)
                throw new InvalidOperationException("Original airship configuration is incomplete.");
            trade = data.tables.function_limit[0];
            foreach (var row in data.tables.activity_airship_banner)
            {
                if (row == null || string.IsNullOrEmpty(row.PrefabPath) ||
                    banners.ContainsKey((row.EntryType, row.Id)))
                    throw new InvalidOperationException("Original airship banner configuration is invalid.");
                banners.Add((row.EntryType, row.Id), row);
            }
        }
        public static OfflineHomeAirshipConfiguration LoadDefault()
        {
            var text = Resources.Load<TextAsset>("Home/Airship/airship-config");
            if (!text) throw new InvalidOperationException("Original airship configuration is unavailable.");
            return new OfflineHomeAirshipConfiguration(JsonUtility.FromJson<Data>(text.text));
        }
        public HomeConditionNode TradeCondition(int type, int key0, int key1)
            => type == trade.Type && key0 == trade.Key0 && key1 == trade.Key1 ? trade.Condition : null;
        public AirshipActivityEntry MatchActivity(int type, int id)
            => banners.TryGetValue((type, id), out var row)
                ? new AirshipActivityEntry { EntryType = row.EntryType, ActivityId = row.Id, Order = row.Order }
                : null;
        public string BannerPrefabPath(int type, int id)
            => banners.TryGetValue((type, id), out var row) ? row.PrefabPath : null;
    }
}
