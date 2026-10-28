namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using Zjcs.Official.WorldUI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ProfessionChangeJumpBtn : MonoBehaviour
{
    private IProfessionDetailBindings mBindings;
    private IProfessionDetailBindings Bindings => mBindings ?? ProfessionDetailBindings.Required;
    public void Configure(IProfessionDetailBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }

	[SerializeField]
	private Image mTutorHeadImg;

	[SerializeField]
	private Button mJumpBtn;

	private int mTargetProfression;

	private ProfessionResourceOperationBag mDisposableBag;







	public void Awake()
	{
		mDisposableBag = new ProfessionResourceOperationBag();
		mJumpBtn.onClick.AddListener(Jump);
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

	public void SetProfession(int profession)
	{
		int highestRank = Bindings.HighestRank;
		ProfessionDetailDefinition professionBaseInfo = Bindings.GetProfession(profession);
		int num = professionBaseInfo.Rank;
		while (num <= highestRank && num > 0)
		{
			List<int> successor = Bindings.GetSuccessor(professionBaseInfo.Profession);
			if (successor == null || successor.Count == 0)
			{
				break;
			}
			int profession2 = successor[0];
			if (Bindings.IsProfessionTutorUnlock(profession2))
			{
				professionBaseInfo = Bindings.GetProfession(profession2);
				num++;
				continue;
			}
			if (Bindings.IsProfessionTutorUnlock(professionBaseInfo.Profession))
			{
				break;
			}
			professionBaseInfo = Bindings.GetProfession(professionBaseInfo.PrePro);
			num--;
		}
		mTargetProfression = professionBaseInfo.Profession;
		if ((Object)(object)mTutorHeadImg != (Object)null)
		{
			string assetGuid = Bindings.GetTutorHeadGuid(professionBaseInfo.Profession);
			mTutorHeadImg.sprite = mDisposableBag.Add(Bindings.ResourceLoader.LoadAssetByGuid<Sprite>(assetGuid)).Result;
		}
	}

	private void Jump()
	{
		ProfessionDetailDefinition professionBaseInfo = Bindings.GetProfession(mTargetProfression);
		if (mTargetProfression == 0 || !Bindings.TryGotoTutor(professionBaseInfo.TutorEntityTag))
		{
			Bindings.ShowSystemMessage("ui_profession_tutor_not_found");
		}
	}
}

}
