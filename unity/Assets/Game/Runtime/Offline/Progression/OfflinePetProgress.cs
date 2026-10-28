using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflinePetProgress
    {
        public int Version;
        public long NextPetIndex = 1;
        public long FollowPetIndex = -1;
        public int SharedLevel = 1;
        public int BlessSharedLevel = 1;
        public bool EnableUseGeneralPetPiece;
        public long MaxObservedFeedExp;
        public List<OfflinePetInstance> Pets = new List<OfflinePetInstance>();
        public List<OfflinePetFormationSlot> Formation = CreateFormation();
        public List<OfflinePetSeasonRecord> SeasonRecords = new List<OfflinePetSeasonRecord>();

        public static List<OfflinePetFormationSlot> CreateFormation()
        {
            return Enumerable.Range(0, 4).Select(index => new OfflinePetFormationSlot
            {
                SlotIndex = index,
                PetIndex = -1,
                Level = 1,
                BlessLevel = 1
            }).ToList();
        }

        public static void Normalize(OfflinePetProgress progress, OfflinePetGachaProgress legacyOwnership)
        {
            if (progress == null) return;
            if (progress.Pets == null) progress.Pets = new List<OfflinePetInstance>();
            if (progress.Formation == null) progress.Formation = CreateFormation();
            if (progress.SeasonRecords == null) progress.SeasonRecords = new List<OfflinePetSeasonRecord>();
            if (progress.FollowPetIndex == 0) progress.FollowPetIndex = -1;

            if (progress.Version == 0)
            {
                foreach (int classId in legacyOwnership?.OwnedPetIds ?? new List<int>())
                    if (classId > 0 && !progress.Pets.Any(value => value != null && value.ClassId == classId))
                    {
                        int quality = (legacyOwnership?.History ?? new List<OfflinePetGachaDrawRecord>())
                            .Where(value => value != null)
                            .SelectMany(value => value.Awards ?? new List<OfflinePetGachaAward>())
                            .Where(value => value != null && value.IsPet && value.ItemId == classId)
                            .Select(value => value.Quality)
                            .DefaultIfEmpty(0).Max();
                        AddPet(progress, classId, quality);
                    }
                progress.Version = 1;
            }

            while (progress.Formation.Count < 4)
                progress.Formation.Add(new OfflinePetFormationSlot
                {
                    SlotIndex = progress.Formation.Count,
                    PetIndex = -1,
                    Level = 1,
                    BlessLevel = 1
                });
            foreach (OfflinePetFormationSlot slot in progress.Formation)
                if (slot != null && slot.PetIndex == 0) slot.PetIndex = -1;
            foreach (OfflinePetSeasonRecord record in progress.SeasonRecords)
                if (record != null && record.Slots == null) record.Slots = new List<OfflinePetSeasonSlot>();
            foreach (OfflinePetInstance pet in progress.Pets)
            {
                if (pet == null) continue;
                if (pet.Abilities == null) pet.Abilities = new List<OfflinePetAbilityLevel>();
                if (pet.SkillRanks == null) pet.SkillRanks = new List<OfflinePetSkillRank>();
                if (pet.SlotIndex == 0 && !progress.Formation.Any(value => value != null
                    && value.SlotIndex == 0 && value.PetIndex == pet.Index)) pet.SlotIndex = -1;
                if (pet.Index <= 0) pet.Index = AllocateIndex(progress);
            }
            long maxIndex = progress.Pets.Where(value => value != null).Select(value => value.Index).DefaultIfEmpty(0).Max();
            if (progress.NextPetIndex <= maxIndex) progress.NextPetIndex = checked(maxIndex + 1);
        }

        public static OfflinePetInstance AddPet(OfflinePetProgress progress, int classId, int quality)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (classId <= 0) throw new ArgumentOutOfRangeException(nameof(classId));
            if (progress.Pets == null) progress.Pets = new List<OfflinePetInstance>();
            var pet = new OfflinePetInstance
            {
                Index = AllocateIndex(progress),
                ClassId = classId,
                Quality = Math.Max(quality, 0),
                SlotIndex = -1,
                Level = 1,
                EvolutionPhase = 1,
                Status = 0,
                Name = string.Empty,
                Abilities = new List<OfflinePetAbilityLevel>(),
                SkillRanks = new List<OfflinePetSkillRank>()
            };
            progress.Pets.Add(pet);
            return pet;
        }

        public static bool IsValid(OfflinePetProgress progress, OfflineSkillProfile skills)
        {
            if (progress == null || progress.Version != 1 || progress.NextPetIndex <= 0
                || progress.FollowPetIndex == 0 || progress.FollowPetIndex < -1
                || progress.SharedLevel < 1 || progress.BlessSharedLevel < 1
                || progress.MaxObservedFeedExp < 0 || progress.Pets == null || progress.Pets.Count > 2000
                || progress.Formation == null || progress.Formation.Count != 4
                || progress.SeasonRecords == null || progress.SeasonRecords.Count > 100) return false;

            var petsByIndex = new Dictionary<long, OfflinePetInstance>();
            if (skills == null || skills.Plans == null || skills.Plans.Length != OfflineSkillProfile.PlanCount) return false;
            var engagedPetIds = new HashSet<long>(skills.Plans.Where(value => value != null && value.PetIndex > 0)
                .Select(value => value.PetIndex));
            foreach (OfflinePetInstance pet in progress.Pets)
            {
                if (pet == null || pet.Index <= 0 || pet.ClassId <= 0 || pet.Quality < 0
                    || pet.SlotIndex < -1 || pet.Level < 1 || pet.CombatRating < 0
                    || pet.EvolutionPhase < 1 || pet.EvolutionPhase > 3 || pet.Status < 0 || pet.Status > 7
                    || pet.Name == null || pet.Name.Length > 64 || pet.ChangeNameUnixSeconds < 0
                    || pet.ChangeNameCount < 0 || pet.PotentialLevel < 0 || pet.Abilities == null
                    || pet.Abilities.Count > 1000 || pet.SkillRanks == null || pet.SkillRanks.Count > 100
                    || !petsByIndex.TryAdd(pet.Index, pet)) return false;

                var abilityNodes = new HashSet<int>();
                foreach (OfflinePetAbilityLevel ability in pet.Abilities)
                    if (ability == null || ability.NodeId <= 0 || ability.Level <= 0
                        || !abilityNodes.Add(ability.NodeId)) return false;
                var skillsById = new HashSet<int>();
                foreach (OfflinePetSkillRank skill in pet.SkillRanks)
                    if (skill == null || skill.SkillId <= 0 || skill.Rank <= 0
                        || !skillsById.Add(skill.SkillId)) return false;
                OfflinePetFormationSlot assignedSlot = progress.Formation.FirstOrDefault(value => value != null
                    && value.PetIndex == pet.Index);
                if ((pet.SlotIndex >= 0) != (assignedSlot != null)
                    || (assignedSlot != null && assignedSlot.SlotIndex != pet.SlotIndex)
                    || ((pet.Status & 1) != 0) != (assignedSlot != null)
                    || ((pet.Status & 2) != 0) != engagedPetIds.Contains(pet.Index)
                    || ((pet.Status & 4) != 0) != (pet.Index == progress.FollowPetIndex)) return false;
            }
            if (progress.NextPetIndex <= petsByIndex.Keys.DefaultIfEmpty(0).Max()) return false;
            if (progress.FollowPetIndex > 0 && !petsByIndex.ContainsKey(progress.FollowPetIndex)) return false;

            var slotIds = new HashSet<int>();
            var formationPetIds = new HashSet<long>();
            foreach (OfflinePetFormationSlot slot in progress.Formation)
            {
                if (slot == null || slot.SlotIndex < 0 || slot.SlotIndex > 3 || !slotIds.Add(slot.SlotIndex)
                    || slot.PetIndex == 0 || slot.PetIndex < -1
                    || slot.Level < 1 || slot.Exp < 0 || slot.BlessLevel < 1) return false;
                if (slot.PetIndex > 0 && (!petsByIndex.ContainsKey(slot.PetIndex)
                    || !formationPetIds.Add(slot.PetIndex))) return false;
                if (slot.PetIndex > 0 && petsByIndex[slot.PetIndex].SlotIndex != slot.SlotIndex) return false;
            }
            if (slotIds.Count != 4) return false;

            var seasonIds = new HashSet<int>();
            foreach (OfflinePetSeasonRecord record in progress.SeasonRecords)
            {
                if (record == null || record.SeasonId <= 0 || record.Slots == null
                    || record.Slots.Count > 4 || !seasonIds.Add(record.SeasonId)) return false;
                var seasonSlotIds = new HashSet<int>();
                foreach (OfflinePetSeasonSlot slot in record.Slots)
                    if (slot == null || slot.SlotIndex < 0 || slot.SlotIndex > 3
                        || slot.PetBlessLevel < 0 || slot.PetBlessExp < 0
                        || !seasonSlotIds.Add(slot.SlotIndex)) return false;
            }

            foreach (OfflineSkillPlan plan in skills.Plans)
                if (plan == null || plan.PetIndex == 0 || plan.PetIndex < -1
                    || (plan.PetIndex > 0 && !petsByIndex.ContainsKey(plan.PetIndex))) return false;
            return true;
        }

        private static long AllocateIndex(OfflinePetProgress progress)
        {
            if (progress.NextPetIndex <= 0) progress.NextPetIndex = 1;
            long result = progress.NextPetIndex;
            progress.NextPetIndex = checked(result + 1);
            return result;
        }
    }

    [Serializable]
    public sealed class OfflinePetInstance
    {
        public long Index;
        public int ClassId;
        public int Quality;
        public int SlotIndex = -1;
        public int Level = 1;
        public long CombatRating;
        public List<OfflinePetAbilityLevel> Abilities = new List<OfflinePetAbilityLevel>();
        public int EvolutionPhase = 1;
        public int Status;
        public List<OfflinePetSkillRank> SkillRanks = new List<OfflinePetSkillRank>();
        public string Name = string.Empty;
        public long ChangeNameUnixSeconds;
        public int ChangeNameCount;
        public int PotentialLevel;
    }

    [Serializable]
    public sealed class OfflinePetAbilityLevel { public int NodeId; public int Level; }
    [Serializable]
    public sealed class OfflinePetSkillRank { public int SkillId; public int Rank; }

    [Serializable]
    public sealed class OfflinePetFormationSlot
    {
        public int SlotIndex;
        public long PetIndex = -1;
        public int Level = 1;
        public long Exp;
        public int BlessLevel = 1;
    }

    [Serializable]
    public sealed class OfflinePetSeasonRecord
    {
        public int SeasonId;
        public List<OfflinePetSeasonSlot> Slots = new List<OfflinePetSeasonSlot>();
    }

    [Serializable]
    public sealed class OfflinePetSeasonSlot
    {
        public int SlotIndex;
        public int PetBlessLevel;
        public long PetBlessExp;
    }
}
