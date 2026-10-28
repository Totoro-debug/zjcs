using System;

namespace Zjcs.Official.PlayerPlay
{
    // Original PlayerPlayConfirmEnv. Retain the original OrigninalFunc spelling.
    public sealed class PlayerPlayConfirmEnv : IPlayerPlayActionEnv
    {
        public Action FinalExecFunc;
        public Action OrigninalFunc;
        public Func<PlayerPlayStateType, PlayerPlayText> MergeTextFunc;
        public bool PopConfirmFlag;
    }
}
