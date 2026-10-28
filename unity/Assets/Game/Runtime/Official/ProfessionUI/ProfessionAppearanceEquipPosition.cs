namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;
public static class ProfessionAppearanceEquipPosition
{
	public static EquipPos ToEquipPos(this SkinType skinType)
	{
		switch (skinType)
		{
		case SkinType.body:
			return EquipPos.Clothes;
		case SkinType.pWeapon:
			return EquipPos.Weapon;
		case SkinType.sWeapon:
			return EquipPos.SecondWeapon;
		default:
		{
			Debug.LogWarning("ToEquipPos not support type:" + (skinType));
			return EquipPos.Clothes;
		}
		}
	}
}

}
