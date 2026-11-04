using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleHudView
    {
        private OfficialBattlePetHudView petHud;

        // Invoked at the scheduled native cast start, not when its settled
        // checkpoint arrives. Other players cannot animate this player's widget.
        public void OnPetSkillStarted(FightBattleSkillCastEvent cast)
        {
            if (cast == null || cast.ActorId != 1) return;
            petHud?.PlaySkill(cast.SkillItemId);
        }

        public void RefreshPet(FightBattleCheckpoint checkpoint, OfflinePlayerProgress player)
        {
            if (player == null) return;
            bool unlocked = OfficialBattleSkillSlotBindings.IsPetFunctionUnlocked(player.Pets?.Pets?.Count ?? 0);
            RefreshPetFunction(unlocked);
            if (!unlocked) return;
            if (petHud == null) petHud = new OfficialBattlePetHudView(renderer, mainAnimationPlayer);
            petHud.Refresh(checkpoint, player);
        }
    }
}
