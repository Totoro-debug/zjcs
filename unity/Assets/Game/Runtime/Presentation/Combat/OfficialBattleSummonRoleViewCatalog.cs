using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    internal static class OfficialBattleSummonRoleViewCatalog
    {
        [Serializable] private sealed class Views { public string manifest; public Role[] roles; }
        [Serializable] private sealed class Role
        {
            public int entityId;
            public string mGeneratePlayable;
        }
        private static Dictionary<int, Role> roles;

        internal static OfficialBattleCharacterGenerationPlayback.Binding FindGeneration(int entityId)
        {
            if (roles == null)
            {
                var asset = Resources.Load<TextAsset>("Combat/SummonRoleViews/role-views");
                var views = asset ? JsonUtility.FromJson<Views>(asset.text) : null;
                if (views?.manifest != "884_156692" || views.roles == null)
                    throw new InvalidOperationException("Original summon role view bindings are missing.");
                roles = views.roles.ToDictionary(value => value.entityId);
            }
            if (!roles.TryGetValue(entityId, out var role)) return null;
            return new OfficialBattleCharacterGenerationPlayback.Binding
            {
                Generate = Load(role.mGeneratePlayable)
            };
        }
        private static GameObject Load(string path) => string.IsNullOrEmpty(path) ? null
            : Resources.Load<GameObject>(path) ?? throw new InvalidOperationException("Original summon lifecycle graph is missing: " + path);
    }
}
