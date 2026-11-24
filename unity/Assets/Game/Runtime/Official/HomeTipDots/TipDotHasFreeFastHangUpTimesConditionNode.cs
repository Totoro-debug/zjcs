// Selected original Home TipDot condition body; explicit local ports.
using System;
using System.Collections.Generic;
using Zjcs.Official.HomeUI;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeTipDots
{
[Serializable]
public class TipDotHasFreeFastHangUpTimesConditionNode : TipDotConditionNodeBase, IHomeTipDotNode
{
	public IHomeTipDotServices Services { get; set; }

	public EFurniture Furniture;

	private List<HomeTipFastHangUpInfo> mFastHangUpList;

	private IDisposable mEventDisposable;

	public override void Init()
	{
		base.Init();
		if (Services.TryGetFastHangUpInfo(Furniture, out mFastHangUpList))
		{
			Services.Player.OnHangUpUnlock += OnHangUpUnlock;
			Services.Player.OnFastHangUp += OnFastHangUp;
			Services.Player.OnDebugTimeOffset += OnDebugTimeOffset;
			mEventDisposable = Services.SubscribeEnterNewDay(OnEnterNewDay);
		}
	}

	public override void Release()
	{
		mEventDisposable?.Dispose();
		mEventDisposable = null;
		if (Services.Player != null)
		{
			Services.Player.OnHangUpUnlock -= OnHangUpUnlock;
			Services.Player.OnFastHangUp -= OnFastHangUp;
			Services.Player.OnDebugTimeOffset -= OnDebugTimeOffset;
		}
		base.Release();
	}

	private void OnFastHangUp(int fastHangUpId)
	{
		if (mFastHangUpList == null)
		{
			return;
		}
		foreach (HomeTipFastHangUpInfo mFastHangUp in mFastHangUpList)
		{
			if (mFastHangUp.FastHangUpId == fastHangUpId)
			{
				SetDirty();
				break;
			}
		}
	}

	private void OnEnterNewDay()
	{
		SetDirty();
	}

	private void OnDebugTimeOffset(long _)
	{
		SetDirty();
	}

	private void OnHangUpUnlock(int hangUpId)
	{
		if (Services.GetHangUpBindFurniture(hangUpId) == Furniture)
		{
			SetDirty();
		}
	}

	public override bool CheckShowTip()
	{
		foreach (HomeTipFastHangUpInfo mFastHangUp in mFastHangUpList)
		{
			if (Services.Player.CanFreeFastHangUp(mFastHangUp.FastHangUpId))
			{
				return true;
			}
		}
		return false;
	}

	public override TipDotNodeBase Clone()
	{
		TipDotHasFreeFastHangUpTimesConditionNode tipDotHasFreeFastHangUpTimesConditionNode = new TipDotHasFreeFastHangUpTimesConditionNode();
		tipDotHasFreeFastHangUpTimesConditionNode.Fill(this);
		return tipDotHasFreeFastHangUpTimesConditionNode;
	}

	public virtual void Fill(TipDotNodeBase source)
	{
		TipDotHasFreeFastHangUpTimesConditionNode tipDotHasFreeFastHangUpTimesConditionNode = source as TipDotHasFreeFastHangUpTimesConditionNode;
		if (tipDotHasFreeFastHangUpTimesConditionNode == null)
		{
			throw new ArgumentException(GetType().Name, nameof(source));
		}
		Fill(tipDotHasFreeFastHangUpTimesConditionNode);
	}

	public void Fill(TipDotHasFreeFastHangUpTimesConditionNode obj)
	{
		Furniture = obj.Furniture;
	}
}

}
