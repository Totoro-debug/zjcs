// Selected Console.dll; explicit data, clock, config and disabled-function ports.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeOutputAddition
{
public sealed class OutputAdditionInfo
{
    public int ClassId, OutputAdditionPropVal;
    public OutputActionType OutputActionType;
    public OutputGroupType OutputGroupType;
    public OutputAdditionPropType OutputAdditionPropType;
    public string ShowGroupKey, ShowKey;
}
public sealed class PlayerOutputAdditionPropTypeDataWrap
{
    public int ClassId;
    public DateTime StartTime, EndTime;
}
public sealed class PlayerOutputAdditionDataWrap
{
    public long PlayerId, Id;
    public OutputActionType OutputActionType;
    public DateTime StartTime, EndTime;
    public Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> OutputAdditionPropDict
        = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap>();
}
public sealed class OutputAdditionInfoParser
{
    private readonly IReadOnlyDictionary<int, OutputAdditionInfo> rows;
    public OutputAdditionInfoParser(IReadOnlyDictionary<int, OutputAdditionInfo> rows)
    { this.rows = rows ?? throw new ArgumentNullException(nameof(rows)); }
    public OutputAdditionInfo Get(int id) => rows.TryGetValue(id, out var value) ? value : null;
}
public sealed class HomeOutputAdditionSettings
{
    public Dictionary<int, OutputAdditionPropType> HomeHangUpAddPercentTypeDict;
    public Dictionary<int, OutputAdditionPropType> HomeHangUpMaxStoreTypeDict;
}
public sealed class HomeOutputAdditionPlayer
{
    public readonly Dictionary<long, PlayerOutputAdditionDataWrap> mAllOutputAdditionDict;
    public readonly OutputAdditionInfoParser Configuration;
    public readonly HomeOutputAdditionSettings Settings;
    public readonly Func<DateTime> GetDateTime;
    public readonly Func<int?, bool> CheckFunctionDisabled;
    public HomeOutputAdditionPlayer(Dictionary<long, PlayerOutputAdditionDataWrap> records,
        OutputAdditionInfoParser configuration, HomeOutputAdditionSettings settings, Func<DateTime> now, Func<int?, bool> disabled)
    {
        mAllOutputAdditionDict = records ?? throw new ArgumentNullException(nameof(records));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        GetDateTime = now ?? throw new ArgumentNullException(nameof(now));
        CheckFunctionDisabled = disabled ?? throw new ArgumentNullException(nameof(disabled));
    }
}
}
