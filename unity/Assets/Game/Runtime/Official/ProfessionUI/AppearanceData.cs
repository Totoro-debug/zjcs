namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;

public class AppearanceData
{
		public Dictionary<SkinType, SkinData> SkinMap = new Dictionary<SkinType, SkinData>();

		public Dictionary<DyeType, SkinDyeData> DyeMap = new Dictionary<DyeType, SkinDyeData>();

		public Dictionary<SkinType, int> AppendAppearanceDict = new Dictionary<SkinType, int>();

		public Dictionary<EquipPos, int> EquipSkinDict = new Dictionary<EquipPos, int>();

		public Dictionary<HonorType, int> HonorItemDict = new Dictionary<HonorType, int>();

		public long ModifyTimeStamp;

		public int AppearanceLevel;

		public int FullBodyAppearanceId;

		public HashSet<int> LimitItemExpSet = new HashSet<int>();

		public Dictionary<SkinType, DateTime> AppendAppearanceWearTimeDict = new Dictionary<SkinType, DateTime>();

	public bool Valid => SkinMap.Count > 0;

	public AppearanceData Clone()
	{
		AppearanceData appearanceData = new AppearanceData
		{
			SkinMap = ((SkinMap == null) ? new Dictionary<SkinType, SkinData>() : new Dictionary<SkinType, SkinData>(SkinMap)),
			DyeMap = ((DyeMap == null) ? new Dictionary<DyeType, SkinDyeData>() : new Dictionary<DyeType, SkinDyeData>(DyeMap)),
			AppendAppearanceDict = ((AppendAppearanceDict == null) ? new Dictionary<SkinType, int>() : new Dictionary<SkinType, int>(AppendAppearanceDict)),
			EquipSkinDict = ((EquipSkinDict == null) ? new Dictionary<EquipPos, int>() : new Dictionary<EquipPos, int>(EquipSkinDict)),
			HonorItemDict = ((HonorItemDict == null) ? new Dictionary<HonorType, int>() : new Dictionary<HonorType, int>(HonorItemDict)),
			ModifyTimeStamp = ModifyTimeStamp,
			AppearanceLevel = AppearanceLevel,
			FullBodyAppearanceId = FullBodyAppearanceId,
			LimitItemExpSet = ((LimitItemExpSet == null) ? new HashSet<int>() : new HashSet<int>(LimitItemExpSet)),
			AppendAppearanceWearTimeDict = ((AppendAppearanceWearTimeDict == null) ? new Dictionary<SkinType, DateTime>() : new Dictionary<SkinType, DateTime>(AppendAppearanceWearTimeDict))
		};
		if (SkinMap != null)
		{
			foreach (KeyValuePair<SkinType, SkinData> item in SkinMap)
			{
				appearanceData.SkinMap[item.Key] = new SkinData
				{
					Type = item.Value.Type,
					SkinId = item.Value.SkinId
				};
			}
		}
		if (DyeMap != null)
		{
			foreach (KeyValuePair<DyeType, SkinDyeData> item2 in DyeMap)
			{
				appearanceData.DyeMap[item2.Key] = new SkinDyeData
				{
					ColorId = item2.Value.ColorId,
					Hsv = item2.Value.Hsv
				};
			}
		}
		return appearanceData;
	}

}

}
