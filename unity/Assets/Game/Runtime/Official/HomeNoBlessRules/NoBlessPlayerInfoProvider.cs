// Selected original 884 no-blessing implementation; typed data/configuration boundaries only.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Zjcs.Official.HomeExperienceRules;
namespace Zjcs.Official.HomeNoBlessRules
{
public sealed class NoBlessPlayerInfoProvider<TPet> where TPet : INoBlessPet<TPet>
{
	public Dictionary<long, TPet> mTempPets;

	private Dictionary<int, long> mEquipResonanceProps = new Dictionary<int, long>();

	private readonly ReadOnlyDictionary<int, long> mEquipResonanceReadOnly;

	private readonly Dictionary<int, int> mNoBlessEquipUpgradeLevel = new Dictionary<int, int>();

	private readonly Dictionary<int, Dictionary<int, int>> mNoBlessSkillSlotLevelData = new Dictionary<int, Dictionary<int, int>>();

	private readonly Dictionary<int, Dictionary<int, int>> mNoBlessSkillSlotInnerPool = new Dictionary<int, Dictionary<int, int>>();


    public INoBlessPlayerInput<TPet> PlayerInfoProvider { get; }
    private INoBlessConfiguration Rules { get; }
    public NoBlessPlayerInfoProvider(INoBlessPlayerInput<TPet> player, INoBlessConfiguration rules)
    {
        PlayerInfoProvider=player ?? throw new ArgumentNullException(nameof(player));
        Rules=rules ?? throw new ArgumentNullException(nameof(rules));
        mEquipResonanceReadOnly=new ReadOnlyDictionary<int,long>(mEquipResonanceProps);
    }
	public Rank MyPlanesRank => PlayerInfoProvider.MyPlanesRank + 1;
	public int MySeasonId => Rules.GetSeasonByRank(PlayerInfoProvider.MyPlanesRank);
	public SubRank MySubRank => PlayerInfoProvider.MySubRank;
	public int MyLevel
	{
		get
		{
			int baseLevel = Rules.LevelLimits.GetPlayerBaseLevelLimit(MySubRank);
			return Math.Min(PlayerInfoProvider.MyLevel, baseLevel);
		}
	}

	public Dictionary<int, int> MyEquipUpgradeLevel
	{
		get
		{
			int maxEquipBaseLevel = Rules.LevelLimits.GetEquipBaseLevelLimit(MySubRank);
			Dictionary<int, int> myEquipUpgradeLevel = PlayerInfoProvider.MyEquipUpgradeLevel;
			mNoBlessEquipUpgradeLevel.Clear();
			foreach (KeyValuePair<int, int> item in myEquipUpgradeLevel)
			{
				mNoBlessEquipUpgradeLevel[item.Key] = Math.Min(item.Value, maxEquipBaseLevel);
			}
			return mNoBlessEquipUpgradeLevel;
		}
	}

	public ReadOnlyDictionary<int, long> MyEquipResonance
	{
		get
		{
			CalcEquipResonanceProps(MyActivedEquipResonanceLevel);
			return mEquipResonanceReadOnly;
		}
	}

	public Dictionary<long, TPet> MyPetDatas
	{
		get
		{
			if (mTempPets == null)
			{
				mTempPets = new Dictionary<long, TPet>();
			}
			mTempPets.Clear();
			Dictionary<long, TPet> myPetDatas = PlayerInfoProvider.MyPetDatas;
			SystemLevelLimitInfoParser systemLevelLimitInfoParser = Rules.LevelLimits;
			SubRank mySubRank = MySubRank;
			int num = systemLevelLimitInfoParser.GetPetLevelLimit(mySubRank, MyLevel);
			if (num <= 0)
			{
				num = systemLevelLimitInfoParser.GetPetBaseLevelLimit(mySubRank);
			}
			foreach (KeyValuePair<long, TPet> item in myPetDatas)
			{
				TPet playerPetDataWrap = item.Value.CloneWrapForAnother();
				playerPetDataWrap.Level = Math.Min(playerPetDataWrap.Level, num);
				mTempPets.Add(item.Key, playerPetDataWrap);
			}
			return mTempPets;
		}
	}

	public Dictionary<int, Dictionary<int, int>> MySkillSlotLevelData
	{
		get
		{
			int maxSkillSlotBaseLevel = Rules.LevelLimits.GetSkillBaseLevelLimit(MySubRank);
			Dictionary<int, Dictionary<int, int>> mySkillSlotLevelData = PlayerInfoProvider.MySkillSlotLevelData;
			mNoBlessSkillSlotLevelData.Clear();
			foreach (KeyValuePair<int, Dictionary<int, int>> item in mySkillSlotLevelData)
			{
				if (!mNoBlessSkillSlotInnerPool.TryGetValue(item.Key, out var value))
				{
					value = new Dictionary<int, int>();
					mNoBlessSkillSlotInnerPool[item.Key] = value;
				}
				value.Clear();
				foreach (KeyValuePair<int, int> item2 in item.Value)
				{
					value[item2.Key] = Math.Min(item2.Value, maxSkillSlotBaseLevel);
				}
				mNoBlessSkillSlotLevelData[item.Key] = value;
			}
			return mNoBlessSkillSlotLevelData;
		}
	}

	public uint MyActivedEquipResonanceLevel
	{
		get
		{
			Dictionary<int, int> myEquipUpgradeLevel = MyEquipUpgradeLevel;
			uint myActivedEquipResonanceLevel = PlayerInfoProvider.MyActivedEquipResonanceLevel;
			EquipResonanceInfoParser equipResonanceInfoParser = Rules.Resonances;
			for (uint num = myActivedEquipResonanceLevel; num != 0; num--)
			{
				EquipResonanceInfo equipResonanceInfo = equipResonanceInfoParser.Get(num);
				int equipLevel = equipResonanceInfo.EquipLevel;
				int equipCount = equipResonanceInfo.EquipCount;
				int num2 = 0;
				foreach (int value in myEquipUpgradeLevel.Values)
				{
					if (value >= equipLevel)
					{
						num2++;
					}
				}
				if (num2 >= equipCount)
				{
					return num;
				}
			}
			return 0u;
		}
	}

	private void CalcEquipResonanceProps(uint activedEquipResonanceLevel)
	{
		mEquipResonanceProps.Clear();
		Rules.Resonances.CalcEquipResonanceProps(activedEquipResonanceLevel, mEquipResonanceProps);
	}

}

}
