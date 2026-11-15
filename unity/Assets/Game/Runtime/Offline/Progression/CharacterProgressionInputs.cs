using System;
using System.Collections.Generic;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Skills;

namespace Zjcs.Offline.Progression
{
    public sealed class CharacterProgressionInputs
    {
        public OfficialCharacterCatalog Character { get; }
        public OfficialEquipmentCatalog Equipment { get; }
        public OfflineSkillRules Skills { get; }
        public IEnumerable<OfficialSkillRecommendation> Recommendations { get; }
        public Func<DateTime> NpcClock { get; }
        public Func<OfflinePlayerProgress, CharacterPropCalculator, CharacterExperienceState> ExperienceState { get; }

        public CharacterProgressionInputs(OfficialCharacterCatalog character, OfficialEquipmentCatalog equipment,
            OfflineSkillRules skills, IEnumerable<OfficialSkillRecommendation> recommendations = null,
            Func<DateTime> npcClock = null,
            Func<OfflinePlayerProgress, CharacterPropCalculator, CharacterExperienceState> experienceState = null)
        {
            Character = character; Equipment = equipment; Skills = skills;
            Recommendations = recommendations; NpcClock = npcClock;
            ExperienceState = experienceState;
        }
    }

    public sealed class CharacterProgressionInitializationException : InvalidOperationException
    {
        public string Stage { get; }
        internal CharacterProgressionInitializationException(string stage, Exception cause = null)
            : base("Character progression initialization failed: " + stage, cause) { Stage = stage; }
    }

    public sealed partial class OfflineCharacterProgressionService
    {
        // All production pages need complete stats; recommendations and a custom
        // NPC clock are optional. No partly assembled instance escapes on failure.
        public static OfflineCharacterProgressionService Create(OfflineMainlineProgressSession profile,
            CharacterProgressionInputs inputs)
        {
            if (profile == null) throw new CharacterProgressionInitializationException("profile");
            if (inputs?.Character == null) throw new CharacterProgressionInitializationException("character catalog");
            if (inputs.Equipment == null) throw new CharacterProgressionInitializationException("equipment catalog");
            if (inputs.Skills == null) throw new CharacterProgressionInitializationException("skill rules");
            string stage = "property pipeline";
            try
            {
                var instance = new OfflineCharacterProgressionService(profile, inputs.Character, inputs.NpcClock,
                    refreshDerivedProgress: inputs.ExperienceState == null,
                    experienceState: inputs.ExperienceState);
                stage = "equipment and gem configuration";
                instance.AttachEquipmentCatalog(inputs.Equipment);
                stage = "skill configuration";
                instance.AttachSkillRules(inputs.Skills, inputs.Recommendations);
                if (inputs.ExperienceState != null)
                {
                    stage = "experience rules";
                    instance.RefreshDerivedProgress(profile.Save.Player);
                }
                return instance;
            }
            catch (Exception error) { throw new CharacterProgressionInitializationException(stage, error); }
        }
    }
}
