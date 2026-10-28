namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
public sealed class ProfessionAppearanceResolver
{
    private readonly ProfessionAppearanceTables mTables;
    private readonly HashSet<int> sEffectiveSkinIds = new HashSet<int>();
    private readonly HashSet<SkinType> sMutexLostTypes = new HashSet<SkinType>();
    public ProfessionAppearanceResolver(ProfessionAppearanceTables tables)
    { mTables = tables ?? throw new ArgumentNullException(nameof(tables)); }
	private enum AppendSkinState
	{
		Undecided,
		Alive,
		Blocked,
		MutexLost,
		CycleLost
	}

	private struct AppendSkinEntry : IComparable<AppendSkinEntry>
	{
		public SpineSkinInfo Info;

		public SkinType WearSkinType;

		public int ItemId;

		public DateTime WearTime;

		public AppendSkinState State;

		public int CompareTo(AppendSkinEntry other)
		{
			int num = WearTime.CompareTo(other.WearTime);
			if (num == 0)
			{
				return ItemId.CompareTo(other.ItemId);
			}
			return num;
		}
	}

	public string GetSkinName(int skinId)
	{
		SpineSkinInfo spineSkinInfo = mTables.SpineSkinInfoParser.Get(skinId);
		if (spineSkinInfo == null)
		{
			Debug.LogError("传入皮肤Id为空:" + (skinId));
			return string.Empty;
		}
		return spineSkinInfo.SkinName;
	}
	public AppearanceRuntimeData BuildAppearanceRuntimeData(Sex sex, Profession profession, AppearanceData data, List<string> appendSkins)
	{
		AppearanceRuntimeData appearanceRuntimeData = new AppearanceRuntimeData();
		BuildAppearanceRuntimeData(sex, profession, data, appendSkins, appearanceRuntimeData);
		return appearanceRuntimeData;
	}

	public void BuildAppearanceRuntimeData(Sex sex, Profession profession, AppearanceData data, List<string> appendSkins, AppearanceRuntimeData runtimeData)
	{
		if (runtimeData == null)
		{
			return;
		}
		runtimeData.Sex = sex;
		runtimeData.AppendSkins = appendSkins;
		AppearanceRuntimeData appearanceRuntimeData = runtimeData;
		if (appearanceRuntimeData.SkinMap == null)
		{
			Dictionary<SkinType, string> dictionary = (appearanceRuntimeData.SkinMap = new Dictionary<SkinType, string>());
		}
		appearanceRuntimeData = runtimeData;
		if (appearanceRuntimeData.DyeMap == null)
		{
			Dictionary<SkinType, Dictionary<int, HsvChange>> dictionary3 = (appearanceRuntimeData.DyeMap = new Dictionary<SkinType, Dictionary<int, HsvChange>>());
		}
		Dictionary<SkinType, string> skinNameMap = runtimeData.SkinMap;
		skinNameMap.Clear();
		Dictionary<SkinType, int> skinIdDict = CollectionPool<Dictionary<SkinType, int>, KeyValuePair<SkinType, int>>.Get();
		ResolveAppendAppearanceMutex(data, sex, sEffectiveSkinIds, sMutexLostTypes);
		foreach (KeyValuePair<SkinType, SkinData> item in data.SkinMap)
		{
			if (TryGetFinalSkinId(data, item.Key, sex, profession, sEffectiveSkinIds, sMutexLostTypes, out var skinId))
			{
				CacheSkinDict(item.Key, skinId, ref skinNameMap, ref skinIdDict);
			}
		}
		BuildDeyMap(data.DyeMap, skinIdDict, runtimeData.DyeMap);
		skinIdDict.Clear();
		CollectionPool<Dictionary<SkinType, int>, KeyValuePair<SkinType, int>>.Release(skinIdDict);
	}

