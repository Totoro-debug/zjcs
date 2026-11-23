using System;

namespace Zjcs.Offline.Progression
{
    // User-confirmed offline availability, separate from the official resolver.
    // Preserve the source assets until complete official artwork is available.
    public static class OfflineAppearanceAvailability
    {
        public static bool IsSelectable(string skinName)
        {
            return !string.Equals(skinName, "pWeapon_100_G_125", StringComparison.Ordinal)
                && !string.Equals(skinName, "pWeapon_200_G_125", StringComparison.Ordinal)
                && !string.Equals(skinName, "pWeapon_100_G_140", StringComparison.Ordinal)
                && !string.Equals(skinName, "pWeapon_200_G_140", StringComparison.Ordinal);
        }
    }
}
