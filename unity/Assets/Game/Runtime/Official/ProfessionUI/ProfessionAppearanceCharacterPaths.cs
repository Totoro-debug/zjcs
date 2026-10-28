namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
public static class ProfessionAppearanceCharacterPaths
{
	public static string GetPlayerSkeletonAssetPath(Sex sex)
	{
		return sex switch
		{
			Sex.None => string.Empty,
			Sex.Male => GetPlayerPath(sex) + "character_Male_1_SkeletonData.asset",
			_ => GetPlayerPath(sex) + "character_Female_1_SkeletonData.asset",
		};
	}

	public static string GetPlayerSplitAnimationSoPath(Sex sex)
	{
		return sex switch
		{
			Sex.None => string.Empty,
			Sex.Male => GetPlayerPath(sex) + "character_Male_1_SkeletonData_SplitBinarySO.asset",
			_ => GetPlayerPath(sex) + "character_Female_1_SkeletonData_SplitBinarySO.asset",
		};
	}

	public static string GetPlayerPath(Sex sex)
	{
		return sex switch
		{
			Sex.None => string.Empty,
			Sex.Male => "Assets/AppearanceAssets/Character/Model/Male_1/",
			_ => "Assets/AppearanceAssets/Character/Model/Female_1/",
		};
	}

}

}
