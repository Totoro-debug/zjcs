namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public interface IAppearanceRuntimeDataOverride
{
	void ApplyOverride(AppearanceRuntimeData runtimeData, object appearanceData, Dictionary<SkinType, string> modifiedSkinDict = null);
}

}
