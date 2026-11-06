using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleLegacyBodyRestoreValidation
    {
        // Supply a checkpoint captured by the real weekly host. No test save is
        // bundled into the game or substituted for a player's profile.
        public static void Run()
        {
            var path = Environment.GetEnvironmentVariable("ZJCS_BATTLE_CHECKPOINT_FIXTURE");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException("Supply the real weekly host checkpoint fixture.");
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            string initial = File.ReadAllText(path);
            CheckLegacy(assets, initial);
            var advancing = assets.RestoreBattle(FightBattleCheckpointSerializer.Load(initial));
            for (int i = 0; i < 8 && !advancing.State.Finished; i++)
                advancing.Step(advancing.State.Sequence, advancing.State.Counter + 1);
            Require(advancing.State.Counter > 0, "The real fixture did not advance.");
            string advanced = FightBattleCheckpointSerializer.Save(advancing.State);
            CheckLegacy(assets, advanced);
            CheckPreserved(assets, advanced, unit =>
            {
                unit.MonsterTemplateId = 0;
                unit.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0), new FightGridPoint(1, 0) };
            });
            CheckPreserved(assets, advanced, unit =>
            {
                unit.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0) };
            });
            CheckPreserved(assets, advanced, unit =>
            {
                unit.MonsterTemplateId = 0;
                unit.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0) };
                unit.CharacterType = 1;
            });
            CheckPreserved(assets, advanced, unit =>
            {
                unit.MonsterTemplateId = 0;
                unit.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0) };
                unit.MasterId = unit.Turn.MasterId = 1;
            });
            Debug.Log("[Legacy monster restore] Initial and advanced real checkpoints recover the original five by five boss body; all serialized combat state matches normal restore. Explicit bodies, templates, role players and summons retain their saved identity.");
        }

        private static FightBattleUnitState Boss(FightBattleCheckpoint checkpoint)
        {
            return checkpoint.Units.Single(unit => unit.MonsterId == 3803 && unit.MasterId == 0);
        }

        private static void CheckLegacy(OfficialCombatAssetCatalog assets, string payload)
        {
            var expected = assets.RestoreBattle(FightBattleCheckpointSerializer.Load(payload)).State;
            Require(Boss(expected).MonsterTemplateId == 3803 && Boss(expected).BodyOffsets.Count == 25,
                "The input fixture does not contain the original assembled boss.");
            var old = FightBattleCheckpointSerializer.Load(payload);
            var boss = Boss(old);
            boss.MonsterTemplateId = 0;
            boss.BodyOffsets = new List<FightGridPoint> { new FightGridPoint(0, 0) };
            // Round trip the actual legacy-shaped payload before restoring it.
            var actual = assets.RestoreBattle(FightBattleCheckpointSerializer.Load(
                FightBattleCheckpointSerializer.Save(old))).State;
            Require(Boss(actual).MonsterTemplateId == 3803 && Boss(actual).BodyOffsets.Count == 25,
                "Legacy restore left the boss template missing or its body reduced to one cell.");
            Require(FightBattleCheckpointSerializer.Save(actual) == FightBattleCheckpointSerializer.Save(expected),
                "Legacy body repair changed another saved combat field.");
        }

        private static void CheckPreserved(OfficialCombatAssetCatalog assets, string payload,
            Action<FightBattleUnitState> arrange)
        {
            var input = FightBattleCheckpointSerializer.Load(payload);
            var boss = Boss(input);
            arrange(boss);
            int template = boss.MonsterTemplateId;
            var body = boss.BodyOffsets.ToArray();
            var actual = assets.RestoreBattle(FightBattleCheckpointSerializer.Load(
                FightBattleCheckpointSerializer.Save(input))).State.Units.Single(unit => unit.Id == boss.Id);
            Require(actual.MonsterTemplateId == template && actual.BodyOffsets.SequenceEqual(body),
                "Legacy repair overwrote an explicitly saved identity or a separate role/summon assembly.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Legacy monster restore: " + message);
        }
    }
}
