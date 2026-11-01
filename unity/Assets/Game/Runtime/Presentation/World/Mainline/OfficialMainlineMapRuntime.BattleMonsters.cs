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
            long maxHp = OfficialBattlePropertyAssembler.Value(properties, 3);
            if (maxHp <= 0) throw new InvalidDataException("Official Map10 monster MaxHp is not positive: " + source.MonsterId);
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
                Hp = maxHp,
                MaxHp = maxHp,
                LastMaxHp = maxHp,
                MoveMask = moveMask,
                Properties = OfficialBattlePropertyAssembler.ToProperties(properties)
            };
            unit.MoveDist = formula.CalculateMoveDist(properties);
            unit.Turn.Id = unit.Id;
            unit.Turn.Hp = unit.Hp;
            unit.Turn.Speed = OfficialBattlePropertyAssembler.CheckedValue(properties, 4);
            return unit;
        }
    }
}
