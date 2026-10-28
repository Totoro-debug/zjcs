using System;
using System.Threading.Tasks;

namespace Zjcs.Official.PlayerPlay
{
    // Selected original team methods; async body recovered from MoveNext.
    public static class PlayerPlayStateClientUtils
    {
        public static readonly PlayerPlayTeamAction sTeamAction = new PlayerPlayTeamAction();

        public static Task DoTeamAction(PlayerPlayStateModule module, Action execFunc)
            => module.DoAction(sTeamAction, execFunc);

        public static Task DoTeamActionWithConfirm(PlayerPlayStateModule module,
            Action finalExecFunc, Action confirmFunc,
            Func<PlayerPlayStateType, PlayerPlayText> textFunc)
            => DoTeamActionWithConfirm(module,
                CreatePlayerPlayConfirmEnv(finalExecFunc, confirmFunc, textFunc));

        public static Task DoTeamActionWithConfirm(PlayerPlayStateModule module,
            PlayerPlayConfirmEnv env) => AsyncDoActionWithConfirm(module, sTeamAction, env);

        public static PlayerPlayConfirmEnv CreatePlayerPlayConfirmEnv(Action finalExecFunc,
            Action confirmFunc, Func<PlayerPlayStateType, PlayerPlayText> textFunc)
            => new PlayerPlayConfirmEnv
            {
                FinalExecFunc = finalExecFunc,
                OrigninalFunc = confirmFunc,
                MergeTextFunc = textFunc,
                PopConfirmFlag = false
            };

        public static async Task AsyncDoActionWithConfirm(PlayerPlayStateModule module,
            IPlayerPlayAction action, PlayerPlayConfirmEnv env)
        {
            if (await module.TryDoAction(action, env))
            {
                if (env.PopConfirmFlag) env.FinalExecFunc?.Invoke();
                else env.OrigninalFunc?.Invoke();
            }
        }
    }
}
