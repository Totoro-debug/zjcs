using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Explicit local policies confirmed by the user. Only the character-type exclusions come
    /// from official GameCharacterType.NotCheckFightResult; server result predicates are absent.
    /// A null policy on an older checkpoint retains the earlier local result behavior.
    /// </summary>
    [Serializable]
    public sealed class FightBattleResultRules
    {
        public const int Standard = 0;
        public const int Guard = 1;
        public const int Duel = 2;
        public const int InvalidDuelReason = 5;
        public const int GuardFailedReason = 6;

        public int Kind;
        public List<int> GuardUnitIds = new List<int>();

        public static bool Participates(int characterType) =>
            characterType == 1 || characterType == 2 || characterType == 4;

        public static bool CountsEnemy(FightBattleUnitState unit, FightBattleResultRules rules)
        {
            if (unit == null || unit.Side == 1 || unit.VoodooDoll != null) return false;
            return rules == null || (rules.Kind == Duel
                ? unit.CharacterType == 1 : Participates(unit.CharacterType));
        }

        public FightBattleOutcome Evaluate(FightBattleCheckpoint state, int round)
        {
            Validate(state);
            FightBattleUnitState player = state.Units.Find(unit => unit.Id == 1);
            long enemyHp = 0, enemyMax = 0;
            bool allyAlive = false, enemyAlive = false;
            foreach (FightBattleUnitState unit in state.Units)
            {
                if (CountsEnemy(unit, this))
                {
                    enemyHp += unit.Hp;
                    enemyMax += unit.MaxHp;
                    if (unit.Hp > 0) enemyAlive = true;
                }
                if (unit.VoodooDoll == null && unit.CharacterType == 1 && unit.Side == 1 && unit.Hp > 0)
                    allyAlive = true;
            }
            bool invalid = Kind == Duel && !allyAlive && !enemyAlive;
            bool defeated = Kind == Duel ? !allyAlive : player.Hp <= 0;
            bool guardFailed = Kind == Guard && GuardUnitIds.Exists(id =>
                state.Units.Find(unit => unit.Id == id).Hp <= 0);
            bool victory = Kind == Duel ? allyAlive && !enemyAlive
                : !defeated && !guardFailed && enemyHp == 0;
            bool timeout = round > state.MaxRound;
            bool playerRoundLimit = state.IsPlayerRoundLimit && state.PlayerRoundLimit > 0
                && player.SkillState.Round >= state.PlayerRoundLimit;
            bool finished = invalid || defeated || guardFailed || victory || timeout || playerRoundLimit;
            return new FightBattleOutcome
            {
                Finished = finished, Won = victory, Invalid = invalid,
                Reason = invalid ? InvalidDuelReason : defeated ? 2 : guardFailed ? GuardFailedReason
                    : victory ? 1 : timeout ? 3 : playerRoundLimit ? 4 : 0,
                Round = round, PlayerHp = player.Hp, EnemyHp = enemyHp, EnemyMaxHp = enemyMax
            };
        }

        public void Validate(FightBattleCheckpoint state)
        {
            if (Kind < Standard || Kind > Duel || GuardUnitIds == null)
                throw new ArgumentException("Local fight result policy is invalid.");
            if (state.Units.Find(unit => unit.Id == 1) == null)
                throw new ArgumentException("Local fight result policy requires the main player.");
            if (Kind == Guard)
            {
                if (GuardUnitIds.Count == 0) throw new ArgumentException("Guard result requires an explicit protected roster.");
                var seen = new HashSet<int>();
                foreach (int id in GuardUnitIds)
                    if (!seen.Add(id) || !state.Units.Exists(unit => unit.Id == id && unit.Side == 1))
                        throw new ArgumentException("Guard result contains an unknown, duplicate or opposing unit.");
            }
            if (Kind == Duel && (!state.Units.Exists(unit => unit.VoodooDoll == null && unit.CharacterType == 1 && unit.Side == 1)
                || !state.Units.Exists(unit => unit.VoodooDoll == null && unit.CharacterType == 1 && unit.Side != 1)))
                throw new ArgumentException("Duel result requires explicit player participants on both sides.");
        }
    }
}
