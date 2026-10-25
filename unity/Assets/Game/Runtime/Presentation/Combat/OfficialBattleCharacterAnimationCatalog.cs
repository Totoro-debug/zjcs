using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleCharacterAnimationBinding
    {
        public RuntimeAnimatorController controller;
        public string clip;
        public string state;
    }

    /// <summary>Maps recovered native clips to states that actually contain those clips.</summary>
    public sealed class OfficialBattleCharacterAnimationCatalog : ScriptableObject
    {
        public const string ResourcePath = "Combat/OfficialBattleCharacterAnimationCatalog";
        [SerializeField] private RuntimeAnimatorController[] controllers = Array.Empty<RuntimeAnimatorController>();
        [SerializeField] private OfficialBattleCharacterAnimationBinding[] bindings =
            Array.Empty<OfficialBattleCharacterAnimationBinding>();
        private static OfficialBattleCharacterAnimationCatalog instance;

        public static bool TryResolve(RuntimeAnimatorController controller, string clip, out string state)
        {
            if (instance == null) instance = Resources.Load<OfficialBattleCharacterAnimationCatalog>(ResourcePath);
            if (instance == null) throw new InvalidOperationException("Native battle character animation bindings are missing.");
            while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
            foreach (var binding in instance.bindings)
                if (binding.controller == controller && binding.clip == clip)
                {
                    state = binding.state;
                    return true;
                }
            state = null;
            foreach (var nativeController in instance.controllers)
                if (nativeController == controller) return false; // A source state with no converted motion cannot play.
            state = "Base Layer." + clip;
            return true;
        }
    }
}
