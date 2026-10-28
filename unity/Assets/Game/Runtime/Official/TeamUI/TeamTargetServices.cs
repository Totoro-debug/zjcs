using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // The parser/localization boundaries consumed by the original target widgets.
    public interface ITeamTargetServices
    {
        int CategorySort(TeamTargetCatalog catalog);
        bool HasMultipleTargets(TeamTargetCatalog catalog);
        int TeamMaxPlayer(TeamTargetCatalog catalog);
        string EnumName(TeamTargetCatalog catalog);
        string Localize(string key);
        string Format(string key, params object[] arguments);
        int CompareDelegateTargets(TeamTargetData left, TeamTargetData right);
        void ShowError(int errorCode, bool useDefaultTip);
    }

    // Consumed Common.ITeamTarget projection; no transport or server implementation.
    public abstract class TeamTargetData : IWorldTeamInviteTarget
    {
        protected readonly ITeamTargetServices Services;
        public TeamTargetCatalog TargetCatalog;
        protected TeamTargetData(ITeamTargetServices services) { Services = services; }
        public virtual string ToTeamTargetKey() => $"{nameof(TeamTargetCatalog)}.{TargetCatalog}";
        public virtual string GetTargetName() => ToTeamTargetKey();
        public virtual string GetItemName() => Services.EnumName(TargetCatalog);
        public virtual int GetTeamMaxPlayer() => Services.TeamMaxPlayer(TargetCatalog);
        public virtual bool CanInviteNpc() => true;
        public virtual bool CanInviteOffline() => false;
        public virtual bool TryGetLimitErrorTip(bool isSelf, int errorCode, out string tip)
        {
            if (errorCode != 1145) { tip = string.Empty; return false; }
            tip = Services.Format(isSelf ? "chat_hyper_team_invite_target_limit_self" :
                "chat_hyper_team_invite_target_limit_other", Services.Localize(GetTargetName()));
            return true;
        }
        public override bool Equals(object obj) => obj is TeamTargetData other && TargetCatalog == other.TargetCatalog;
        public override int GetHashCode() => TargetCatalog.GetHashCode();
        public override string ToString() => ToTeamTargetKey();
    }
}
