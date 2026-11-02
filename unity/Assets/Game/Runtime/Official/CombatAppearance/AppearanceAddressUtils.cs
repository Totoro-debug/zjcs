namespace Zjcs.Official.CombatAppearance
{
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using System;
public static class AppearanceAddressUtils
{
public static string GetPlayerAnimatorPath(Sex sex, int profession, PlayerPhase playerPhase, IProfessionPlayerAppearanceBindings bindings, Func<string, bool> locationExists)
	{
		Profession baseProfession = (Profession)bindings.GetBaseProfession(profession);
		switch (playerPhase)
		{
		case PlayerPhase.Game:
		{
			string text2 = ((sex == Sex.Male) ? "controller" : "overrideController");
			return string.Format("{0}{1}_{2}_Controller.{3}", "Assets/AppearanceAssets/Character/Model/Anim/", sex, baseProfession, text2);
		}
		case PlayerPhase.Map:
		{
			string text2 = ((sex == Sex.Male) ? "controller" : "overrideController");
			return string.Format("{0}{1}_{2}_Controller.{3}", "Assets/AppearanceAssets/Character/Model/Anim/", sex, playerPhase, text2);
		}
		case PlayerPhase.MusicMap:
		{
			string text2 = ((sex == Sex.Male && baseProfession == Profession.Zhanshi) ? "controller" : "overrideController");
			return string.Format("{0}{1}_{2}_{3}_Controller.{4}", "Assets/AppearanceAssets/Character/Model/Anim/", sex, playerPhase, baseProfession, text2);
		}
		case PlayerPhase.DanceMap:
		{
			string text = string.Format("{0}{1}/{2}/{3}_{4}_{5}_Controller.{6}", "Assets/AppearanceAssets/Character/Model/Anim/", playerPhase, "CN", sex, playerPhase, baseProfession, "overrideController");
			if (locationExists(text))
			{
				return text;
			}
			return string.Format("{0}{1}/Default/{2}_{3}_{4}_Controller.{5}", "Assets/AppearanceAssets/Character/Model/Anim/", playerPhase, sex, playerPhase, baseProfession, "overrideController");
		}
		case PlayerPhase.Shot:
			return string.Format("{0}{1}_{2}_{3}_Controller.{4}", "Assets/AppearanceAssets/Character/Model/Anim/", sex, playerPhase, baseProfession, "controller");
		case PlayerPhase.Home:
		case PlayerPhase.HomeVisitor:
		case PlayerPhase.HideAndSeek:
		case PlayerPhase.ActivityMap:
			return string.Format("{0}{1}/{2}_{3}_{4}_Controller.{5}", "Assets/AppearanceAssets/Character/Model/Anim/", playerPhase, sex, playerPhase, baseProfession, "overrideController");
		default:
			return string.Empty;
		}
	}
}

}
