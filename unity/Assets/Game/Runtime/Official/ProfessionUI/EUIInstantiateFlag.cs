namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using LayoutElementClamper = Zjcs.Official.WorldUI.WorldLayoutElementClamper;
using ItemProgress = Zjcs.Official.WorldUI.WorldItemProgress;
using TMPEffect = TMPro.TMPEffect;
public enum EUIInstantiateFlag
{
	None = 0,
	Position = 1,
	Rotation = 2,
	Scale = 4,
	AutoActive = 8,
	Transform = 7,
	KeepScale = 11,
	All = 15
}

}
