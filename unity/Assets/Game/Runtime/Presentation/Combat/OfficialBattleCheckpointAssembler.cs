using System;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Mode-specific inputs used to create a shared battle checkpoint.</summary>
    public sealed class OfficialBattleCheckpointOptions
    {
        public int StageId;
        public int StageType;
        public int Mode;
        public bool IsPvp;
        public int MaxRound = 25;
        public bool IsPlayerRoundLimit;
        public int PlayerRoundLimit;
        public int RandomSeed;
        public FightBattleGrid Grid;
        public FightBattleResultRules ResultRules;
        public FightDamageTestRules DamageTest;
        public FightStageStatusCatalog StageStatuses;
        public int StageStatusClassId;
    }

    /// <summary>
    /// Creates the serialized state shared by every battle mode. Mode adapters retain ownership of
    /// roster construction and rewards while this assembler keeps checkpoint defaults consistent.
    /// </summary>
    public static class OfficialBattleCheckpointAssembler
    {
        public static FightBattleCheckpoint Create(OfficialBattleCheckpointOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.StageId <= 0) throw new InvalidOperationException("Official battle stage id is invalid.");
            if (options.StageType <= 0) throw new InvalidOperationException("Official battle stage type is invalid.");
            if (options.MaxRound <= 0) throw new InvalidOperationException("Official battle max round is invalid.");
            if (options.IsPlayerRoundLimit && options.PlayerRoundLimit <= 0)
                throw new InvalidOperationException("Official battle player round limit is invalid.");
            if (options.Grid == null) throw new InvalidOperationException("Official battle grid is missing.");

            var checkpoint = new FightBattleCheckpoint
            {
                StageId = options.StageId,
                StageType = options.StageType,
                Mode = options.Mode,
                IsPvp = options.IsPvp,
                MaxRound = options.MaxRound,
                IsPlayerRoundLimit = options.IsPlayerRoundLimit,
                PlayerRoundLimit = options.PlayerRoundLimit,
                RandomSeed = options.RandomSeed,
                Grid = options.Grid,
                ResultRules = options.ResultRules,
                DamageTest = options.DamageTest
            };

            if (options.StageStatuses != null)
            {
                int classId = options.StageStatusClassId > 0
                    ? options.StageStatusClassId : options.StageId;
                checkpoint.StageStatus = options.StageStatuses.CreateCheckpointConfig(classId);
            }
            return checkpoint;
        }
    }
}
