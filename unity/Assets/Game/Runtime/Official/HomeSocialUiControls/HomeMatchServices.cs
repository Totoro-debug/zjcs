using System;
namespace Zjcs.Official.HomeSocialUiControls
{
    public interface IHomeMatchTarget
    {
        TeamTargetCatalog TargetCatalog { get; }
        string GetItemName();
    }
    public interface IHomeMatchInfo
    {
        DateTime MatchStartTime { get; }
        DateTime MatchTargetTime { get; }
        int AlreadyWaitSec { get; }
        IHomeMatchTarget MatchTarget { get; }
    }
    public interface IHomeMatchPlayer
    {
        event Action OnGameMatchUpdate;
        bool IsInGameMatch { get; }
        IHomeMatchInfo GameMatchInfo { get; }
    }
    public interface IHomeMatchServices
    {
        IHomeMatchPlayer Player { get; }
        bool IsLeader { get; }
        DateTime GetDateTime();
        string GetText(string key);
        IDisposable SubscribeConfirmStart(Action listener);
        void OperPlayerStopGameMatch(Action<object> onSuccess);
    }
}
