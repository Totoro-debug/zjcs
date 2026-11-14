using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Official.HomePlayerStatusRules;

namespace Zjcs.Offline.HomePlayerHealthUI
{
    public sealed class OfflineHomeStatusConfiguration
    {
        private readonly Dictionary<int, StatusEffectInfo> statuses;
        public IReadOnlyCollection<StatusEffectInfo> Statuses => statuses.Values;
        // Preserve the selected source rows for scope/entry owners. This does
        // not evaluate conditions or manufacture missing detail-window assets.
        public IReadOnlyDictionary<int, JObject> Scopes { get; }
        public IReadOnlyDictionary<int, JObject> Entries { get; }

        private OfflineHomeStatusConfiguration(JObject source)
        {
            if ((string)source["manifest"] != "884_156692")
                throw new InvalidDataException("Wrong player-status source version.");
            statuses = source["statuses"].ToObject<StatusEffectInfo[]>().ToDictionary(row => row.ClassId);
            Scopes = source["scopes"].Cast<JObject>().ToDictionary(row => (int)row["Id"]);
            Entries = source["entries"].Cast<JObject>().ToDictionary(row => (int)row["Id"]);
            if (statuses.Count == 0 || statuses.Values.Any(row => row.ClassId < 0
                || !Enum.IsDefined(typeof(StatusEffectIconType), row.IconType)))
                throw new InvalidDataException("Invalid selected player-status table.");
        }

        public StatusEffectInfo Get(int id) => statuses.TryGetValue(id, out var row)
            ? row : throw new InvalidDataException("Unknown selected player status: " + id);

        public static OfflineHomeStatusConfiguration LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/player-status");
            if (!asset) throw new InvalidDataException("The selected player-status table is unbound.");
            return new OfflineHomeStatusConfiguration(JObject.Parse(asset.text));
        }
    }
}
