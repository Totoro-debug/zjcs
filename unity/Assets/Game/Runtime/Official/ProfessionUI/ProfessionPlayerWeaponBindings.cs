using System;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionWeaponSkeleton:IProfessionAppearanceSkeletonComponent
    {
        string SkinName { get; }
        void SetSkin(string name);
        void PlayAnimation(string name,bool loop);
    }
    public interface IProfessionPlayerWeaponBindings
    {
        ICharacterPartEntryProvider Parts { get; }
        IProfessionPlayerAppearanceBindings Player { get; }
    }
    public static class ProfessionPlayerWeaponBindings
    {
        public static IProfessionPlayerWeaponBindings Current { get; set; }
        public static IProfessionPlayerWeaponBindings Required => Current ??
            throw new InvalidOperationException("Original weapon appearance parts/player data are unbound.");
    }
}
