// Selected original 884 implementation; configuration and logging boundaries only.
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeLevelUpRules
{
[Serializable] public sealed class HomeBattleSettings
{ public int PlayerLevelBattlePropId, PlayerSubRankBattlePropId, PlayerFightRolePropGroupId; }
[Serializable] public sealed class ProfessionBaseInfo
{ public Profession Profession; public int LevelPropId; public Dictionary<PropType,long> FeatureProps; }
[Serializable] public sealed class LevelPropDefaultValue
{ public int ClassId; public List<PropType> PropTypes; public long DefaultValue; }
public sealed class LevelPropDefaultValueParser
{
    private readonly Dictionary<int,LevelPropDefaultValue> rows;
    public LevelPropDefaultValueParser(IEnumerable<LevelPropDefaultValue> rows) { this.rows = rows.ToDictionary(row=>row.ClassId); }
    public bool TryGet(int id, out LevelPropDefaultValue row) => rows.TryGetValue(id, out row);
}
public sealed class ProfessionBaseInfoParser
{
    private readonly Dictionary<Profession,ProfessionBaseInfo> rows;
    public ProfessionBaseInfoParser(IEnumerable<ProfessionBaseInfo> rows) { this.rows = rows.ToDictionary(row=>row.Profession); }
    public ProfessionBaseInfo Get(Profession id) => rows.TryGetValue(id,out var row) ? row : null;
}
public sealed class RolePropGroupInfoParser
{
    private readonly Dictionary<(int,SubRank),RolePropGroupInfo> rows;
    public RolePropGroupInfoParser(IEnumerable<RolePropGroupInfo> rows) { this.rows = rows.ToDictionary(row=>(row.ID,row.SubRank)); }
    public RolePropGroupInfo Get(int id, SubRank rank) => rows.TryGetValue((id,rank),out var row) ? row : null;
}
public sealed class HomeCharacterBattleTables : IDisposable
{
    public HomeBattleSettings Settings { get; }
    public LevelPropParser LevelPropParser { get; }
    public ProfessionBaseInfoParser ProfessionBaseInfoParser { get; }
    public RolePropGroupInfoParser RolePropGroupInfoParser { get; }
    public HomeCharacterBattleFormula Formula { get; }
    public HomeCharacterBattleTables(HomeBattleSettings settings, byte[] levelBytes,
        IEnumerable<ProfessionBaseInfo> professions, IEnumerable<RolePropGroupInfo> roles, IEnumerable<LevelPropDefaultValue> defaults)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        LevelPropParser = new LevelPropParser(levelBytes, defaults);
        ProfessionBaseInfoParser = new ProfessionBaseInfoParser(professions);
        var roleRows = roles.ToArray();
        RolePropGroupInfoParser = new RolePropGroupInfoParser(roleRows);
        Formula = new HomeCharacterBattleFormula(this);
        foreach (var row in roleRows) row.Tables = this;
    }
    public void Dispose() => LevelPropParser.Dispose();
}
public static class HomeLevelUpDiagnostics
{ public static Action<string> Error = message => System.Diagnostics.Trace.TraceError(message); }

}
