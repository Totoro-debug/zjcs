using System;
using System.IO;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        internal static void ConfigureAcquiredBattleRandomInput(FightBattleCheckpoint checkpoint)
        {
            string configured = Environment.GetEnvironmentVariable("ZJCS_SKILLS_BATTLE_SEED");
            if (string.IsNullOrWhiteSpace(configured)) return;
            Require(int.TryParse(configured, out int seed) && seed > 0 && seed < FightRandomStream.Modulus
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_ACTIVE")),
                "An explicit acquired-skill diagnostic requires a valid battle random seed.");
            checkpoint.RandomSeed = seed;
            string directory = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Require(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory),
                "The acquired-skill diagnostic output directory is absent.");
            File.WriteAllText(Path.Combine(directory, "acquired-battle-initial.json"),
                FightBattleCheckpointSerializer.Save(checkpoint));
        }
    }
}
