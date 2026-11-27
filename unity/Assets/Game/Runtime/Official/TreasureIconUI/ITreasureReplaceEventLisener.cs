// Original treasure UI controller; explicit local bindings only.
using Object=UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureCultivation;
namespace Zjcs.Official.TreasureUI {
using System;

public interface ITreasureReplaceEventLisener
{
	Action<TreasureReplaceInfo> OnTreasureStartReplace { get; set; }

	Action OnTreasureCancelReplace { get; set; }

	TreasureReplaceInfo GetReplaceInfo();

	void FireOnTreasureStartReplace(TreasureReplaceInfo info);

	void FireOnTreasureCancelReplace();
}

}
