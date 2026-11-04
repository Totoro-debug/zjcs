using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed partial class ProfessionNativeSceneRenderer
    {
        // Original appearance setup precedes PlayerAnimatorAppearance.Update(0).
        // A newly selected, unevaluated controller has no motion yet. Complete
        // its setup constraints without inventing an idle clip or a clock tick.
        internal void ApplyPlayerSkinPose(Animator animator)
        {
            if (playerPlayback == null) throw new InvalidOperationException("Bind the player's original Animator first.");
            if (!animator.isInitialized || animator.layerCount == 0
                || animator.GetCurrentAnimatorClipInfoCount(0) == 0)
                playerPlayback.ApplySetup(State);
            else ApplyAnimatorPose();
        }
    }
}
