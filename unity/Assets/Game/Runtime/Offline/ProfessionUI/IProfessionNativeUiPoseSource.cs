using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public interface IProfessionNativeUiPhysicsMovement
    {
        void UpdatePhysicsMovement();
    }
    // The application owns sampling, constraints and animation transitions. The
    // Canvas endpoint consumes a completed pose and never substitutes a clip.
    public interface IProfessionNativeUiPoseSource
    {
        ScriptableObject Asset { get; }
        ProfessionNativeAppearanceSkeleton CreateSkeleton(string initialSkin);
        IProfessionAppearanceAnimationState AnimationState { get; }
        void Play(ProfessionNativeAppearanceSkeleton skeleton, string name, bool loop);
        void Apply(ProfessionNativeAppearanceSkeleton skeleton, float deltaTime);
        Material MaterialForPage(string page, int blend);
        Texture TextureForPage(string page);
    }
}
