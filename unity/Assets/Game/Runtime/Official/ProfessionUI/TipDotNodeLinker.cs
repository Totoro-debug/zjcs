// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
public class TipDotNodeLinker : MonoBehaviour
{
	public bool TempBeLimitShow;

	public SafeAction<TipDotStateInfo> OnStateChanged = new SafeAction<TipDotStateInfo>();

	[SerializeField]
	private TipDotGraphNodeSelector mGraphNodeSelector;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldStateSwitcher<ETipDotLevelType> mTipLevelState;

	[SerializeField]
	private Zjcs.Official.WorldUI.WorldMultiLanguageText mNodeDataText;

	[SerializeField]
	private GameObject mTipDotContent;

	private TipDotStateInfo mStateInfo;

	[SerializeField]

	private bool mNeedShowData = true;

	private bool mIsEnabled;

	private TipDotNodeKey mWaitLinkNodeKey;

	private TipDotNodeKey mLinkedNodeKey;

	public bool IsShowTip => mStateInfo.TipLevel != ETipDotLevelType.None;

	public bool IsShowData => mNeedShowData;

	public TipDotStateInfo StateInfo => mStateInfo;

	public IProfessionTipDotSystemController Controller { get; set; }

	public IProfessionTipDotSystem TipDotSystem { get; set; }

	private void OnEnable()
	{
		mIsEnabled = true;
		OnSetShow();
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Combine(tipDotSystem.OnTipDotShowChanged, new Action(OnSetShow));
		TipDotSystem.OnTipDotSystemInited += new Action(ConnectNode);
		TipDotSystem.OnTipDotSystemPreClear += new Action(DisconnectNode);
		ConnectNode();
	}

	private void OnDisable()
	{
		DisconnectNode();
		IProfessionTipDotSystem tipDotSystem = TipDotSystem;
		tipDotSystem.OnTipDotShowChanged = (Action)Delegate.Remove(tipDotSystem.OnTipDotShowChanged, new Action(OnSetShow));
		TipDotSystem.OnTipDotSystemInited -= new Action(ConnectNode);
		TipDotSystem.OnTipDotSystemPreClear -= new Action(DisconnectNode);
		mIsEnabled = false;
	}

	private void ConnectNode()
	{
		LinkNodeStateChanged();
	}

	private void DisconnectNode()
	{
		UnlinkNodeStateChanged();
	}

	private TipDotNodeKey GetWaitLinkNodeKey()
	{
		if (!mWaitLinkNodeKey.IsValid())
		{
			TipDotNodeKey tipDotNodeKey = mGraphNodeSelector.GetTipDotNodeKey();
			if (tipDotNodeKey.IsValid())
			{
				TipDotGraphType? tipDotInfo = TipDotSystem.GetGraphType(tipDotNodeKey.GraphName);
				if (tipDotInfo == null)
				{
					UnityEngine.Debug.LogError("Original profession tip-dot graph configuration is missing.");
				}
				else if (tipDotInfo.Value == TipDotGraphType.Static)
				{
					return mGraphNodeSelector.GetTipDotNodeKey();
				}
			}
		}
		return mWaitLinkNodeKey;
	}

	private void OnSetShow()
	{
		if ((Object)(object)mTipDotContent == (Object)null)
		{
			UnityEngine.Debug.LogWarning("Original profession tip-dot content binding is missing.");
		}
		else
		{
			mTipDotContent.SetActive(TipDotSystem.IsShowTipDot);
		}
	}

	public void SetNodeKey(TipDotNodeKey nodeKey)
	{
		if ((!TempBeLimitShow || !TipDotSystem.TempLimitShowTip) && nodeKey.IsValid())
		{
			mWaitLinkNodeKey = nodeKey;
			if (mIsEnabled)
			{
				LinkNodeStateChanged();
			}
		}
	}

	public void SetNodeInstanceInfo(INodeInstanceInfo info)
	{
		if (TempBeLimitShow && TipDotSystem.TempLimitShowTip)
		{
			return;
		}
		TipDotNodeKey tipDotNodeKey = mGraphNodeSelector.GetTipDotNodeKey();
		if (tipDotNodeKey.IsValid())
		{
			mWaitLinkNodeKey = tipDotNodeKey.SetInstanceInfo(info);
			if (mIsEnabled)
			{
				LinkNodeStateChanged();
			}
		}
	}

	private void OnNodeStateChanged(TipDotStateInfo info)
	{
		mStateInfo = info;
		OnStateChanged?.Invoke(info);
		mTipLevelState.SetState(info.TipLevel);
		((Component)mNodeDataText).gameObject.SetActive(IsShowTip && mNeedShowData && !string.IsNullOrEmpty(info.Data));
	}

	private void LinkNodeStateChanged()
	{
		if (TempBeLimitShow && TipDotSystem.TempLimitShowTip)
		{
			return;
		}
		TipDotNodeKey waitLinkNodeKey = GetWaitLinkNodeKey();
		if (waitLinkNodeKey.IsValid() && !(mLinkedNodeKey == waitLinkNodeKey))
		{
			if (mLinkedNodeKey.IsValid())
			{
				UnlinkNodeStateChanged();
			}
			if (Controller.LinkNodeStateChange(waitLinkNodeKey, null, OnNodeStateChanged))
			{
				mLinkedNodeKey = waitLinkNodeKey;
			}
		}
	}

	private void UnlinkNodeStateChanged()
	{
		if ((!TempBeLimitShow || !TipDotSystem.TempLimitShowTip) && mLinkedNodeKey.IsValid())
		{
			TipDotNodeKey key = mLinkedNodeKey;
			if (Controller.UnlinkNodeStateChange(key, null, OnNodeStateChanged))
			{
				mLinkedNodeKey = default;
			}
		}
	}
}
}
