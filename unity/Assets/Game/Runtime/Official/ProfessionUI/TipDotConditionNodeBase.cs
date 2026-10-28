// Ported from the verified official source; only dependency/serialization adapters differ.
using System;

namespace Zjcs.Official.ProfessionUI
{
public abstract class TipDotConditionNodeBase : TipDotNodeBase
{
	public IProfessionTipDotPlayer Player { get; set; }

public IProfessionTipDotRules Rules { get; set; }

private bool mIsDirty;

	private bool mForceUpdate = true;

	public override bool IsActive
	{
		get
		{
			return base.IsActive;
		}
		set
		{
			base.IsActive = value;
			if (mIsDirty && value)
			{
				SetDirty();
			}
		}
	}

	public override void Init()
	{
		base.Init();
		ITipDotNodeManager nodeManager = NodeManager;
		nodeManager.OnAddScopeDataCollector = (Action)Delegate.Combine(nodeManager.OnAddScopeDataCollector, new Action(AddScopeDataCollector));
		ITipDotNodeManager nodeManager2 = NodeManager;
		nodeManager2.OnRemoveScopeDataCollector = (Action)Delegate.Combine(nodeManager2.OnRemoveScopeDataCollector, new Action(RemoveScopeDataCollector));
		AddScopeDataCollector();
	}

	public override void Release()
	{
		RemoveScopeDataCollector();
		ITipDotNodeManager nodeManager = NodeManager;
		nodeManager.OnAddScopeDataCollector = (Action)Delegate.Remove(nodeManager.OnAddScopeDataCollector, new Action(AddScopeDataCollector));
		ITipDotNodeManager nodeManager2 = NodeManager;
		nodeManager2.OnRemoveScopeDataCollector = (Action)Delegate.Remove(nodeManager2.OnRemoveScopeDataCollector, new Action(RemoveScopeDataCollector));
		NodeManager.RemoveDirtyConditionNode(this);
		base.Release();
	}

	public abstract bool CheckShowTip();

	public void SetDirty()
	{
		mIsDirty = true;
		if (IsActive)
		{
			ReportDirtyNode();
		}
	}

	public void ForceUpdate()
	{
		mForceUpdate = true;
		ReportDirtyNode();
	}

	private void ReportDirtyNode()
	{
		NodeManager.AddDirtyConditionNode(this);
	}

	public override void UpdateNode()
	{
		if (!mForceUpdate && (!IsActive || !mIsDirty))
		{
			return;
		}
		mIsDirty = false;
		mForceUpdate = false;
		TipDotStateInfo tipDotInfo = TipDotInfo;
		bool flag = false;
		try
		{
			flag = CheckShowTip();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		if (PresetData.IsReverse)
		{
			flag = !flag;
		}
		if (IsTipDotBan())
		{
			flag = false;
		}
		if (IsShowTip != flag || !(TipDotInfo.Data == tipDotInfo.Data))
		{
			IsShowTip = flag;
			if (IsShowTip)
			{
				TipDotInfo.TipLevel = ETipDotLevelType.Important;
			}
			else
			{
				TipDotInfo.TipLevel = ETipDotLevelType.None;
			}
			FireStateChanged();
		}
	}
}
}
