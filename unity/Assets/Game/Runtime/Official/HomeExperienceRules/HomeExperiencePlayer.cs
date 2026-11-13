// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
// A read projection: it never writes the local profile or invents season records.
public sealed class HomeExperiencePlayer
{
    public HomeExperienceTables Tables { get; }
    public SubRank SubRank;
    public Rank Rank => SubRank.ToRank();
    public int BaseLevel;
    public int Level;
    public bool Inited;
    public double PlayerLevelLimitProperty;
    public int LevelLimit => (int)((double)Tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(SubRank) + PlayerLevelLimitProperty);
    public int MaxLevelLimit => Tables.LevelExpParser.GetLevelMax(1);
    public int ServerRecordSeason;
    public Rank PlanesRank;
    public HomeExperienceSeasonRecord CurrentRecordData;
    public bool PlayerExpLimitFunctionLimited;
    public long Experience;
    public readonly Dictionary<int, HomeExperienceBlessRecord> BlessRecords = new Dictionary<int, HomeExperienceBlessRecord>();
    public event Action<int> OnLevelUpdate;
    public HomeExperiencePlayer(HomeExperienceTables tables) { Tables = tables ?? throw new ArgumentNullException(nameof(tables)); }
    public long GetItemAmount(int itemId) => itemId == Tables.PlayerExpItemId ? Experience : 0;
    public bool TryGetSeasonBlessData(int season, out HomeExperienceBlessRecord record) => BlessRecords.TryGetValue(season, out record);
    public void SetLevel(int value) { BaseLevel = value; }
    public void FireOnLevelUpdate(int level) { OnLevelUpdate?.Invoke(level); }
	public bool TryRefreshLevel()
	{
		long itemAmount = this.GetItemAmount(Tables.PlayerExpItemId);
		int val = Tables.LevelExpParser.CalcLevelByExp(1, itemAmount);
		int playerBaseLevelLimit = Tables.SystemLevelLimitInfoParser.GetPlayerBaseLevelLimit(SubRank - 1);
		val = Math.Max(val, playerBaseLevelLimit);
		val = Math.Min(val, LevelLimit);
		bool flag = val != BaseLevel;
		bool flag2 = this.GetCurBlessPlayerLevel() + val != Level;
		if (flag | flag2)
		{
			_ = Level;
			if (flag)
			{
				SetLevel(val);
			}
			this.UpdateShowLevel();
			FireOnLevelUpdate(Level);
			_ = Inited;
			return true;
		}
		return false;
	}
 }
[Serializable] public sealed class HomeExperienceSeasonRecord { public int Season; public Rank RecordRank; }
[Serializable] public sealed class HomeExperienceBlessRecord { public long PlayerBlessExp; }
public sealed class HomeExperienceTables
{
    public LevelExpParser LevelExpParser { get; }
    public LevelBlessInfoParser LevelBlessInfoParser { get; }
    public SystemLevelLimitInfoParser SystemLevelLimitInfoParser { get; }
    public AstrologicalSeasonConfigParser AstrologicalSeasonConfigParser { get; }
    public int PlayerExpItemId { get; }
    public Rank FirstBlessRank { get; }
    public HomeExperienceTables(IEnumerable<LevelExp> experience, IEnumerable<LevelBlessInfo> blessing,
        IEnumerable<SystemLevelLimitInfo> limits, IEnumerable<AstrologicalSeasonConfig> seasons, int playerExpItemId, Rank firstBlessRank)
    {
        LevelExpParser = new LevelExpParser(experience); LevelBlessInfoParser = new LevelBlessInfoParser(blessing);
        SystemLevelLimitInfoParser = new SystemLevelLimitInfoParser(limits);
        AstrologicalSeasonConfigParser = new AstrologicalSeasonConfigParser(seasons);
        PlayerExpItemId = playerExpItemId; FirstBlessRank = firstBlessRank;
    }
}

}
