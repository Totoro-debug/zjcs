using Zjcs.Offline.BattleThemeUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleHudView
    {
        private readonly string themeNpcPrefabPath;

        private void MountThemeNpc()
        {
            var slot = renderer.Find("/FightMainPanel/Content/ThemeNpcSlot");
            OfflineBattleThemeNpcView.Mount(slot == null ? null : slot.transform, themeNpcPrefabPath);
        }
    }
}