	private void BuildDeyMap(Dictionary<DyeType, SkinDyeData> dyeDataMap, Dictionary<SkinType, int> skinDict, Dictionary<SkinType, Dictionary<int, HsvChange>> dyeMap)
	{
		foreach (KeyValuePair<SkinType, Dictionary<int, HsvChange>> item in dyeMap)
		{
			Dictionary<int, HsvChange> value = item.Value;
			value.Clear();
			CollectionPool<Dictionary<int, HsvChange>, KeyValuePair<int, HsvChange>>.Release(value);
		}
		dyeMap.Clear();
		SpineSkinInfoParser spineSkinInfoParser = mTables.SpineSkinInfoParser;
		DyeType dyeType = default;
		SkinDyeData skinDyeData = default;
		SkinType skinType = default;
		int num = default;
		foreach (KeyValuePair<DyeType, SkinDyeData> item2 in dyeDataMap)
		{
			dyeType = item2.Key; skinDyeData = item2.Value;
			DyeType dyeType2 = dyeType;
			SkinDyeData skinDyeData2 = skinDyeData;
			CharacterSkinDyeInfo characterSkinDyeInfo = mTables.CharacterSkinDyeInfoParser.Get(dyeType2);
			if (characterSkinDyeInfo == null)
			{
				continue;
			}
			Hsv hsv = skinDyeData2.Hsv;
			HsvChange dyeHsvChange = GetDyeHsvChange(dyeType2, hsv.H, hsv.S, hsv.V);
			foreach (DyeSkinTypeInfo dyeSkinTypeInfo in characterSkinDyeInfo.DyeSkinTypeInfoList)
			{
				foreach (KeyValuePair<SkinType, int> item3 in skinDict)
				{
					skinType = item3.Key; num = item3.Value;
					SkinType skinType2 = skinType;
					int key = num;
					if (skinType2 == dyeSkinTypeInfo.SkinType && spineSkinInfoParser.Get(key).HasDye)
					{
						if (!dyeMap.TryGetValue(dyeSkinTypeInfo.SkinType, out var value2))
						{
							value2 = CollectionPool<Dictionary<int, HsvChange>, KeyValuePair<int, HsvChange>>.Get();
							dyeMap.Add(dyeSkinTypeInfo.SkinType, value2);
						}
						value2[dyeSkinTypeInfo.Channel] = ((dyeSkinTypeInfo.VOffset == 0) ? dyeHsvChange : GetDyeHsvChange(dyeType2, hsv.H, hsv.S, hsv.V + dyeSkinTypeInfo.VOffset));
						break;
					}
				}
			}
		}
	}

	private void CacheSkinDict(SkinType skinType, int skinId, ref Dictionary<SkinType, string> skinNameMap, ref Dictionary<SkinType, int> skinIdDict)
	{
		SpineSkinInfoParser spineSkinInfoParser = mTables.SpineSkinInfoParser;
		string skinName = GetSkinName(skinId);
		if (!string.IsNullOrEmpty(skinName))
		{
			skinNameMap.TryAdd(skinType, skinName);
			skinIdDict.TryAdd(skinType, skinId);
		}
		foreach (int bindingId in spineSkinInfoParser.Get(skinId).BindingIdList)
		{
			SpineSkinInfo spineSkinInfo = spineSkinInfoParser.Get(bindingId);
			skinName = GetSkinName(bindingId);
			if (!skinNameMap.TryAdd(spineSkinInfo.SkinType, skinName))
			{
				skinNameMap[spineSkinInfo.SkinType] = skinName;
			}
			if (!skinIdDict.TryAdd(spineSkinInfo.SkinType, bindingId))
			{
				skinIdDict[spineSkinInfo.SkinType] = bindingId;
			}
		}
	}

	public HsvChange GetDyeHsvChange(DyeType dyeType, int h, int s, int v)
	{
		return GetDyeHsvChange(mTables.CharacterSkinDyeInfoParser.Get(dyeType), h, s, v);
	}

	public HsvChange GetDyeHsvChange(CharacterSkinDyeInfo dyeInfo, int h, int s, int v)
	{

		Color val = HsvToLinearColor(dyeInfo.BaseH, dyeInfo.BaseS, dyeInfo.BaseV);
		Color val2 = HsvToLinearColor(h, s, v);
		float num = default;
		float num2 = default;
		float num3 = default;
		Color.RGBToHSV(val, out num, out num2, out num3);
		float num4 = default;
		float num5 = default;
		float num6 = default;
		Color.RGBToHSV(val2, out num4, out num5, out num6);
		return new HsvChange
		{
			OffsetH = num4 - num,
			OffsetScaleS = (num5 - num2) / num2,
			OffsetScaleV = (num6 - num3) / num3
		};
	}

	public Color HsvToLinearColor(int h, int s, int v)
	{

		Color val = Color.HSVToRGB(NormalizedH(h), NormalizedSOrV(s), NormalizedSOrV(v));
		return val.linear;
	}

	public Color HsvToColor(int h, int s, int v)
	{

		return Color.HSVToRGB(NormalizedH(h), NormalizedSOrV(s), NormalizedSOrV(v));
	}

	private float NormalizedH(int h)
	{
		return Mathf.Clamp((float)h / 360f, 0f, 1f);
	}

	private float NormalizedSOrV(int value)
	{
		return Mathf.Clamp((float)value / 100f, 0f, 1f);
	}

