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

}

}
