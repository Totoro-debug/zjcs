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
public sealed class LevelPropParser : IDisposable
{
    private LevelPropBinaryReader mBinaryReader;
    private readonly LRUCollection<(int,int),IPropDictionary> mCache = new LRUCollection<(int,int),IPropDictionary>(1000);
    private readonly LevelPropDefaultValueParser mDefaultValueParser;
    public LevelPropParser(byte[] bytes, IEnumerable<LevelPropDefaultValue> defaults)
    { mBinaryReader = new LevelPropBinaryReader(bytes); mDefaultValueParser = new LevelPropDefaultValueParser(defaults); }
	public IReadOnlyDictionary<PropType, long> GetProps(int classId, int level)
	{
		return GetConfigProps(classId, level);
	}

	private IReadOnlyDictionary<PropType, long> GetConfigProps(int classId, int level)
	{
		if (!TryGetPropDict(classId, level, out var propDict))
		{
			if (!TryGetLevelRange(classId, out var levelRange))
			{

				HomeLevelUpDiagnostics.Error("LevelPropParser original input error");
				return null;
			}
			if (level < levelRange.Item2)
			{

				HomeLevelUpDiagnostics.Error("LevelPropParser original input error");
				return null;
			}
			TryGetPropDict(classId, levelRange.Item2, out propDict);
		}
		return propDict;
	}

	public bool TryGetLevelRange(int classId, out (int, int) levelRange)
	{
		if (mBinaryReader.TryGetLevelRange(classId, out var minLevel, out var maxLevel))
		{
			levelRange = (minLevel, maxLevel);
			return true;
		}
		levelRange = default;
		return false;
	}

	private bool TryGetPropDict(int classId, int level, out IPropDictionary propDict)
	{
		propDict = null;
		(int, int) key = (classId, level);
		if (mCache.TryGetValue(key, out propDict))
		{
			return true;
		}
		if (mBinaryReader.TryGetProps(classId, level, out propDict))
		{
			if (mDefaultValueParser.TryGet(classId, out var config))
			{
				foreach (PropType propType in config.PropTypes)
				{
					if (!propDict.ContainsKey(propType))
					{
						propDict.Add(propType, config.DefaultValue);
					}
				}
			}
			propDict.TrimExcess();
			mCache.Add(key, propDict);
			return true;
		}
		return false;
	}

	public void Dispose()
	{
		mBinaryReader?.Dispose();
		mBinaryReader = null;
	}
}

}
