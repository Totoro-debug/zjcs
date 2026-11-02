using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.CombatAppearance
{
    // Current application's preheated asset lookup, independent of its framework
    // service lifetime and of the historical UI character's per-load handles.
    public interface IProfessionAppearanceSceneCharacterBindings
    {
        AppearanceConfig Config { get; }
        bool TryGetPlayerSkeletonAssets(Sex sex, out ScriptableObject skeletonDataAsset, out ScriptableObject splitAnimationSo);
    }
}
