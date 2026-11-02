using System;
using System.Collections.Generic;
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

        // Recovered game clips are SkeletonMecanim markers. The converted character
        // controller carries actual bone curves; keep the game graph and bind those
        // motions by their exact source names, never by a pose/name approximation.
        internal static AnimatorOverrideController BindNativeMotions(RuntimeAnimatorController native,
            RuntimeAnimatorController game)
        {
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            var seen = new HashSet<AnimationClip>();
            foreach (AnimationClip marker in game.animationClips)
                if (marker != null && seen.Add(marker))
                    overrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(marker,
                        ResolveNativeMotion(native, marker)));
            var controller = new AnimatorOverrideController(game) { name = game.name + " (native motions)" };
            controller.ApplyOverrides(overrides);
            return controller;
        }

        internal static AnimationClip ResolveNativeMotion(RuntimeAnimatorController native, AnimationClip marker)
        {
            if (native == null || marker == null) return marker;
            foreach (AnimationClip motion in native.animationClips)
                if (motion != null && motion.name == marker.name)
                {
                    if (Mathf.Abs(motion.length - marker.length) > .002f)
                        throw new InvalidOperationException("Native battle motion duration differs from its source marker: "
                            + marker.name);
                    return motion;
                }
            // Unconverted motions stay explicitly unsupported. In particular a map
            // run/idle must not replace a different battle/skill animation.
            return marker;
        }

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
