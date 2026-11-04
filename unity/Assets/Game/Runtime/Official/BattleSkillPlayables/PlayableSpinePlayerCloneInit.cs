using Spine.Unity;
using UnityEngine;
using Zjcs.Official.CombatAppearance;

// Original 884_156692 player-clone initialization and release order.
public class PlayableSpinePlayerCloneInit : PlayableSpineCloneInit
{
    public AppearanceCharacter AppearanceCharacter;

    public override void Init(SkeletonRenderer skeletonRenderer)
    {
        if (skeletonRenderer.TryGetComponent<PlayerAppearance>(out var playerAppearance))
            AppearanceCharacter.SetRuntimeData(playerAppearance.AppearanceRuntimeData);
    }

    public override void Clear() => AppearanceCharacter.Release();
}
