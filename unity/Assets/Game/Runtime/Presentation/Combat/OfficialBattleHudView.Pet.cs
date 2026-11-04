using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleHudView
    {
        private OfficialBattlePetHudView petHud;

        public void RefreshPet(FightBattleCheckpoint checkpoint, OfflinePlayerProgress player)
        {
            if (player == null) return;
            bool unlocked = OfficialBattleSkillSlotBindings.IsPetFunctionUnlocked(player.Pets?.Pets?.Count ?? 0);
            RefreshPetFunction(unlocked);
            if (!unlocked) return;
            if (petHud == null) petHud = new OfficialBattlePetHudView(renderer);
            petHud.Refresh(checkpoint, player);
        }
    }
}
