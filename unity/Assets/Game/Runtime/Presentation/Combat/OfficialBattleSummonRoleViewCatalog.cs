using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Presentation.Combat
{
    internal static class OfficialBattleSummonRoleViewCatalog
    {
        [Serializable] private sealed class Views { public int schema; public string manifest; public Role[] roles; }
        [Serializable] internal sealed class Role
        {
            public int entityId;
            public string mGeneratePlayable, mDiePlayable, mDieOnStartPlayable;
            public bool tryOnStart, DestroyOnDie;
            public float DestroyDelayTime, mGenerateDelayTime;
            public bool mGeneratePlayableNeedDelay;
        }
        private static Dictionary<int, Role> roles;

        internal static Role FindRole(int entityId, BattleNativeMonsterAssets assets = null)
        {
            string scoped = assets?.PresentationResource("role-views");
            var selected = scoped != null ? Read(scoped, assets.SourceManifest)
                : roles ?? (roles = Read("Combat/SummonRoleViews/role-views", "884_156692"));
            return selected.TryGetValue(entityId, out var role) ? role : null;
        }

        private static Dictionary<int, Role> Read(string resource, string expectedManifest)
        {
            var asset = Resources.Load<TextAsset>(resource);
            var views = asset ? JsonUtility.FromJson<Views>(asset.text) : null;
            if (views?.schema != 2 || views.manifest != expectedManifest || views.roles == null)
                throw new InvalidOperationException("Original summon role view bindings are missing: " + resource);
            return views.roles.ToDictionary(value => value.entityId);
        }

        internal static OfficialBattleCharacterGenerationPlayback.Binding FindGeneration(int entityId,
            BattleNativeMonsterAssets assets = null)
        {
            var role = FindRole(entityId, assets);
            if (role == null) return null;
            return new OfficialBattleCharacterGenerationPlayback.Binding
            {
                Generate = Load(role.mGeneratePlayable), DelayTime = role.mGenerateDelayTime,
                NeedsDelay = role.mGeneratePlayableNeedDelay
            };
        }
        internal static OfficialBattleCharacterDeathCatalog.Binding FindDeath(int entityId,
            BattleNativeMonsterAssets assets = null)
        {
            var role = FindRole(entityId, assets);
            return role == null ? null : new OfficialBattleCharacterDeathCatalog.Binding
            {
                Normal = Load(role.mDiePlayable), OnStart = Load(role.mDieOnStartPlayable), TryOnStart = role.tryOnStart
            };
        }
        private static GameObject Load(string path) => string.IsNullOrEmpty(path) ? null
            : Resources.Load<GameObject>(path) ?? throw new InvalidOperationException("Original summon lifecycle graph is missing: " + path);
    }
}
