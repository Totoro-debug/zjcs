using UnityEngine;

namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
public class MiniMapActiveByNPCVisibleSupporter : MiniMapMarkSupportBase
{
	[Header("目标显示的时候打勾内容里的物件显示")]
	[SerializeField]
	private StateSwitcher<bool> mShowSwitcher;

	private IMiniMapMarkEntity mEnt;

	private IClientVisibilityHandler mMyVisibility;

	public override void Init(MiniMapMarkUIBase markUi)
	{
		if (markUi.Mark is MiniMapEntityMark miniMapEntityMark)
		{
			mEnt = miniMapEntityMark.Entity;
			DoInit();
		}
	}

	public override void Release(MiniMapMarkUIBase markUi)
	{
		if (mMyVisibility != null)
		{
			mMyVisibility.OnVisibleChanged -= OnVisibleChanged;
		}
		mMyVisibility = null;
		mEnt = null;
	}

	private void DoInit()
	{
		mMyVisibility = GetNpcClientVisibilityHandler();
		if (mMyVisibility != null)
		{
			mMyVisibility.OnVisibleChanged += OnVisibleChanged;
			Refresh();
		}
	}

	private void OnVisibleChanged(bool visible)
	{
		Refresh();
	}

	private void Refresh()
	{
		mShowSwitcher.SetState(mMyVisibility.Visible);
	}

	private IClientVisibilityHandler GetNpcClientVisibilityHandler()
	{
		if (mEnt == null)
		{
			return null;
		}
		return mEnt.GetComponent<IClientVisibilityHandler>();
	}
}

}
