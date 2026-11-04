using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleCharacterDeathCatalog
    {
        [Serializable] private sealed class Graph { public string id, resource; }
        [Serializable] private sealed class Graphs { public string manifest; public Graph[] graphs; }
        [Serializable] private sealed class Role
        {
            public int entity_id;
            public bool try_on_start;
            public string mDiePlayable, mDieOnStartPlayable;
        }
        [Serializable] private sealed class Roles { public string manifest; public Role[] roles; }
        public sealed class Binding
        {
            public GameObject Normal { get; internal set; }
            public GameObject OnStart { get; internal set; }
            public bool TryOnStart { get; internal set; }
        }
        private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
        public static OfficialBattleCharacterDeathCatalog LoadDefault()
        {
            var graphText = Resources.Load<TextAsset>("Combat/character-death-graphs");
            var roleText = Resources.Load<TextAsset>("Combat/character-death-views");
            if (!graphText || !roleText) throw new InvalidOperationException("Original character death bindings are unavailable.");
            var graphs = JsonUtility.FromJson<Graphs>(graphText.text);
            var roles = JsonUtility.FromJson<Roles>(roleText.text);
            if (graphs?.manifest != "884_156692" || roles?.manifest != graphs.manifest)
                throw new InvalidOperationException("Original character death source versions differ.");
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var row in graphs.graphs)
            {
                var prefab = Resources.Load<GameObject>(row.resource);
                if (!prefab) throw new InvalidOperationException("Original character death graph is unavailable: " + row.resource);
                prefabs.Add(row.id, prefab);
            }
            var result = new OfficialBattleCharacterDeathCatalog();
            foreach (var role in roles.roles)
                result.bindings.Add(role.entity_id, new Binding
                {
                    Normal = string.IsNullOrEmpty(role.mDiePlayable) ? null : prefabs[role.mDiePlayable],
                    OnStart = string.IsNullOrEmpty(role.mDieOnStartPlayable) ? null : prefabs[role.mDieOnStartPlayable],
                    TryOnStart = role.try_on_start
                });
            return result;
        }
        public Binding Find(int entityId) => bindings.TryGetValue(entityId, out var value) ? value : null;
    }
}
