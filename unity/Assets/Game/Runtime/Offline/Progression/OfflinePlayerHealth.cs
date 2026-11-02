using System;
using System.Collections.Generic;
using Zjcs.Official.PlayerHealth;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflinePlayerHealthProgress
    {
        public float HpPercentVal = 1f;
        public long HpUpdateTimeTicks = DateTime.UtcNow.Ticks;

        public static bool IsValid(OfflinePlayerHealthProgress value) => value != null
            && !float.IsNaN(value.HpPercentVal) && !float.IsInfinity(value.HpPercentVal)
            && value.HpPercentVal >= 0f && value.HpPercentVal <= 1f
            && value.HpUpdateTimeTicks >= DateTime.MinValue.Ticks
            && value.HpUpdateTimeTicks <= DateTime.MaxValue.Ticks;
    }

    /// <summary>Live Common.Player health projection over the durable local profile.</summary>
    public sealed class OfflinePlayerHealth
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialPropCatalog props;
        private readonly Func<DateTime, IReadOnlyDictionary<int, long>> nonBattleProps;
        private readonly Func<int> mapCount;
        private readonly Func<IReadOnlyList<bool>> currentGamesUsePlayerHp;
        private readonly Func<DateTime> clock;
        private readonly int hpAdd;
        private readonly int hpAddPercent;

        public OfflinePlayerHealth(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService character, Func<int> mapCount,
            Func<IReadOnlyList<bool>> currentGamesUsePlayerHp, Func<DateTime> clock = null)
            : this(session, character?.Catalog.Props,
                now => character.EvaluateProps(new DateTimeOffset(now).ToUnixTimeSeconds()).NonBattleProps,
                mapCount, currentGamesUsePlayerHp, clock) { }

        public OfflinePlayerHealth(OfflineMainlineProgressSession session, OfficialPropCatalog props,
            Func<DateTime, IReadOnlyDictionary<int, long>> nonBattleProps, Func<int> mapCount,
            Func<IReadOnlyList<bool>> currentGamesUsePlayerHp, Func<DateTime> clock = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.props = props ?? throw new ArgumentNullException(nameof(props));
            this.nonBattleProps = nonBattleProps ?? throw new ArgumentNullException(nameof(nonBattleProps));
            this.mapCount = mapCount ?? throw new ArgumentNullException(nameof(mapCount));
            this.currentGamesUsePlayerHp = currentGamesUsePlayerHp
                ?? throw new ArgumentNullException(nameof(currentGamesUsePlayerHp));
            this.clock = clock ?? (() => DateTime.UtcNow);
            hpAdd = props.Id("HangUpHpAdd");
            hpAddPercent = props.Id("HangUpHpAddPercent");
            if (props.Get(hpAdd) == null || props.Get(hpAddPercent) == null)
                throw new InvalidOperationException("The official hang-up health property definitions are required.");
        }

        public event Action<float> HpUpdated;
        public int MapCount => mapCount();
        public float HpPercent
        {
            get
            {
                OfflinePlayerHealthProgress health = session.Save.Player.Health;
                // Preserve the original short circuit: a map never queries
                // battle configuration; a blocked recovery never reads the clock.
                int maps = MapCount;
                if (maps > 0) return health.HpPercentVal;
                IReadOnlyList<bool> games = currentGamesUsePlayerHp();
                if (!PlayerHealthRules.CanAutoAddHpInFight(games)) return health.HpPercentVal;
                DateTime now = clock().ToUniversalTime();
                return PlayerHealthRules.HpPercent(health.HpPercentVal,
                    new DateTime(health.HpUpdateTimeTicks, DateTimeKind.Utc), now,
                    maps, games, () =>
                    {
                        IReadOnlyDictionary<int, long> values = nonBattleProps(now);
                        return PlayerHealthRules.GetHangUpHpAddSpeed(
                            CharacterPropFormula.Value(values, hpAdd, props),
                            CharacterPropFormula.Value(values, hpAddPercent, props));
                    });
            }
        }

        public ProgressionResult SetHpPercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                return new ProgressionResult { Status = ProgressionResultStatus.InvalidState,
                    Message = "Player health must be a finite proportion between zero and one." };
            session.Reload();
            long ticks = clock().ToUniversalTime().Ticks;
            ProgressionResult result = session.ApplyCharacterMutation(player =>
            {
                player.Health.HpUpdateTimeTicks = ticks;
                player.Health.HpPercentVal = value;
            });
            // Common.Player's setter publishes after its state update. Failed local
            // persistence must not publish an HP value the profile did not accept.
            if (result.IsApplied) HpUpdated?.Invoke(value);
            return result;
        }

        public ProgressionResult RefreshHpPercent() => SetHpPercent(HpPercent);
    }
}
