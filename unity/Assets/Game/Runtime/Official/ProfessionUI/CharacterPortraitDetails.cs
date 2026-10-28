namespace Zjcs.Official.ProfessionUI
{
using System.Collections.Generic;
public static class CharacterPortraitDetails
{
	public static long GetAppearanceHashCode(this object appearanceData, PlayerPortraitSettings.PhotographyParameters param, ICharacterPortraitSkinHashProvider hashProvider, IProfessionPortraitAppearanceReader reader)
	{
		long num = reader.GetModifyTimeStamp(appearanceData);
		foreach (KeyValuePair<SkinType, object> item in reader.GetSkins(appearanceData))
		{
			if (param.GetHashWeightsBySkinType(item.Key) > 0)
			{
				object value = item.Value;
				long skinHash = hashProvider.GetSkinHash(reader.GetSkinId(value));
				num ^= skinHash;
			}
		}
		return num;
	}

	public static PlayerPortraitSettings.PhotographyParameters GetParam(this ICharacterPortraitRequestor requestor, PlayerPortraitSettings setting)
	{
		return setting.GetParameters(requestor.GetPortraitType());
	}

	public static void GetDetailRequestData(this ICharacterPortraitRequestor requestor, PlayerPortraitSettings setting, ICharacterPortraitSkinHashProvider provider, IProfessionPortraitAppearanceReader reader, out CharacterPortraitDetailRequestData data)
	{
		object appearanceData = requestor.GetAppearanceData();
		PlayerPortraitSettings.PhotographyParameters param = requestor.GetParam(setting);
		data = new CharacterPortraitDetailRequestData
		{
			RequestId = requestor.GetRequestId(),
			VersionId = reader.GetModifyTimeStamp(appearanceData),
			AppearanceHashCode = appearanceData.GetAppearanceHashCode(param, provider, reader)
		};
	}

	public static bool IsVaildCacha(this CharacterPortraitCache cache, in CharacterPortraitDetailRequestData data)
	{
		return cache.AppearanceHashCode == data.AppearanceHashCode;
	}
}
}
