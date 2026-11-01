using System.Collections.Generic;
using System.IO;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private static FightBattleUnitState CreateMonsterBattleUnit(FightOfficialCombatCatalog combatCatalog, FightDamageFormula formula,
            Map10RuntimeMonster source, Dictionary<int, long> properties, int monsterLevel,
            int monsterSubRank, int id, int moveMask)
        {
            var unit = new FightBattleUnitState
            {
                Id = id,
                Side = 2,
                CharacterType = 2,
                MonsterId = source.MonsterId,
                MonsterGroupNo = source.MonsterGroupNo,
                MonsterType = source.MonsterType,
                MaxHpPlies = combatCatalog.MonsterHpPliesFor(source.MonsterId,
                    source.MonsterGroupNo, monsterLevel),
                Level = monsterLevel,
                SubRank = monsterSubRank,
                Wave = source.WaveIndex,
                X = source.Position.X,
                Y = source.Position.Y,
                MoveMask = moveMask
            };
            OfficialBattleUnitAssembler.Initialize(formula, unit, properties, 3, 4, true);
            return unit;
        }
    }
}
