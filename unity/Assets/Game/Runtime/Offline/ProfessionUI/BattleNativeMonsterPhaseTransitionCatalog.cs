using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    [Serializable]
    public sealed class BattleNativeMonsterPhaseTransitionBinding
    {
        public int entityId;
        public RuntimeAnimatorController controller;
    }

    public sealed class BattleNativeMonsterPhaseTransitionCatalog : ScriptableObject
    {
        public const string ResourcePath = "Combat/NativeMonsters/phase-transition-catalog";

        [SerializeField] private BattleNativeMonsterPhaseTransitionBinding[] bindings =
            Array.Empty<BattleNativeMonsterPhaseTransitionBinding>();

        public static BattleNativeMonsterPhaseTransitionCatalog Load()
        {
            var catalog = Resources.Load<BattleNativeMonsterPhaseTransitionCatalog>(ResourcePath);
            if (!catalog) throw new InvalidOperationException("Original monster phase transition catalog is not packaged.");
            return catalog;
        }

        public RuntimeAnimatorController FindController(int entityId)
        {
            RuntimeAnimatorController result = null;
            if (bindings == null) return result;
            foreach (BattleNativeMonsterPhaseTransitionBinding binding in bindings)
            {
                if (binding == null || binding.entityId != entityId) continue;
                if (result) throw new InvalidOperationException("Duplicate original monster phase Animator: " + entityId);
                result = binding.controller;
            }
            return result;
        }
    }
}
