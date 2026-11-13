// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
[Serializable]
public sealed class AstrologicalSeasonConfig
{
    public int Season;
    public Rank Rank;
    public int NextSeason;
    public int SourceStarRateMatrial;
    public float SourceStarRateMatrialValue;
}

}
