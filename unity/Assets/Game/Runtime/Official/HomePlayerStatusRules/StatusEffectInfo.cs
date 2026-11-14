// Selected original 884 player-status implementation; storage/configuration boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomePlayerStatusRules
{
[Serializable]
public sealed class StatusEffectInfo
{
    public int ClassId;
    public int MaxActivateTimes;
    public int EntryId;
    public int Duration;
    public StatusEffectIconType IconType;
    public bool IsReduceOncePerPlay;
    public List<int> Scopes;
}

}
