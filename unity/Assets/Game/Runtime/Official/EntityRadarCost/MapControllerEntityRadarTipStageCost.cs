// Original client cost radar tip; see map14 radar-cost source receipt.
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using TMPro;
using Zjcs.Official.EntityRadar;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldInput;
using MultiLanguageText = Zjcs.Official.World.NativeLabelUI.MultiLanguageText;
namespace Zjcs.Official.EntityRadarCost
{

public class MapControllerEntityRadarTipStageCost : MapControllerEntityRadarTipBase
{
	[SerializeField]
	private SpriteRenderer mIconImg;

	[SerializeField]
	private MultiLanguageText mCostTxt;

	[SerializeField]
	private List<float> mIconPosXList;

	private TextMeshPro mCostTextMeshPro;

	private RadarCostResourceBag mDisposableBag;

    public IRadarStageCostQueries CostQueries { get; set; }
    public IRadarItemIconQueries ItemIcons { get; set; }

		public IWorldInputResourceLoader ResourceLoader { get; set; }

	public override RadarTipType TipType => RadarTipType.StageCost;

	private TextMeshPro CostTextMeshPro
	{
		get
		{
			if ((Object)(object)mCostTextMeshPro == (Object)null)
			{
				mCostTextMeshPro = ((Component)mCostTxt).GetComponent<TextMeshPro>();
			}
			return mCostTextMeshPro;
		}
	}

	private void Awake()
	{

		mDisposableBag = default;
	}

	private void OnDestroy()
	{
		mDisposableBag.Dispose();
	}

	public override void Init(MyEntity myEnt)
	{
		base.Init(myEnt);
		Dictionary<int, long> entityCost = GetEntityCost(myEnt);
		if ((entityCost?.Count ?? 0) <= 0)
		{
			return;
		}
		string text = string.Empty;
		using (Dictionary<int, long>.Enumerator enumerator = entityCost.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				KeyValuePair<int, long> current = enumerator.Current;
				text = current.Value.ToString();
				string icon = ItemIcons.GetItemIcon(current.Key);
				mIconImg.sprite = mDisposableBag.Add(ResourceLoader.LoadSprite(icon)).Result;
			}
		}
		mCostTxt.SetKey("{0}", text);
		AlignCenter();
	}

	public override bool CanShowTip(MyEntity myEnt)
	{
		return (GetEntityCost(myEnt)?.Count ?? 0) > 0;
	}

	private Dictionary<int, long> GetEntityCost(MyEntity myEnt)
	{
		if ((Object)(object)myEnt == (Object)null)
		{
			return null;
		}
		ECViewEntityData entity = myEnt.Entity;
		if (entity == null)
		{
			return null;
		}
		return CostQueries.GetStageCost(entity);
	}

	private void AlignCenter()
	{

		((TMP_Text)CostTextMeshPro).ForceMeshUpdate(false, false);
		TMP_TextInfo textInfo = ((TMP_Text)CostTextMeshPro).textInfo;
		if (textInfo != null)
		{
			int characterCount = textInfo.characterCount;
			if (characterCount < mIconPosXList.Count)
			{
				float num = mIconPosXList[characterCount];
				((Component)mIconImg).transform.localPosition = new Vector3(num, 0f, 0f);
			}
		}
	}
}

}
