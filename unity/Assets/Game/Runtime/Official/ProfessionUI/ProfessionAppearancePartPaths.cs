namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
public static class ProfessionAppearancePartPaths
{
    private static readonly string[] sKPartModeSuffix = new string[3] { "", "Integrated", "Home" };
	public static string GetPlayerPartPrefabRenderer(PlayerPartMode partMode, string skinName)
	{
		if (partMode == PlayerPartMode.Standalone)
		{
			return "Appearance/PlayerPart/" + skinName + "/" + skinName;
		}
		return "Appearance/PlayerPart/" + skinName + "/" + skinName + "_" + sKPartModeSuffix[(int)partMode];
	}

	public static string GetPlayerPartPrefabUI(PlayerPartMode partMode, string skinName)
	{
		if (partMode == PlayerPartMode.Standalone)
		{
			return "Appearance/PlayerPart/" + skinName + "/" + skinName + "_UI";
		}
		return "Appearance/PlayerPart/" + skinName + "/" + skinName + "_UI_" + sKPartModeSuffix[(int)partMode];
	}

	public static string GetSkinEffectPath(AppearanceRenderType rendererType, string skinName)
	{
		return string.Format("{0}PlayerEffect/{1}/{2}", "Appearance/", rendererType, skinName);
	}

}

}
