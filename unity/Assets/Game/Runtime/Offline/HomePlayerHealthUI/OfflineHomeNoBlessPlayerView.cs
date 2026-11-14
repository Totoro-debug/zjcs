using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeNoBlessRules;

namespace Zjcs.Offline.HomePlayerHealthUI
{
    // A read-only calculation boundary: every changed value comes from the
    // original decorator; all other saved player fields remain unchanged.
    public sealed class OfflineHomeNoBlessPlayerView
    {
        // Original ItemType.ActiveSkill / PassiveSkill keys (also used by OfflineSkillProgression).
        private const int ActiveSkillItemType = 100;
        private const int PassiveSkillItemType = 101;
        public OfflinePlayerProgress Player { get; }
        public CharacterPropCalculationInputs Inputs { get; }

        public OfflineHomeNoBlessPlayerView(OfflinePlayerProgress player,
            Rank worldRank, uint activatedResonanceLevel, OfflineHomeNoBlessConfiguration rules)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (!Enum.IsDefined(typeof(Rank), worldRank)) throw new ArgumentOutOfRangeException(nameof(worldRank));
            if (activatedResonanceLevel > rules.Resonances.MaxLevel) throw new ArgumentOutOfRangeException(nameof(activatedResonanceLevel));
            Player = JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(player));
            var source = new PlayerInput(Player, worldRank, activatedResonanceLevel);
            var original = new NoBlessPlayerInfoProvider<PetInput>(source, rules);
            Player.Level = original.MyLevel;
            var equipmentLevels = original.MyEquipUpgradeLevel;
            foreach (var slot in Player.Equipment.SlotProgress)
            {
                slot.BaseLevel = equipmentLevels[slot.Position];
                slot.BlessLevels.Clear();
            }
            var skillLevels = original.MySkillSlotLevelData;
            for (int slot = 0; slot < Player.Skills.ActiveSlotLevels.Length; slot++)
                Player.Skills.ActiveSlotLevels[slot] = skillLevels[ActiveSkillItemType][slot];
            for (int slot = 0; slot < Player.Skills.PassiveSlotLevels.Length; slot++)
                Player.Skills.PassiveSlotLevels[slot] = skillLevels[PassiveSkillItemType][slot];
            Player.Skills.BlessRecords.Clear();
            Player.Pets.Pets = original.MyPetDatas.Values.Select(pet => pet.Data).ToList();
            Inputs = new CharacterPropCalculationInputs((int)original.MyPlanesRank, original.MySeasonId,
                new Dictionary<int,long>(original.MyEquipResonance));
        }

        public CharacterPropSnapshot Evaluate(CharacterPropCalculator calculator, OfflineWorldProgress world, long nowUnixSeconds)
        {
            if (calculator == null) throw new ArgumentNullException(nameof(calculator));
            return calculator.Evaluate(Player, world, nowUnixSeconds, Inputs);
        }

        private sealed class PetInput : INoBlessPet<PetInput>
        {
            public OfflinePetInstance Data { get; }
            public PetInput(OfflinePetInstance data) { Data = data; }
            public int Level { get => Data.Level; set => Data.Level = value; }
            public PetInput CloneWrapForAnother() => new PetInput(JsonUtility.FromJson<OfflinePetInstance>(JsonUtility.ToJson(Data)));
        }

        private sealed class PlayerInput : INoBlessPlayerInput<PetInput>
        {
            public Rank MyPlanesRank { get; }
            public SubRank MySubRank { get; }
            public int MyLevel { get; }
            public uint MyActivedEquipResonanceLevel { get; }
            public Dictionary<int,int> MyEquipUpgradeLevel { get; }
            public Dictionary<long,PetInput> MyPetDatas { get; }
            public Dictionary<int,Dictionary<int,int>> MySkillSlotLevelData { get; }

            public PlayerInput(OfflinePlayerProgress player, Rank worldRank, uint activatedResonance)
            {
                MyPlanesRank = worldRank; MySubRank = (SubRank)player.SubRank;
                MyLevel = player.Level; MyActivedEquipResonanceLevel = activatedResonance;
                // Existing offline equipment/skill records use the rank key in
                // SeasonId (see SkillPlayerContext). Select the explicit world
                // rank, as the original provider does, rather than personal rank.
                int recordKey = (int)worldRank;
                MyEquipUpgradeLevel = player.Equipment.SlotProgress.ToDictionary(slot => slot.Position,
                    slot => slot.BaseLevel + (slot.BlessLevels.FirstOrDefault(row => row.SeasonId == recordKey)?.Level ?? 0));
                MyPetDatas = player.Pets.Pets.ToDictionary(pet => pet.Index, pet => new PetInput(pet));
                var blessing = player.Skills.BlessRecords.FirstOrDefault(row => row.SeasonId == recordKey);
                MySkillSlotLevelData = new Dictionary<int,Dictionary<int,int>>
                {
                    [ActiveSkillItemType] = Slots(player.Skills.ActiveSlotLevels, blessing?.ActiveSlotLevels),
                    [PassiveSkillItemType] = Slots(player.Skills.PassiveSlotLevels, blessing?.PassiveSlotLevels)
                };
            }
            private static Dictionary<int,int> Slots(int[] levels, int[] blessing)
                => Enumerable.Range(0, levels.Length).ToDictionary(index => index, index => levels[index] + (blessing?[index] ?? 0));
        }
    }
}
