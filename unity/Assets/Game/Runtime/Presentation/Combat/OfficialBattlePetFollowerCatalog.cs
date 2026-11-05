using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattlePetFollowerCatalog
    {
        [Serializable] public sealed class Binding
        {
            public int entityId, petClassId, teleportDistance;
            public float moveGridTime, startDelay;
            public bool followPosZ, useLastMovingData;
            public string modelPath, movePlayable;
            [NonSerialized] public GameObject MovePlayable;
        }
        [Serializable] private sealed class Configuration { public string manifest; public Binding[] models; }
        [Serializable] private sealed class Graph { public string id, resource; }
        [Serializable] private sealed class Graphs { public string manifest; public Graph[] graphs; }
        private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();

        public static OfficialBattlePetFollowerCatalog Load()
        {
            var input = Resources.Load<TextAsset>("Combat/pet-followers");
            var graphInput = Resources.Load<TextAsset>("Combat/pet-follower-graphs");
            if (!input || !graphInput) throw new InvalidOperationException("Original battle pet followers are unavailable.");
            var data = JsonUtility.FromJson<Configuration>(input.text);
            var graphs = JsonUtility.FromJson<Graphs>(graphInput.text);
            if (data?.manifest != "884_156692" || data.models?.Length != 21 || graphs?.manifest != data.manifest)
                throw new InvalidOperationException("Original battle pet follower versions differ.");
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var row in graphs.graphs)
                prefabs.Add(row.id, Resources.Load<GameObject>(row.resource)
                    ?? throw new InvalidOperationException("Original pet movement graph is unavailable: " + row.resource));
            var result = new OfficialBattlePetFollowerCatalog();
            foreach (var row in data.models)
            {
                row.MovePlayable = prefabs[row.movePlayable];
                result.bindings.Add(row.petClassId, row);
            }
            return result;
        }

        public Binding Find(int petClassId) => bindings.TryGetValue(petClassId, out var result) ? result : null;
    }
}
