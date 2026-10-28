using System.Collections.Generic;


namespace Zjcs.Official.World.Map13EntityRendering
{
public class SlicedFilledRenderService
{
	public static readonly SlicedFilledRenderService Current = new SlicedFilledRenderService();

	private readonly HashSet<SlicedFilledRender> mObjects = new HashSet<SlicedFilledRender>();

	public void RegisterForRefresh(SlicedFilledRender obj)
	{
		mObjects.Add(obj);
	}

	public void UnregisterForRefresh(SlicedFilledRender obj)
	{
		mObjects.Remove(obj);
	}

	public void RefreshAll()
	{
		foreach (SlicedFilledRender mObject in mObjects)
		{
			mObject.RefreshMesh();
		}
		mObjects.Clear();
	}

	public void LateUpdate()
	{
		RefreshAll();
	}
}

}
