// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
[Serializable]
public sealed class LevelExp
{
    public int Id;
    public List<long> Exp;
}

}
