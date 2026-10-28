namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System.Collections.Generic;

public class AppearanceRuntimeData
{
	public Sex Sex { get; set; }

	public Dictionary<SkinType, string> SkinMap { get; set; }

	public Dictionary<SkinType, Dictionary<int, HsvChange>> DyeMap { get; set; }

	public List<string> AppendSkins { get; set; }

	public AppearanceRuntimeData(Sex sex, Dictionary<SkinType, string> skinMap = null, Dictionary<SkinType, Dictionary<int, HsvChange>> dyeMap = null, List<string> appendSkins = null)
	{
		Sex = sex;
		SkinMap = skinMap ?? new Dictionary<SkinType, string>(1);
		DyeMap = dyeMap;
		AppendSkins = appendSkins;
	}

	public AppearanceRuntimeData()
	{
	}
}

}
