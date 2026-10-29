namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using UnityEngine;

public class ProfessionInfoPanelSkillItem : MonoBehaviour
{
    private IProfessionSkillIconBindings mBindings;
    private IProfessionSkillIconBindings Bindings => mBindings ?? ProfessionSkillIconBindings.Required;
    public void Configure(IProfessionSkillIconBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public ProfessionSkillSlot SkillSlot;

	public void Init(IProfessionItemInfo skillInfo)
	{
		IProfessionSkillIconPlayer mainPlayer = Bindings.MainPlayer;
		bool flag = mainPlayer.HasItem(skillInfo.ClassId, 1L);
		if (flag)
		{
			IProfessionItemData itemById = mainPlayer.GetItemById(skillInfo.ClassId);
			SkillSlot.Init(itemById);
		}
		else
		{
			SkillSlot.Init(skillInfo.ClassId);
		}
		if (SkillSlot.TryGetItemIcon(out var itemIcon))
		{
			itemIcon.SetClickPopupInfoDialog();
			itemIcon.SetAttachment(ItemIconAttachmentType.NotGotTip, !flag);
		}
	}
}

}
