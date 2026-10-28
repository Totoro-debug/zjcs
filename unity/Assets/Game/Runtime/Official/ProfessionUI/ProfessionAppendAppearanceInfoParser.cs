namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
public sealed class ProfessionAppendAppearanceInfoParser : ProfessionAppearanceKeyTable<ProfessionAppendAppearanceInfo>
{
	public bool TryGetSkinId(int classId, Sex sex, out int skinId)
	{
		ProfessionAppendAppearanceInfo appendAppearanceInfo = Get(classId);
		skinId = 0;
		if (appendAppearanceInfo == null)
		{
			return false;
		}
		if (appendAppearanceInfo.SkinDict == null || !appendAppearanceInfo.SkinDict.TryGetValue(sex, out skinId))
		{
			return false;
		}
		return true;
	}
}

}
