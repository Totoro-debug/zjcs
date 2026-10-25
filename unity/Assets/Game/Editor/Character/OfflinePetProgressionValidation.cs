using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Editor.Character
{
    public static class OfflinePetProgressionValidation
    {
        public static void Run()
        {
            var legacy = new OfflinePetGachaProgress
            {
                OwnedPetIds = new List<int> { 1600 }
            };
            var pets = new OfflinePetProgress();
            OfflinePetProgress.Normalize(pets, legacy);
            Require(pets.Version == 1 && pets.Pets.Count == 1,
                "Legacy pet ownership must restore one official default instance per owned class.");

            OfflinePetInstance first = pets.Pets[0];
            Require(first.Index == 1 && first.ClassId == 1600 && first.Level == 1
                && first.EvolutionPhase == 1 && first.Abilities.Count == 0 && first.SkillRanks.Count == 0,
                "New pet instances must match the official childhood level-one defaults.");
            first.Abilities.Add(new OfflinePetAbilityLevel { NodeId = 1, Level = 1 });
            first.SkillRanks.Add(new OfflinePetSkillRank { SkillId = 16000, Rank = 1 });
            first.Level = 4;
            first.CombatRating = 125;
            first.EvolutionPhase = 2;
            first.Name = "存档幻兽";
            first.ChangeNameUnixSeconds = 1_700_000_000;
            first.ChangeNameCount = 1;
            first.PotentialLevel = 1;
            first.SlotIndex = 0;
            first.Status = 3;
            pets.SharedLevel = 4;
            pets.BlessSharedLevel = 2;
            pets.EnableUseGeneralPetPiece = true;
            pets.MaxObservedFeedExp = 1234;
            pets.Formation[0].PetIndex = first.Index;
            pets.Formation[0].Level = 4;
            pets.Formation[0].Exp = 250;
            pets.Formation[0].BlessLevel = 2;
            pets.SeasonRecords.Add(new OfflinePetSeasonRecord
            {
                SeasonId = 1,
                Slots = new List<OfflinePetSeasonSlot>
                {
                    new OfflinePetSeasonSlot { SlotIndex = 0, PetBlessLevel = 2, PetBlessExp = 12 }
                }
            });

            OfflinePetInstance second = OfflinePetProgress.AddPet(pets, 1600, 5);
            second.CombatRating = 250;
            pets.FollowPetIndex = second.Index;
            second.Status |= 4;

            var skills = new OfflineSkillProfile();
            OfflineSkillProgression.Normalize(skills);
            skills.Plans[0].PetIndex = first.Index;
            skills.Plans[1].PetIndex = first.Index;
            OfflineSkillPlan clone = skills.Plans[0].Clone();
            Require(clone.PetIndex == first.Index,
                "Cloning a skill plan must retain its pet instance index.");
            Require(OfflinePetProgress.IsValid(pets, skills),
                "A pet may be assigned to multiple plans while a different pet follows independently.");

            var save = new OfflineMainlineSave();
            save.Player.Pets = pets;
            save.Player.Skills = skills;
            OfflineMainlineSave restored = UnityEngine.JsonUtility.FromJson<OfflineMainlineSave>(
                UnityEngine.JsonUtility.ToJson(save));
            OfflinePetProgress.Normalize(restored.Player.Pets, restored.Player.PetGacha);
            OfflineSkillProgression.Normalize(restored.Player.Skills);
            OfflinePetInstance restoredFirst = restored.Player.Pets.Pets.First(value => value.Index == first.Index);
            Require(OfflinePetProgress.IsValid(restored.Player.Pets, restored.Player.Skills)
                && restoredFirst.Level == first.Level && restoredFirst.CombatRating == first.CombatRating
                && restoredFirst.EvolutionPhase == first.EvolutionPhase && restoredFirst.Name == first.Name
                && restoredFirst.ChangeNameUnixSeconds == first.ChangeNameUnixSeconds
                && restoredFirst.ChangeNameCount == first.ChangeNameCount
                && restoredFirst.PotentialLevel == first.PotentialLevel
                && restoredFirst.Abilities.Single().Level == first.Abilities.Single().Level
                && restoredFirst.SkillRanks.Single().Rank == first.SkillRanks.Single().Rank
                && restored.Player.Pets.Formation[0].Exp == 250
                && restored.Player.Pets.EnableUseGeneralPetPiece
                && restored.Player.Pets.SeasonRecords.Single().Slots.Single().PetBlessExp == 12,
                "Round-trip pet growth, formation experience, season blessing, and shared plan assignments");

            skills.Plans[2].PetIndex = 0;
            Require(!OfflinePetProgress.IsValid(pets, skills),
                "Zero is not a valid pet plan slot; only -1 clears an assignment.");
            skills.Plans[2].PetIndex = -1;

            skills.Plans[2].PetIndex = long.MaxValue;
            Require(!OfflinePetProgress.IsValid(pets, skills),
                "A skill plan must not reference an unowned pet instance.");
            DebugLog("Offline pet progression save validated.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void DebugLog(string message)
        {
            UnityEngine.Debug.Log(message);
        }
    }
}
