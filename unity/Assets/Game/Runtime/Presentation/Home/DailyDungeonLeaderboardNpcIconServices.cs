using System;
using Zjcs.Offline.PartnersNpc;
using Zjcs.Offline.World;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Presentation.Home
{
    internal sealed class DailyDungeonLeaderboardNpcIconServices : ITeamNpcIconServices
    {
        private readonly OfflinePartnerNpcRuntime runtime;
        private readonly long ownerPlayerId;
        private readonly DailyDungeonTeamMemberSave member;
        private OfflinePartnerNpcLiteInfo cachedInfo;

        public int PlanesRank { get; }
        public IProfessionPlayerInfoBindings PlayerInfo { get; }

        public DailyDungeonLeaderboardNpcIconServices(OfflinePartnerNpcRuntime runtime,
            IProfessionPlayerInfoBindings playerInfo, long ownerPlayerId, int planesRank,
            DailyDungeonTeamMemberSave member)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            PlayerInfo = playerInfo ?? throw new ArgumentNullException(nameof(playerInfo));
            if (ownerPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerPlayerId));
            this.ownerPlayerId = ownerPlayerId;
            PlanesRank = planesRank;
            this.member = member ?? throw new ArgumentNullException(nameof(member));
        }

        public TeamNpcIconDefinition GetNpc(int classId)
        {
            OfflinePartnerNpcLiteInfo info = Resolve(classId, member.level, member.subRank, PlanesRank);
            return new TeamNpcIconDefinition { NameKey = info.NameKey, Icon = info.Icon };
        }

        public int GetProfession(int classId, int level, int planesRank)
        {
            return Resolve(classId, level, member.subRank, planesRank).Profession;
        }

        public long CalcCombatRating(int classId, int level, int subRank)
        {
            return Resolve(classId, level, subRank, PlanesRank).CombatRating;
        }

        private OfflinePartnerNpcLiteInfo Resolve(int classId, int level, int subRank, int planesRank)
        {
            if (classId != member.npcClassId || level != member.level
                || subRank != member.subRank || planesRank != PlanesRank)
                throw new InvalidOperationException("Leaderboard NPC icon data differs from its saved daily dungeon team.");
            return cachedInfo ?? (cachedInfo = runtime.CreateLiteInfo(classId, classId,
                ownerPlayerId, level, subRank, planesRank));
        }
    }
}
