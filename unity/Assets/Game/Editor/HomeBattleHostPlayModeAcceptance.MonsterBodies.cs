using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static void VerifyConfiguredMonsterBodies(MonoBehaviour runner, FightBattleCheckpoint checkpoint)
        {
            var combat = OfficialCombatAssetCatalog.LoadDefault().Load();
            var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            foreach (var unit in checkpoint.Units)
            {
                if (unit.MonsterId <= 0 || unit.CharacterType == 1 || unit.MasterId > 0) continue;
                var expected = combat.MonsterBodyOffsets(unit.MonsterId);
                bool SameBody(FightBattleUnitState value) => value.BodyOffsets.Count == expected.Count
                    && expected.All(point => value.BodyOffsets.Any(actual => actual.X == point.X && actual.Y == point.Y));
                Require(expected.Count > 0 && SameBody(unit)
                    && unit.MonsterTemplateId == combat.MonsterTemplateId(unit.MonsterId),
                    "Formal monster lost its original template or occupied body cells: " + unit.MonsterId);
                var saved = restored.Units.Single(value => value.Id == unit.Id);
                Require(SameBody(saved) && saved.MonsterTemplateId == unit.MonsterTemplateId,
                    "Checkpoint restore lost the original monster footprint: " + unit.MonsterId);
                if (unit.MonsterId == 3803)
                    Require(expected.Count == 25 && expected.Any(point => point.X == -2 && point.Y == -2)
                        && expected.Any(point => point.X == 2 && point.Y == 2),
                        "Original 3803 five-by-five body was reduced to a point.");
            }
        }
    }
}
