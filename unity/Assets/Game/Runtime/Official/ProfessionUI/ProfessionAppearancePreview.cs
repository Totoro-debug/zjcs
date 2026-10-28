namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
public static class ProfessionAppearancePreview
{
	public static void SetProfessionAppearance(IProfessionAppearancePreviewBindings bindings, AppearanceConfig appearanceConfig, AppearanceCharacter characterAppearance, Sex sex, int profession, object appearanceData, AppendSkinType appendSkinType)
	{
		if ((Object)(object)characterAppearance == (Object)null)
		{
			return;
		}
		ProfessionAppearancePreviewData appearanceData2 = bindings.CloneAppearance(appearanceData);
		ProfessionAppearancePreviewDefinition professionBaseInfo = bindings.GetProfession(profession);
		if (profession != 0)
		{
			if (professionBaseInfo.DefaultSkins.TryGetValue(sex, out var value))
			{
				appearanceData2.AppendAppearanceDict.Remove(SkinType.pWeapon);
				if (value.TryGetValue(SkinType.pWeapon, out var value2))
				{
					appearanceData2.EquipSkinDict[EquipPos.Weapon] = value2;
				}
				else
				{
					appearanceData2.EquipSkinDict.Remove(EquipPos.Weapon);
				}
				appearanceData2.AppendAppearanceDict.Remove(SkinType.sWeapon);
				if (value.TryGetValue(SkinType.sWeapon, out var value3))
				{
					appearanceData2.EquipSkinDict[EquipPos.SecondWeapon] = value3;
				}
				else
				{
					appearanceData2.EquipSkinDict.Remove(EquipPos.SecondWeapon);
				}
			}
			if (professionBaseInfo.AppendAppearanceRewards.TryGetValue(sex, out var value4))
			{
				foreach (KeyValuePair<SkinType, int> item in value4)
				{
					appearanceData2.AppendAppearanceDict[item.Key] = item.Value;
				}
			}
		}
		AppearanceRuntimeData runtimeData = new AppearanceRuntimeData();
        bindings.Player.BuildAppearanceRuntimeData(sex, profession, appearanceData2.Source, appearanceConfig.GetAppendSkins(appendSkinType), runtimeData);
		characterAppearance.SetRuntimeData(runtimeData);
	}

}

}
