namespace Zjcs.Official.PartnersUI
{
public static class PartnerDialoguePlaybackRules
{
	public static string GetNpcDialogueOptionKey(int dialogueClassId, int dialogueNodeId)
	{
		return $"npc_dialogue_{dialogueClassId}_{dialogueNodeId}_option_text";
	}
}
}
