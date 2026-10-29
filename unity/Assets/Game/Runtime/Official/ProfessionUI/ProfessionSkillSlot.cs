namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using UnityEngine;

public class ProfessionSkillSlot : MonoBehaviour, IGameObjectPoolItem
{
    private IProfessionSkillIconBindings mBindings;
    private IProfessionSkillIconBindings Bindings => mBindings ?? ProfessionSkillIconBindings.Required;
    public void Configure(IProfessionSkillIconBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	[SerializeField]
	private Transform mSlot;

	private ItemIconSkill mIcon;

	private int mItemId;


	public void Init(int itemId)
	{
		mItemId = itemId;
		RecycleIcon();
		mIcon = Bindings.GetIcon(mItemId, mSlot);
	}

	public void Init(IProfessionItemData itemData)
	{
		mItemId = itemData.Id;
		RecycleIcon();
		mIcon = Bindings.GetIcon(itemData, mSlot);
	}

	public void OnDisable()
	{
		RecycleIcon();
	}

	public void OnGameObjectPoolClear()
	{
		RecycleIcon();
	}

	public void OnGameObjectPoolReset()
	{
		RecycleIcon();
	}

	public bool TryGetItemIcon(out ItemIconSkill itemIcon)
	{
		itemIcon = mIcon;
		return (Object)(object)mIcon != (Object)null;
	}

	private void RecycleIcon()
	{
		if ((Object)(object)mIcon != (Object)null && (mBindings ?? ProfessionSkillIconBindings.Current) != null)
		{
			Bindings.RecycleIcon(mIcon);
		}
		mIcon = null;
	}
}

}
