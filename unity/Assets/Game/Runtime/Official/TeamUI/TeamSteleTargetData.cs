using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    // Original TeamTargetBattleStele value identity. Its UI key is inherited unchanged.
    public sealed class TeamSteleTargetData : TeamTargetData
    {
        public int StageId, GroupId;
        public TeamSteleTargetData(ITeamTargetServices services, int stageId, int groupId) : base(services)
        { TargetCatalog = TeamTargetCatalog.BattleStele; StageId = stageId; GroupId = groupId; }
        public override bool Equals(object obj) => obj is TeamSteleTargetData other &&
            StageId == other.StageId && GroupId == other.GroupId;
        public override int GetHashCode() => (TargetCatalog, StageId, GroupId).GetHashCode();
    }
}
