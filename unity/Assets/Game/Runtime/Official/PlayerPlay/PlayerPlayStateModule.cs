using System;
using System.Linq;
using System.Threading.Tasks;

namespace Zjcs.Official.PlayerPlay
{
    // Recovered from PlayerPlayStateModule's selected official async state machine.
    // The action gates its callback; it does not play a character animation.
    public sealed class PlayerPlayStateModule
    {
        private static readonly int sMaxSoftMutexExitLoopCount =
            Enum.GetValues(typeof(PlayerPlayStateType)).Length + 1;
        private readonly IPlayerPlayServices services;

        public PlayerPlayStateModule(IPlayerPlayServices services)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public async Task DoAction(IPlayerPlayAction action, Action execFunc, IPlayerPlayActionEnv env = null)
        {
            if (await TryDoAction(action, env)) execFunc?.Invoke();
        }

        public async Task<bool> TryDoAction(IPlayerPlayAction action, IPlayerPlayActionEnv env = null)
        {
            IPlayerPlayStateSource player = services.MainPlayer;
            if (player == null) return false;
            if (TryNotifyHardMutexState(action, env)) return false;
            bool anyProcess = false;
            bool allExited = false;
            int i = 0;
            while (i < sMaxSoftMutexExitLoopCount)
            {
                var states = player.GetStates();
                var soft = PlayerPlayStateMutexConfig.FilterStateByMutexType(
                    PlayerPlayStateMutexType.SoftMutex, action, states);
                if (!soft.Any()) { allExited = true; break; }
                PlayerPlayStateExitHandleBase handle = null;
                foreach (IPlayerPlayState state in soft)
                {
                    handle = GetExitHandle(state, action, env);
                    if (handle != null) break;
                }
                if (handle == null) { allExited = true; break; }
                anyProcess = true;
                if (!await handle.Exit()) return false;
                i++;
            }
            if (allExited) return !anyProcess || !TryNotifyHardMutexState(action, env);
            services.Error("PlayerPlayStateModule.TryDoAction 软互斥状态退出未收敛, action:" + action?.Type);
            return false;
        }

        private bool TryNotifyHardMutexState(IPlayerPlayAction action, IPlayerPlayActionEnv env)
        {
            var states = services.MainPlayer.GetStates();
            var hard = PlayerPlayStateMutexConfig.FilterStateByMutexType(
                PlayerPlayStateMutexType.HardMutex, action, states);
            if (!hard.Any()) return false;
            var text = new PlayerPlayText("ui_play_state_hard_mutex");
            foreach (IPlayerPlayState state in hard)
            {
                PlayerPlayStateExitHandleBase handle = GetExitHandle(state, action, env);
                if (handle == null) continue;
                text = handle.GetHardMutexText();
                break;
            }
            services.Notify(text);
            return true;
        }

        private PlayerPlayStateExitHandleBase GetExitHandle(IPlayerPlayState state,
            IPlayerPlayAction action, IPlayerPlayActionEnv env)
        {
            switch (state.Type)
            {
                case PlayerPlayStateType.GameMatch:
                case PlayerPlayStateType.Fight:
                case PlayerPlayStateType.RoomMatch:
                case PlayerPlayStateType.Room:
                case PlayerPlayStateType.Team:
                case PlayerPlayStateType.DreamDance:
                case PlayerPlayStateType.ChatAction:
                    return services.GetExitHandle(state, action, env);
                default: return null;
            }
        }
    }
}
