using System;
using System.Collections.Generic;

namespace Zjcs.Official.Partners
{
    public sealed class PartnerConversationDayLimitRecord
    {
        public long NpcId;
        public int DayShowCount;
        public DateTime LastShowTime;
    }

    // Direct Player day-limit methods and TimeUtils reset arithmetic. The
    // caller supplies the original configuration, UTC clock and server zone.
    public sealed class PartnerConversationDayLimitRules
    {
        private readonly IPartnerConversationConfiguration configuration;
        private readonly IDictionary<long, PartnerConversationDayLimitRecord> records;
        private readonly Func<DateTime> now;
        private readonly int limit;
        private readonly DateTime resetTime;
        private readonly int timeZone;
        private static readonly DateTime sUnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public PartnerConversationDayLimitRules(IPartnerConversationConfiguration configuration,
            IDictionary<long, PartnerConversationDayLimitRecord> records, Func<DateTime> now,
            int limit, DateTime resetTime, int timeZone)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.records = records ?? throw new ArgumentNullException(nameof(records));
            this.now = now ?? throw new ArgumentNullException(nameof(now));
            this.limit = limit; this.resetTime = resetTime; this.timeZone = timeZone;
        }

/*PLAYER_METHODS*/

/*TIME_METHODS*/
    }
}
