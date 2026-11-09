// Original battle entity loading specialization.
using System;
namespace Zjcs.Official.EntityLoading
{
public class MyFightEntityLoadHandler : MyEntityLoadHandler
{
	public MyFightEntityLoadHandler(IServiceProvider serviceProvider, IEntityLoadContainer container, bool fixPosBeforeReturn = false)
		: base(serviceProvider, container, fixPosBeforeReturn)
	{
	}

	public override void OnLowMemory()
	{
		mMyEntityPoolCollection.Clear();
		UnityEngine.Debug.Log("Original entity loading diagnostic");
	}
}

}
