// Selected original screen adaptation; local platform/service boundaries only.
using System.Collections.Generic;
namespace Zjcs.Official.HomeScreenUI
{

public class ScreenAdaptService : IScreenAdaptService, System.IDisposable
{
	private HashSet<CanvasWidthAdjuster> mSetCanvasWidthAdjusters;

	private bool mShowAdaptBorderPaused;

	public IEnumerable<CanvasWidthAdjuster> CanvasWidthAdjusters => mSetCanvasWidthAdjusters;

	public void PauseShowBorder()
	{
		bool enabled;
		if (mShowAdaptBorderPaused)
		{
			UnityEngine.Debug.LogError("ScreenAdaptService: PauseShowAdaptBorder 重复调用了或是调用不对称， 当前已经补是不显示Border的状态");
			return;
		}
		mShowAdaptBorderPaused = true;
		SetBorderVisible(visible: false);
		UnityEngine.Debug.Log("ScreenAdaptService : PauseShowAdaptBorder");
	}

	public void ResumeShowBorder()
	{
		bool enabled;
		if (!mShowAdaptBorderPaused)
		{
			UnityEngine.Debug.LogError("ScreenAdaptService: ResumeShowAdaptBorder 重复调用了或是调用不对称， 当前已经是显示Border的状态");
			return;
		}
		mShowAdaptBorderPaused = false;
		SetBorderVisible(visible: true);
		UnityEngine.Debug.Log("ScreenAdaptService : ResumeShowAdaptBorder");
	}

	public void RegisterCanvasWidthAdjuster(CanvasWidthAdjuster adjuster)
	{
		if (adjuster == null)
		{
			return;
		}
		if (!mSetCanvasWidthAdjusters.Add(adjuster))
		{
			UnityEngine.Debug.LogError("ScreenAdaptService: AddCanvasWidthAdjuster 重复添加了同一个 CanvasWidthAdjuster");
		}
		else
		{
			adjuster.SetAdaptBorderVisible(!mShowAdaptBorderPaused);
		}
	}

	public void UnregisterCanvasWidthAdjuster(CanvasWidthAdjuster adjuster)
	{
		if (adjuster == null)
		{
			return;
		}
		adjuster.SetAdaptBorderVisible(visible: true);
		if (mSetCanvasWidthAdjusters != null && !mSetCanvasWidthAdjusters.Remove(adjuster))
		{
			UnityEngine.Debug.LogError("ScreenAdaptService: RemoveCanvasWidthAdjuster 尝试移除一个不存在的 CanvasWidthAdjuster");
		}
	}

	public void Initialize()
	{

		mSetCanvasWidthAdjusters = new HashSet<CanvasWidthAdjuster>();
		UnityEngine.Debug.Log("ScreenAdaptService Initialized");
	}

	public void Dispose()
	{
		mSetCanvasWidthAdjusters.Clear();
		mSetCanvasWidthAdjusters = null;
		UnityEngine.Debug.Log("ScreenAdaptService Disposed");

	}

	private void SetBorderVisible(bool visible)
	{
		foreach (CanvasWidthAdjuster mSetCanvasWidthAdjuster in mSetCanvasWidthAdjusters)
		{
			mSetCanvasWidthAdjuster.SetAdaptBorderVisible(visible);
		}
	}
}

}
