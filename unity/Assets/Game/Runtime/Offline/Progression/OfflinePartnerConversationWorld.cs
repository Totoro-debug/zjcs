using System;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflinePartnerConversationWorld : IOfflinePartnerConversationWorld
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialPartnerConversationWorldData configuration;
        public OfflinePartnerConversationWorld(OfflineMainlineProgressSession session,
            OfficialPartnerConversationWorldData configuration)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        public int CalculateWorldMapProgress(int mapClassId, int region)
        {
            if (region < 0) throw new NotSupportedException("The selected conversation closure contains regional progress only.");
            if (configuration.GetMaximum(mapClassId, region) == 0) return 0;
            // RefreshRegionProgress already saves CalculateRegionProgress's
            // original 0..10000 result, rather than the raw point count.
            return session.Save.World.Maps.FirstOrDefault(map => map.MapClassId == mapClassId)
                ?.Regions.FirstOrDefault(value => value.Region == region)?.Progress ?? 0;
        }
        public bool IsRegionGoddessActive(int mapClassId, int region)
        {
            int entity = configuration.GetGoddessEntityId(mapClassId, region);
            if (entity == 0) return false;
            // Extraction verifies that this queried region has exactly one
            // original goddess, so its saved activation identifies that entity.
            return session.Save.World.ActivatedGoddessRegions
                .Any(value => value.MapClassId == mapClassId && value.Region == region);
        }
    }
}
