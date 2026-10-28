namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
public static class ProfessionWeaponAnimationNames
{
    private static readonly Dictionary<int, string> sPlayerWeaponAnimDict = new Dictionary<int, string>();
	public static string GetPlayerWeaponAnimName(IProfessionPlayerAppearanceBindings bindings, int profession, bool faceRight)
	{
		int baseProfession = bindings.GetBaseProfession(profession);
		if (baseProfession == (int)Profession.None)
		{
			return string.Empty;
		}
		int key = (int)(((!faceRight) ? 10000 : 0) + baseProfession);
		if (!sPlayerWeaponAnimDict.TryGetValue(key, out var value))
		{
			value = string.Format("{0}/loop_{1}", GetProfessionId(bindings, baseProfession), faceRight ? "R" : "L");
			sPlayerWeaponAnimDict.Add(key, value);
		}
		return value;
	}

	public static int GetProfessionId(IProfessionPlayerAppearanceBindings bindings, int profession)
	{
		string professionIdStr = bindings.GetProfessionIdStr(profession);
		if (!string.IsNullOrEmpty(professionIdStr))
		{
			return int.Parse(professionIdStr);
		}
		return 0;
	}

}

}
