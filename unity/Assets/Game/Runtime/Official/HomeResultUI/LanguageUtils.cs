namespace Zjcs.Official.HomeResultUI
{
internal static class LanguageUtils
{
	public static string GetStageDemonBattlefieldNameKey(int entryIndex)
	{
		return $"stage_DemonBattlefield_{entryIndex}";
	}

	public static string GetStageNameKey(int stageId)
	{
		return $"stage_{stageId}_name";
	}

	public static string GetVoidFissureNameKey(int layer)
	{
		return $"stage_VoidFissure_{layer}";
	}
}
}
