namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public interface IAppearanceRuntimeDataOverride
{
	void CollectOverrideSkins(Sex sex, Dictionary<SkinType, int> result);

	void ApplyOverride(AppearanceRuntimeData runtimeData);
}

}
