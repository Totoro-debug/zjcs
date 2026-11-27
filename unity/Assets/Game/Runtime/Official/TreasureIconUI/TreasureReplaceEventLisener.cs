// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {
using System;

public class TreasureReplaceEventLisener : ITreasureReplaceEventLisener
{
	private TreasureReplaceInfo mReplaceInfo;

	public Action<TreasureReplaceInfo> OnTreasureStartReplace { get; set; }

	public Action OnTreasureCancelReplace { get; set; }

	public void FireOnTreasureCancelReplace()
	{
		OnTreasureCancelReplace?.Invoke();
		mReplaceInfo = null;
	}

	public void FireOnTreasureStartReplace(TreasureReplaceInfo info)
	{
		OnTreasureStartReplace?.Invoke(info);
		mReplaceInfo = info;
	}

	public TreasureReplaceInfo GetReplaceInfo()
	{
		return mReplaceInfo;
	}
}

}
