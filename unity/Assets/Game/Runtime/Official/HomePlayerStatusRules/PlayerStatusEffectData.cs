// Selected original 884 player-status implementation; storage/configuration boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomePlayerStatusRules
{
[Serializable]
public sealed class PlayerStatusEffectData
{
    public long PlayerId;
    public int ClassId;
    public EStatusSourceType StatusSourceType;
    public int ActivedTimes;
    public bool IsActive = true;
    // JsonUtility does not serialize DateTime. Preserve its ticks exactly.
    public long LastGainedTimeTicks;
    public DateTimeKind LastGainedTimeKind;
    public DateTime LastGainedTime
    {
        get => new DateTime(LastGainedTimeTicks, LastGainedTimeKind);
        set { LastGainedTimeTicks = value.Ticks; LastGainedTimeKind = value.Kind; }
    }
    public bool LastIsReduceOncePerPlay;
    public List<long> OccupiedFightIds = new List<long>();
}
}
