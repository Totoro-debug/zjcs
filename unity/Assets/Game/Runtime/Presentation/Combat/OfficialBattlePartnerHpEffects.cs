using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleUnitUI;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattlePartnerHpEffects : IDisposable
    {
        [Serializable] private sealed class Bindings { public string manifest; public BindingRow[] partners; public ColorRules colors; }
        [Serializable] public sealed class BindingRow { public int entityId; public string lockParentPath; }
        [Serializable] private sealed class ColorRules
        {
            public int legionStageType;
            public int[] onlyFactionStages, multipleGroupStages;
        }
        private readonly GamePlayManagerConfig config;
        private readonly Bindings bindings;
        private readonly Transform root;
        private readonly Dictionary<int, GameEffectData> factions = new Dictionary<int, GameEffectData>();
        private readonly GamePrefabTypeSetPools<BattleHpBloodEffect, GameObjectPool<BattleHpBloodEffect>> pools;
        private bool disposed;

        public OfficialBattlePartnerHpEffects(Transform parent, FightBattleCheckpoint checkpoint)
        {
            config = Resources.Load<GamePlayManagerConfig>("Combat/PartnerHpBars/ScriptableConfig/Modules/Fight/GamePlayManagerConfig");
            var json = Resources.Load<TextAsset>("Combat/PartnerHpBars/partner-bindings");
            if (!config || !json) throw new InvalidOperationException("Original partner HP faction resources are absent.");
            bindings = JsonUtility.FromJson<Bindings>(json.text);
            if (bindings?.manifest != "884_156692") throw new InvalidOperationException("Partner HP resource version differs.");
            root = new GameObject("GameFactionEffectAssignerHandler").transform;
            root.SetParent(parent, false);
            pools = new GamePrefabTypeSetPools<BattleHpBloodEffect, GameObjectPool<BattleHpBloodEffect>>(root,
                (slot, prefab) => new GameObjectPool<BattleHpBloodEffect>(slot, (target, _, active) => {
                    // This verified closure contains only BattleHpBloodEffect,
                    // Animator and native renderers; it has no injected service.
                    var instance = UnityEngine.Object.Instantiate(prefab, target, false);
                    if (active) instance.SetActive(true);
                    return instance;
                }, prefab.name));
            var collected = new HashSet<GameObject>();
            foreach (var data in new[] { config.DefaultEffect, config.CharacterMeEffect, config.SpactatedCharacterEffect }
                .Concat(config.FactionEffectDatas))
            {
                collected.Add(data.FillPointEffect); collected.Add(data.OutLineEffect);
            }
            foreach (var prefab in collected) if (prefab) pools.GetPoolByPreFab(prefab).Extend(config.PreCreateEffectCount);
            var ordered = ResolveLocalFactionOrder(checkpoint);
            for (int index = 0; index < ordered.Count; index++)
                factions.Add(ordered[index], index < config.FactionEffectDatas.Count
                    ? config.FactionEffectDatas[index] : config.DefaultEffect);
        }

        // Common.FactionColorResolver's direct/default/legion branches for the
        // confirmed local protagonist + NPC team. Side 1/2 are the common local
        // executor's player/monster faction keys; no spectator player is active.
        private List<int> ResolveLocalFactionOrder(FightBattleCheckpoint checkpoint)
        {
            var order = checkpoint.Units.Select(unit => unit.Side).Distinct().ToList();
            if (bindings.colors.onlyFactionStages.Contains(checkpoint.StageType)) return order;
            if (checkpoint.StageType == bindings.colors.legionStageType) return new List<int> { 1 };
            var players = new HashSet<int>(checkpoint.Units.Where(unit => unit.CharacterType == 1).Select(unit => unit.Side));
            int meFaction = checkpoint.Units.FirstOrDefault(unit => unit.Id == 1 && unit.CharacterType == 1)?.Side ?? 1;
            if (players.Count == 0 && !order.Contains(1)) order.Insert(0, 1);
            order.Sort((a, b) => Weight(b) - Weight(a));
            if (bindings.colors.multipleGroupStages.Contains(checkpoint.StageType)
                && checkpoint.Units.Select(unit => unit.Side).Distinct().Count() == 2)
            {
                order.RemoveAt(1); order.Add(2);
            }
            return order;
            int Weight(int faction) => faction == meFaction ? 100 : players.Contains(faction) ? 10 : 0;
        }

        public BindingRow Binding(int entityId) => bindings.partners.Single(row => row.entityId == entityId);
        public GameEffectData ForFaction(int faction) => factions.TryGetValue(faction, out var data) ? data : config.DefaultEffect;
        public GameObject Borrow(GameObject prefab, Transform slot, bool autoActive)
            => !prefab ? null : pools.GetPoolByPreFab(prefab).BorrowGo(slot, autoActive);
        public void Return(GameObject effect, GameObject prefab)
        {
            if (!effect) return;
            if (!prefab) UnityEngine.Object.Destroy(effect);
            else pools.GetPoolByPreFab(prefab).Return(effect.GetComponent<BattleHpBloodEffect>());
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; pools.Release();
            if (root) UnityEngine.Object.Destroy(root.gameObject);
        }
    }
}
