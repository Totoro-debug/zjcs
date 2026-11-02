namespace Zjcs.Offline.ProfessionUI
{
    // Play changes the requested animation. Apply completes its current pose
    // after setup/skin resets, without restarting or advancing the caller clock.
    public interface IProfessionNativeSceneWeaponPoseProducer
    {
        void Play(ProfessionNativeAppearanceSkeleton state, string animation, bool loop);
        void Apply(ProfessionNativeAppearanceSkeleton state);
    }
}
