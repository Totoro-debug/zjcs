// Selected original 884 experience implementation; table and player service boundaries only.
using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeExperienceRules
{
public sealed class AstrologicalSeasonConfigParser
{
    private readonly AstrologicalSeasonConfig[] rows;
    private Dictionary<Rank, AstrologicalSeasonConfig> mAstrologicalConfigs = new Dictionary<Rank, AstrologicalSeasonConfig>();
    public int MinSeason { get; private set; }
    public int MaxSeason { get; private set; }
    public AstrologicalSeasonConfigParser(IEnumerable<AstrologicalSeasonConfig> source) { rows = new List<AstrologicalSeasonConfig>(source).ToArray(); PostParse(); }
    private AstrologicalSeasonConfig[] GetAll() => rows;
	private void PostParse()
	{
		AstrologicalSeasonConfig[] all = GetAll();
		MinSeason = int.MaxValue;
		MaxSeason = -1;
		List<AstrologicalSeasonConfig> list = new List<AstrologicalSeasonConfig>();
		AstrologicalSeasonConfig[] array = all;
		foreach (AstrologicalSeasonConfig astrologicalSeasonConfig in array)
		{
			MinSeason = Math.Min(MinSeason, astrologicalSeasonConfig.Season);
			MaxSeason = Math.Max(MaxSeason, astrologicalSeasonConfig.Season);
			mAstrologicalConfigs.Add(astrologicalSeasonConfig.Rank, astrologicalSeasonConfig);
			astrologicalSeasonConfig.SourceStarRateMatrialValue = (float)astrologicalSeasonConfig.SourceStarRateMatrial / 1000f;
			list.Add(astrologicalSeasonConfig);
		}
		list.Sort((AstrologicalSeasonConfig x, AstrologicalSeasonConfig y) => x.Season.CompareTo(y.Season));
		for (int num = 1; num < list.Count; num++)
		{
			list[num - 1].NextSeason = list[num].Season;
		}
	}
	public int GetSeasonByRank(Rank rank)
	{
		if (mAstrologicalConfigs.TryGetValue(rank, out var value))
		{
			return value.Season;
		}
		return -1;
	}
}

}
