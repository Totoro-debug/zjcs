using System.Collections.Generic;
using System.Threading.Tasks;

namespace Zjcs.Official.PlayerPlay
{
    public interface IPlayerPlayActionEnv { }

    public interface IPlayerPlayStateSource
    {
        IEnumerable<IPlayerPlayState> GetStates();
    }

    public readonly struct PlayerPlayText
    {
        public readonly string Key;
        public readonly object[] Args;
        public PlayerPlayText(string key, params object[] args) { Key = key; Args = args; }
    }

    public abstract class PlayerPlayStateExitHandleBase
    {
        protected readonly IPlayerPlayState mState;
        protected readonly IPlayerPlayAction mAction;
        protected readonly IPlayerPlayActionEnv mEnv;

        protected PlayerPlayStateExitHandleBase(IPlayerPlayState state, IPlayerPlayAction action,
            IPlayerPlayActionEnv env)
        {
            mState = state;
            mAction = action;
            mEnv = env;
        }

        public abstract PlayerPlayText GetHardMutexText();
        public abstract Task<bool> Exit();
    }

    // Original global player / concrete exit handlers / language broadcast boundaries.
    public interface IPlayerPlayServices
    {
        IPlayerPlayStateSource MainPlayer { get; }
        PlayerPlayStateExitHandleBase GetExitHandle(IPlayerPlayState state, IPlayerPlayAction action,
            IPlayerPlayActionEnv env);
        void Notify(PlayerPlayText text);
        void Error(string message);
    }
}
