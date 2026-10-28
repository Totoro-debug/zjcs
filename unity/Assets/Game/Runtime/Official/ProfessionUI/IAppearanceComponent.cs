namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public interface IAppearanceComponent
{
	bool Valid { get; }

	void Init(ScriptableObject skeletonDataAsset, ScriptableObject spineAnimationSplitBinarySo);

	void SetSkins(List<string> skins);

	void SetDye(string skinName, Dictionary<int, HsvChange> hsvChangeDic);

	bool TryGetMaterial(string skinName, out Material material);

	void Release(bool tryUnloadAllAssets = false);
}

}
