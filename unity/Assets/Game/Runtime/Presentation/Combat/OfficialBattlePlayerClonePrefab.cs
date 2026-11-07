using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattlePlayerClonePrefab
    {
        public static int Bind(GameObject instance, OfflineProfessionBattlePlayerResources resources)
        {
            if (!instance) throw new ArgumentNullException(nameof(instance));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            var bound = new Dictionary<Component, ProfessionNativeSceneRenderer>();
            foreach (var playable in instance.GetComponentsInChildren<MyPlayableSpineClone>(true))
            {
                var original = playable.SkeletonAnimation;
                if (!original || original.gameObject.activeInHierarchy || !playable.CloneInit
                    || playable.CloneInit.AppearanceCharacter != original.GetComponent<Current.AppearanceCharacter>())
                    throw new InvalidOperationException("Original clone initialization and renderer targets differ.");
                if (original is ProfessionNativeSceneRenderer) continue;
                if (!bound.TryGetValue(original, out var native))
                {
                    if (original.GetType().FullName != "Spine.Unity.SkeletonAnimation")
                        throw new InvalidOperationException("Unexpected original clone renderer endpoint.");
                    var appearance = original.GetComponent<AppearanceAnimation>();
                    if (!appearance) throw new InvalidOperationException("Original clone animation appearance is absent.");
                    native = original.gameObject.AddComponent<ProfessionNativeSceneRenderer>();
                    resources.BindCloneAppearance(appearance, original, native);
                    original.gameObject.AddComponent<OfficialBattleSpineCloneAnimation>().Configure(native);
                    bound.Add(original, native);
                }
                playable.SkeletonAnimation = native;
            }
            // Keep every original reference alive until all playables have been rebound.
            // A disabled obsolete SDK renderer would still receive Awake on activation.
            foreach (var original in bound.Keys) UnityEngine.Object.DestroyImmediate(original);
            return bound.Count;
        }
    }
}
