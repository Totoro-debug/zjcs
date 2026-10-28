namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using UnityEngine;

public abstract class APlayerAppearanceInfo : MonoBehaviour
{
	public abstract void RefreshAppearance(Sex sex, int profession, AppearanceRuntimeData appearanceRuntimeData);
}

}
