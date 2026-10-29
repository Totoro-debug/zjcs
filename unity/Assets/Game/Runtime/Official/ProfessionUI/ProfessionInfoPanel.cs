namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionInfoPanel : MonoBehaviour, LoopScrollDataSource, IProfessionDetailInfoPanel
{
    private IProfessionInfoBindings mBindings;
    private IProfessionInfoBindings Bindings => mBindings ?? ProfessionInfoBindings.Required;
    public void Configure(IProfessionInfoBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	public WorldMultiLanguageText Desc;

	public ProfessionEquipTypeIcon WeaponType;

	public ProfessionEquipTypeIcon SecondWeaponType;

	public ProfessionPropRadar Radar;

	public LoopScrollRect SkillScrollRect;

	private int mPro;

	private ProfessionDetailDefinition mInfo;

	private List<IProfessionInfoSkill> mAllSkills = new List<IProfessionInfoSkill>();

	private List<IProfessionInfoSkill> mPassiveSkills = new List<IProfessionInfoSkill>();

	public void Init(int pro, GameObjectPool<ProfessionPlayerPropItem> propPool)
	{
		mPro = pro;
		mInfo = Bindings.GetProfession(pro);
		Desc.SetKey(Bindings.GetProfessionDescKey(mPro));
		UpdateWeaponIcons();
		Radar.ShowProp(Bindings.GetFeatureProps(mPro), Bindings.GetRadarColor(mPro), Bindings.InitPropItem);
		UpdateSkillGrid();
	}

	private void UpdateWeaponIcons()
	{
		List<int> supportEquipTypeByProfession = Bindings.GetSupportWeaponTypes(mPro);
		WeaponType.RefeshView(supportEquipTypeByProfession[0]);
		supportEquipTypeByProfession = Bindings.GetSupportSecondWeaponTypes(mPro);
		bool flag = supportEquipTypeByProfession != null && supportEquipTypeByProfession.Count > 0;
		((Component)SecondWeaponType).gameObject.SetActive(flag);
		if (flag)
		{
			SecondWeaponType.RefeshView(supportEquipTypeByProfession[0]);
		}
	}

	private void UpdateSkillGrid()
	{
		SortSkills();
		SkillScrollRect.dataSource = this;
		SkillScrollRect.ClearCells();
		SkillScrollRect.totalCount = mAllSkills.Count;
		SkillScrollRect.RefillCells();
	}

	private void SortSkills()
	{
		mAllSkills.Clear();
		mPassiveSkills.Clear();
		if (mInfo.Skills == null)
		{
			return;
		}
		int count = mInfo.Skills.Count;
		for (int i = 0; i < count; i++)
		{
			int key = mInfo.Skills[i];
			IProfessionInfoSkill itemInfo = Bindings.GetItemInfo(key);
			if (itemInfo.ItemType == 100)
			{
				mAllSkills.Add(itemInfo);
			}
			else if (itemInfo.ItemType == 101)
			{
				mPassiveSkills.Add(itemInfo);
			}
		}
		ProfessionInfoSkillComparer sDefault = ProfessionInfoSkillComparer.sDefault;
		mAllSkills.Sort(sDefault.DefaultComparer);
		mPassiveSkills.Sort(sDefault.DefaultComparer);
		mAllSkills.AddRange(mPassiveSkills);
	}

	public void ProvideData(Transform transform, int idx)
	{
		ProfessionInfoPanelSkillItem professionInfoPanelSkillItem = default;
		if (((Component)transform).TryGetComponent<ProfessionInfoPanelSkillItem>(out professionInfoPanelSkillItem))
		{
			IProfessionInfoSkill skillInfo = mAllSkills[idx];
			professionInfoPanelSkillItem.Init(skillInfo);
		}
	}
}

}
