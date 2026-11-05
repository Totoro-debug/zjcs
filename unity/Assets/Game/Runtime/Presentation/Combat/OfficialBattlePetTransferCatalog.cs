using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattlePetTransferCatalog
    {
        [Serializable] public sealed class Binding
        {
            public int entityId;
            public float transferTime;
            public string[] graphGroups;
            public string hidePlayable, showPlayable;
            [NonSerialized] public GameObject HidePlayable, ShowPlayable;
        }
        [Serializable] private sealed class Configuration { public string manifest; public Binding[] models; }
        [Serializable] private sealed class Graph { public string id, resource; }
        [Serializable] private sealed class Graphs { public string manifest; public Graph[] graphs; }
        private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();

        public static OfficialBattlePetTransferCatalog Load()
        {
            var data = Resources.Load<TextAsset>("Combat/pet-transfers");
            var graphData = Resources.Load<TextAsset>("Combat/pet-transfer-graphs");
            if (!data || !graphData) throw new InvalidOperationException("Original pet transfer bindings are unavailable.");
            var configuration = JsonUtility.FromJson<Configuration>(data.text);
            var graphs = JsonUtility.FromJson<Graphs>(graphData.text);
            if (configuration?.manifest != "884_156692" || configuration.models?.Length != 21 || graphs?.manifest != configuration.manifest)
                throw new InvalidOperationException("Original pet transfer source versions differ.");
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var row in graphs.graphs)
                prefabs.Add(row.id, Resources.Load<GameObject>(row.resource)
                    ?? throw new InvalidOperationException("Original pet transfer graph is missing: " + row.resource));
            var result = new OfficialBattlePetTransferCatalog();
            foreach (var row in configuration.models)
            {
                row.HidePlayable = prefabs[row.hidePlayable]; row.ShowPlayable = prefabs[row.showPlayable];
                result.bindings.Add(row.entityId, row);
            }
            return result;
        }

        public Binding Find(int entityId) => bindings.TryGetValue(entityId, out var binding) ? binding : null;
    }
}
