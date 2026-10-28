namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using System;

public class ProfessionLiteInfoCallback : ProfessionLiteInfoCallbackBase<ProfessionPlayerLiteAppearance>
{
	public ProfessionLiteInfoCallback(Action<ProfessionPlayerLiteAppearance> callback)
		: base(callback)
	{
	}
}

}