	public bool TryGetFinalSkinId(AppearanceData data, SkinType skinType, Sex sex, Profession profession, HashSet<int> effectiveSkinIds, HashSet<SkinType> mutexLostTypes, out int skinId)
	{
		if (!TryGetSkinId(data, skinType, sex, profession, mutexLostTypes, out skinId))
		{
			return false;
		}
		if (IsFilterSkinId(effectiveSkinIds, skinId))
		{
			return false;
		}
		return true;
	}

	private bool TryGetSkinId(AppearanceData data, SkinType skinType, Sex sex, Profession profession, HashSet<SkinType> mutexLostTypes, out int skinId)
	{
		skinId = 0;
		if (IsSkinLimit(data, skinType, sex, profession))
		{
			return false;
		}
		if (!mutexLostTypes.Contains(skinType) && TryGetAppendSkinId(data, skinType, sex, profession, out skinId))
		{
			return true;
		}
		if (TryGetEquipSkinId(skinType, sex, profession, data.EquipSkinDict, out skinId))
		{
			return true;
		}
		if (data.SkinMap.TryGetValue(skinType, out var value))
		{
			skinId = value.SkinId;
			return true;
		}
		return false;
	}

	public bool TryGetAppendSkinId(AppearanceData data, SkinType skinType, Sex sex, Profession profession, out int skinId)
	{
		skinId = 0;
		if (!data.AppendAppearanceDict.TryGetValue(skinType, out var value))
		{
			return false;
		}
		if (!mTables.AppendAppearanceInfoParser.TryGetSkinId(value, sex, out skinId))
		{
			return false;
		}
		if (!mTables.CharacterSkinInfoParser.GetSkinInfo(sex, profession).ContainsSkin(skinType, skinId))
		{
			return false;
		}
		return true;
	}

	public bool TryGetEquipSkinId(SkinType skinType, Sex sex, Profession profession, Dictionary<EquipPos, int> equipSkinDict, out int skinId)
	{
		skinId = 0;
		if (skinType != SkinType.pWeapon && skinType != SkinType.sWeapon)
		{
			return false;
		}
		if (!equipSkinDict.TryGetValue(skinType.ToEquipPos(), out skinId))
		{
			return false;
		}
		if (!mTables.CharacterSkinInfoParser.GetSkinInfo(sex, profession).ContainsSkin(skinType, skinId))
		{
			return false;
		}
		return true;
	}

	public bool IsSkinLimit(AppearanceData data, SkinType skinType, Sex sex, Profession profession)
	{
		if (skinType == SkinType.sWeapon)
		{
			if (!data.EquipSkinDict.TryGetValue(skinType.ToEquipPos(), out var value))
			{
				return true;
			}
			if (value == 0)
			{
				return false;
			}
			if (!mTables.CharacterSkinInfoParser.GetSkinInfo(sex, profession).ContainsSkin(skinType, value))
			{
				return true;
			}
		}
		return false;
	}

	public void ResolveAppendAppearanceMutex(AppearanceData data, Sex sex, HashSet<int> effectiveSkinIds, HashSet<SkinType> mutexLostTypes = null, List<SkinType> cycleLostTypes = null)
	{
		ResolveAppendAppearanceMutex(data?.AppendAppearanceDict, data?.AppendAppearanceWearTimeDict, sex, effectiveSkinIds, mutexLostTypes, cycleLostTypes);
	}

	public void ResolveAppendAppearanceMutex(Dictionary<SkinType, int> appendAppearanceDict, Dictionary<SkinType, DateTime> wearTimeDict, Sex sex, HashSet<int> effectiveSkinIds, HashSet<SkinType> mutexLostTypes = null, List<SkinType> cycleLostTypes = null)
	{
		effectiveSkinIds.Clear();
		mutexLostTypes?.Clear();
		cycleLostTypes?.Clear();
		if (appendAppearanceDict == null || appendAppearanceDict.Count == 0)
		{
			return;
		}
		ProfessionAppendAppearanceInfoParser appendAppearanceInfoParser = mTables.AppendAppearanceInfoParser;
		SpineSkinInfoParser spineSkinInfoParser = mTables.SpineSkinInfoParser;
		List<AppendSkinEntry> list = CollectionPool<List<AppendSkinEntry>, AppendSkinEntry>.Get();
		SkinType skinType = default;
		int num = default;
		foreach (KeyValuePair<SkinType, int> item in appendAppearanceDict)
		{
			skinType = item.Key; num = item.Value;
			SkinType skinType2 = skinType;
			int num2 = num;
			if (!appendAppearanceInfoParser.TryGetSkinId(num2, sex, out var skinId))
			{
				continue;
			}
			SpineSkinInfo spineSkinInfo = spineSkinInfoParser.Get(skinId);
			if (spineSkinInfo != null)
			{
				if (wearTimeDict == null || !wearTimeDict.TryGetValue(skinType2, out var value))
				{
					value = DateTime.MinValue;
				}
				list.Add(new AppendSkinEntry
				{
					ItemId = num2,
					Info = spineSkinInfo,
					WearSkinType = skinType2,
					WearTime = value
				});
			}
		}
		list.Sort();
		ResolveEffectiveSkin(list);
		foreach (AppendSkinEntry item2 in list)
		{
			switch (item2.State)
			{
			case AppendSkinState.Alive:
				effectiveSkinIds.Add(item2.Info.Id);
				break;
			case AppendSkinState.MutexLost:
				mutexLostTypes?.Add(item2.WearSkinType);
				break;
			case AppendSkinState.CycleLost:
				mutexLostTypes?.Add(item2.WearSkinType);
				cycleLostTypes?.Add(item2.WearSkinType);
				break;
			}
		}
		CollectionPool<List<AppendSkinEntry>, AppendSkinEntry>.Release(list);
	}

