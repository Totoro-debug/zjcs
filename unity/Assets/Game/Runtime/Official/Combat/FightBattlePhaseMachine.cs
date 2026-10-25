using System;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Official battle phases (<c>GameStatus</c>). A mode does not necessarily visit every phase;
    /// the machine only enforces the legal order between the phases it does enter.
    /// </summary>
    public enum FightBattlePhase
    {
        Preparing,
        PlayStart,
        Play,
        Pause,
        EntryTransition,
        EntryTransitionFinished,
        BattleEnd,
        BattleEndTransition,
        BattleEndTransitionFinished,
        End
    }

    /// <summary>
    /// Shared battle phase state machine used by every mode. It owns the prepare countdown and the
    /// legal phase order; the mode decides which phases it uses and performs the actual scene, HUD
    /// and timing work on <see cref="PhaseChanged"/>.
    /// </summary>
    public sealed class FightBattlePhaseMachine
    {
        private double countdown;
        private bool autoStartRaised;

        public FightBattlePhase Phase { get; private set; } = FightBattlePhase.Preparing;
        public bool IsFinished => Phase == FightBattlePhase.End;
        public bool IsBattleActive => Phase == FightBattlePhase.PlayStart
            || Phase == FightBattlePhase.Play || Phase == FightBattlePhase.EntryTransition;

        public double PrepareSeconds { get; set; }
        public double CountdownRemaining => countdown;
        public bool IsPreparing => Phase == FightBattlePhase.Preparing;

        public event Action<FightBattlePhase, FightBattlePhase> PhaseChanged;
        public event Action AutoStartRequested;
        public event Action<double> CountdownChanged;

        public FightBattlePhaseMachine(double prepareSeconds = 0)
        {
            PrepareSeconds = prepareSeconds < 0 ? 0 : prepareSeconds;
        }

        /// <summary>
        /// Enters the first phase. A stage that skips preparation starts at <c>PlayStart</c>.
        /// Otherwise preparation waits for readiness, with an optional automatic-start deadline.
        /// </summary>
        public void Begin(bool skipPrepare)
        {
            if (Phase != FightBattlePhase.Preparing) throw new InvalidOperationException("Battle session already started.");
            if (skipPrepare)
            {
                countdown = 0;
                Transition(FightBattlePhase.PlayStart);
                return;
            }
            countdown = PrepareSeconds;
            autoStartRaised = false;
            CountdownChanged?.Invoke(countdown);
        }

        /// <summary>Player confirmed ready: stops the countdown and starts the battle.</summary>
        public bool Ready()
        {
            if (Phase != FightBattlePhase.Preparing) return false;
            countdown = 0;
            autoStartRaised = true;
            CountdownChanged?.Invoke(0);
            return Transition(FightBattlePhase.PlayStart);
        }

        /// <summary>Advances the prepare countdown; raises <see cref="AutoStartRequested"/> once at zero.</summary>
        public void Tick(double unscaledDelta)
        {
            if (Phase != FightBattlePhase.Preparing || countdown <= 0) return;
            countdown = Math.Max(0, countdown - Math.Max(0, unscaledDelta));
            CountdownChanged?.Invoke(countdown);
            if (countdown <= 0 && !autoStartRaised)
            {
                autoStartRaised = true;
                AutoStartRequested?.Invoke();
            }
        }

        public bool StartPlay()
        {
            if (Phase == FightBattlePhase.Play) return true;
            if (Phase != FightBattlePhase.PlayStart && Phase != FightBattlePhase.EntryTransitionFinished)
                return false;
            return Transition(FightBattlePhase.Play);
        }

        public bool Pause()
        {
            return Phase == FightBattlePhase.Play && Transition(FightBattlePhase.Pause);
        }

        public bool Resume()
        {
            return Phase == FightBattlePhase.Pause && Transition(FightBattlePhase.Play);
        }

        public bool BeginEntryTransition()
        {
            return Phase == FightBattlePhase.Play && Transition(FightBattlePhase.EntryTransition);
        }

        public bool FinishEntryTransition()
        {
            return Phase == FightBattlePhase.EntryTransition
                && Transition(FightBattlePhase.EntryTransitionFinished);
        }

        public bool BeginBattleEnd()
        {
            if (Phase != FightBattlePhase.Play && Phase != FightBattlePhase.PlayStart
                && Phase != FightBattlePhase.Pause) return false;
            return Transition(FightBattlePhase.BattleEnd);
        }

        public bool BeginBattleEndTransition()
        {
            return Phase == FightBattlePhase.BattleEnd && Transition(FightBattlePhase.BattleEndTransition);
        }

        public bool FinishBattleEndTransition()
        {
            return Phase == FightBattlePhase.BattleEndTransition
                && Transition(FightBattlePhase.BattleEndTransitionFinished);
        }

        public bool Finish()
        {
            return Transition(FightBattlePhase.End);
        }

        public static bool IsLegal(FightBattlePhase from, FightBattlePhase to)
        {
            if (from == to) return false;
            switch (from)
            {
                case FightBattlePhase.Preparing:
                    return to == FightBattlePhase.PlayStart || to == FightBattlePhase.End;
                case FightBattlePhase.PlayStart:
                    return to == FightBattlePhase.Play || to == FightBattlePhase.BattleEnd
                        || to == FightBattlePhase.End;
                case FightBattlePhase.Play:
                    return to == FightBattlePhase.Pause || to == FightBattlePhase.EntryTransition
                        || to == FightBattlePhase.BattleEnd || to == FightBattlePhase.End;
                case FightBattlePhase.Pause:
                    return to == FightBattlePhase.Play || to == FightBattlePhase.BattleEnd
                        || to == FightBattlePhase.End;
                case FightBattlePhase.EntryTransition:
                    return to == FightBattlePhase.EntryTransitionFinished || to == FightBattlePhase.End;
                case FightBattlePhase.EntryTransitionFinished:
                    return to == FightBattlePhase.Play || to == FightBattlePhase.BattleEnd
                        || to == FightBattlePhase.End;
                case FightBattlePhase.BattleEnd:
                    return to == FightBattlePhase.BattleEndTransition || to == FightBattlePhase.End;
                case FightBattlePhase.BattleEndTransition:
                    return to == FightBattlePhase.BattleEndTransitionFinished || to == FightBattlePhase.End;
                case FightBattlePhase.BattleEndTransitionFinished:
                    return to == FightBattlePhase.End;
                default:
                    return false;
            }
        }

        public bool Transition(FightBattlePhase next)
        {
            if (!IsLegal(Phase, next)) return false;
            FightBattlePhase previous = Phase;
            Phase = next;
            if (next == FightBattlePhase.End)
            {
                countdown = 0;
                CountdownChanged?.Invoke(0);
            }
            PhaseChanged?.Invoke(previous, next);
            return true;
        }
    }
}
