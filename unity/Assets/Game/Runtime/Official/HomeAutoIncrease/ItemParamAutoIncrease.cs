// Selected original item auto-increase rules; clock/data boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeAutoIncrease
{
[Serializable]
public sealed class ItemParamAutoIncrease
{
    public double Decimal;
    public DateTime LastUpdateTime;
    public double IncreaseSpeed;
    public double BonusSeconds;
    public long MaxCount;
}

}
