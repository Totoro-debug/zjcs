// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
[Serializable]
public sealed class LevelBlessInfo
{
    public Rank Rank;
    public int BlessLevel;
    public long Exp;
    public long SeasonScore;
}

}
