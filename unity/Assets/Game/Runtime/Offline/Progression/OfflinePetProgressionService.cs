using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflinePetProgressionService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly Func<OfflinePetInstance, long> combatRating;

        public OfflinePetProgressionService(OfflineMainlineProgressSession session,
            Func<OfflinePetInstance, long> combatRating = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.combatRating = combatRating;
        }

        public OfflinePetProgress State => session.Save.Player.Pets;
        public long CombatRating(OfflinePetInstance pet) => pet == null
            ? 0L : combatRating?.Invoke(pet) ?? pet.CombatRating;

        public IReadOnlyList<OfflinePetInstance> Candidates(int planIndex)
        {
            if (planIndex < 0 || planIndex >= 3) return Array.Empty<OfflinePetInstance>();
            return CandidatesFor();
        }

        public IReadOnlyList<OfflinePetInstance> FollowingCandidates()
        {
            return CandidatesFor();
        }

        private IReadOnlyList<OfflinePetInstance> CandidatesFor()
        {
            return State.Pets.Where(value => value != null)
                .OrderByDescending(CombatRating)
                .ThenByDescending(value => value.Quality)
                .ThenByDescending(value => value.ClassId)
                .ThenBy(value => value.Index)
                .ToArray();
        }

        public ProgressionResult EquipPlanPet(int planIndex, long petIndex)
        {
            if (planIndex < 0 || planIndex >= 3 || petIndex == 0 || petIndex < -1
                || (petIndex > 0 && !State.Pets.Any(value => value.Index == petIndex)))
                return Invalid("幻兽或技能方案无效");
            if (session.Save.Player.Skills.Plans[planIndex].PetIndex == petIndex)
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged, Message = "幻兽方案未变化" };

            return session.ApplyCharacterMutation(player =>
            {
                player.Skills.Plans[planIndex].PetIndex = petIndex;
                var engaged = new HashSet<long>(player.Skills.Plans
                    .Where(value => value != null && value.PetIndex > 0)
                    .Select(value => value.PetIndex));
                foreach (OfflinePetInstance pet in player.Pets.Pets)
                    pet.Status = engaged.Contains(pet.Index) ? pet.Status | 2 : pet.Status & ~2;
            },
                "pet-plan-" + planIndex + "-" + Guid.NewGuid().ToString("N"));
        }

        public ProgressionResult SetFollowingPet(long petIndex)
        {
            if (petIndex == 0 || petIndex < -1
                || (petIndex > 0 && !State.Pets.Any(value => value.Index == petIndex)))
                return Invalid("随行幻兽无效");
            if (State.FollowPetIndex == petIndex)
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged, Message = "随行幻兽未变化" };

            return session.ApplyCharacterMutation(player =>
            {
                foreach (OfflinePetInstance pet in player.Pets.Pets)
                    pet.Status = pet.Index == petIndex ? pet.Status | 4 : pet.Status & ~4;
                player.Pets.FollowPetIndex = petIndex;
            },
                "pet-follow-" + Guid.NewGuid().ToString("N"));
        }

        public static OfflinePetInstance RegisterAcquiredPet(OfflinePlayerProgress player, int classId, int quality)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (player.Pets == null) player.Pets = new OfflinePetProgress();
            OfflinePetProgress.Normalize(player.Pets, player.PetGacha);
            return OfflinePetProgress.AddPet(player.Pets, classId, quality);
        }

        private static ProgressionResult Invalid(string message)
        {
            return new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };
        }
    }
}