	private void ResolveEffectiveSkin(List<AppendSkinEntry> entries)
	{
		PropagateAppendSkinState(entries);
		if (KillBlockCycle(entries))
		{
			PropagateAppendSkinState(entries);
		}
	}

	private void PropagateAppendSkinState(List<AppendSkinEntry> entries)
	{
		bool flag = true;
		while (flag)
		{
			flag = false;
			for (int i = 0; i < entries.Count; i++)
			{
				AppendSkinEntry value = entries[i];
				if (value.State == AppendSkinState.Undecided)
				{
					AppendSkinState appendSkinState = DecideAppendSkinState(entries, i);
					if (appendSkinState != AppendSkinState.Undecided)
					{
						value.State = appendSkinState;
						entries[i] = value;
						flag = true;
					}
				}
			}
		}
	}

	private AppendSkinState DecideAppendSkinState(List<AppendSkinEntry> entries, int index)
	{
		bool flag = false;
		for (int i = 0; i < entries.Count; i++)
		{
			if (!IsBlocking(entries, i, index))
			{
				continue;
			}
			switch (entries[i].State)
			{
			case AppendSkinState.Alive:
				if (!IsFilterSkinInfo(entries[index].Info, entries[i].Info))
				{
					return AppendSkinState.Blocked;
				}
				return AppendSkinState.MutexLost;
			case AppendSkinState.Undecided:
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return AppendSkinState.Alive;
		}
		return AppendSkinState.Undecided;
	}

	private bool IsBlocking(List<AppendSkinEntry> entries, int blockerIndex, int targetIndex)
	{
		if (blockerIndex == targetIndex)
		{
			return false;
		}
		if (!IsFilterSkinInfo(entries[blockerIndex].Info, entries[targetIndex].Info))
		{
			return false;
		}
		if (blockerIndex >= targetIndex)
		{
			return !IsFilterSkinInfo(entries[targetIndex].Info, entries[blockerIndex].Info);
		}
		return true;
	}

	private bool KillBlockCycle(List<AppendSkinEntry> entries)
	{
		List<int> list = CollectionPool<List<int>, int>.Get();
		for (int i = 0; i < entries.Count; i++)
		{
			if (entries[i].State == AppendSkinState.Undecided && IsInBlockCycle(entries, i))
			{
				list.Add(i);
			}
		}
		foreach (int item in list)
		{
			AppendSkinEntry value = entries[item];
			value.State = AppendSkinState.CycleLost;
			entries[item] = value;
		}
		bool result = list.Count > 0;
		CollectionPool<List<int>, int>.Release(list);
		return result;
	}

	private bool IsInBlockCycle(List<AppendSkinEntry> entries, int index)
	{
		List<int> list = CollectionPool<List<int>, int>.Get();
		list.Add(index);
		bool flag = false;
		for (int i = 0; i < list.Count; i++)
		{
			if (flag)
			{
				break;
			}
			for (int j = 0; j < entries.Count; j++)
			{
				if (entries[j].State == AppendSkinState.Undecided && IsBlocking(entries, list[i], j))
				{
					if (j == index)
					{
						flag = true;
						break;
					}
					if (!list.Contains(j))
					{
						list.Add(j);
					}
				}
			}
		}
		CollectionPool<List<int>, int>.Release(list);
		return flag;
	}

	public bool IsFilterSkinId(HashSet<int> effectiveSkinIds, int skinId)
	{
		SpineSkinInfoParser spineSkinInfoParser = mTables.SpineSkinInfoParser;
		SpineSkinInfo spineSkinInfo = spineSkinInfoParser.Get(skinId);
		if (spineSkinInfo == null)
		{
			return false;
		}
		foreach (int effectiveSkinId in effectiveSkinIds)
		{
			if (effectiveSkinId != skinId && IsFilterSkinInfo(spineSkinInfoParser.Get(effectiveSkinId), spineSkinInfo))
			{
				return true;
			}
		}
		return false;
	}

	public bool CanShowAppendSkin(int skinId, List<SkinType> allowTypes, List<SkinType> mutexTypes)
	{
		SpineSkinInfoParser spineSkinInfoParser = mTables.SpineSkinInfoParser;
		SpineSkinInfo spineSkinInfo = spineSkinInfoParser.Get(skinId);
		if (spineSkinInfo == null)
		{
			return false;
		}
		foreach (int bindingId in spineSkinInfo.BindingIdList)
		{
			SpineSkinInfo spineSkinInfo2 = spineSkinInfoParser.Get(bindingId);
			if (spineSkinInfo2 == null)
			{
				return false;
			}
			if (!allowTypes.Contains(spineSkinInfo2.SkinType))
			{
				return false;
			}
			if (mutexTypes.Contains(spineSkinInfo2.SkinType))
			{
				return false;
			}
		}
		return true;
	}

	public bool IsFilterSkinInfo(SpineSkinInfo currentInfo, SpineSkinInfo targetInfo)
	{
		if (!currentInfo.BlackIdSet.Contains(targetInfo.Id))
		{
			return false;
		}
		if (currentInfo.WhiteIdSet.Contains(targetInfo.Id))
		{
			return false;
		}
		return true;
	}

	public bool IsAppearanceDataValid(AppearanceData data, AppearanceConfig appearanceConfig)
	{
		foreach (PlayerSkinType playerSkinType in appearanceConfig.PlayerSkinTypes)
		{
			if (playerSkinType.BaseSkin && !data.SkinMap.ContainsKey(playerSkinType.SkinType))
			{
				return false;
			}
		}
		return true;
	}

	public AppearanceData BuildDefaultAppearanceData(AppearanceConfig appearanceConfig, Sex gender = Sex.Male, Profession profession = Profession.None)
	{
		AppearanceData appearanceData = new AppearanceData();
		appearanceData.SkinMap = new Dictionary<SkinType, SkinData>();
		appearanceData.DyeMap = new Dictionary<DyeType, SkinDyeData>();
		CharacterSkinInfo skinInfo = mTables.CharacterSkinInfoParser.GetSkinInfo(gender, profession);
		var characterSkinDyeInfoParser = mTables.CharacterSkinDyeInfoParser;
		var characterSkinColorInfoParser = mTables.CharacterSkinColorInfoParser;
		bool flag = profession != Profession.None;
		SkinType skinType = default;
		Dictionary<SkinUseType, List<int>> dictionary = default;
		foreach (KeyValuePair<SkinType, Dictionary<SkinUseType, List<int>>> item in skinInfo.TypeDic)
		{
			skinType = item.Key; dictionary = item.Value;
			SkinType skinType2 = skinType;
			Dictionary<SkinUseType, List<int>> dictionary2 = dictionary;
			if (dictionary2.Count != 0 && dictionary2.TryGetValue(SkinUseType.Free, out var value) && value.Count != 0 && appearanceConfig.GetPlayerSkinType(skinType2).BaseSkin)
			{
				if (flag && mTables.ProfessionBaseInfoParser.Get(profession).DefaultSkins.TryGetValue(gender, out var value2) && value2.TryGetValue(skinType2, out var value3))
				{
					appearanceData.SkinMap[skinType2] = new SkinData
					{
						Type = skinType2,
						SkinId = value3
					};
				}
				else
				{
					appearanceData.SkinMap[skinType2] = new SkinData
					{
						Type = skinType2,
						SkinId = value[0]
					};
				}
			}
		}
		CharacterSkinDyeInfo[] all = characterSkinDyeInfoParser.GetAll();
		foreach (CharacterSkinDyeInfo characterSkinDyeInfo in all)
		{
			int num = characterSkinDyeInfo.FixedColorIdList[0];
			CharacterSkinColorInfo characterSkinColorInfo = characterSkinColorInfoParser.Get(num);
			appearanceData.DyeMap[characterSkinDyeInfo.DyeType] = new SkinDyeData
			{
				ColorId = num,
				Hsv = new Hsv(characterSkinColorInfo.H, characterSkinColorInfo.S, characterSkinColorInfo.V)
			};
		}
		return appearanceData;
	}

}

}
