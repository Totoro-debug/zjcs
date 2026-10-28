// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]
public abstract class TipDotNodeBase
{

	public SafeAction<TipDotStateInfo> OnStateChanged = new SafeAction<TipDotStateInfo>();

	public SafeAction OnActiveChanged = new SafeAction();

	public List<TipDotNodeKey> Parents;

	public List<TipDotNodeKey> Childs;


	public TipDotNodeKey NodeKey;


	public TipDotStateInfo TipDotInfo;


	public bool IsShowTip;

	[NonSerialized]

	public TipDotNodePresetData PresetData;

	[NonSerialized]

	public int UiLinkCount;

	protected bool mIsActive;


	public ITipDotNodeManager NodeManager { get; set; }


	public ITipDotBanManager TipDotBanManager { get; set; }

	public virtual bool IsActive
	{
		get
		{
			return mIsActive;
		}
		set
		{
			if (mIsActive != value)
			{
				mIsActive = value;
				OnActiveChanged?.Invoke();
			}
		}
	}

	public int Priority
	{
		get
		{
			if (!(this is TipDotConditionNodeBase))
			{
				return PresetData.Priority;
			}
			return -99;
		}
	}

	public ETipDotLevelType PresetTipLevel => PresetData.PresetTipLevel;

	public bool NeedAllShow => PresetData.NeedAllShow;

	public bool IsRoot => PresetData.IsRoot;

	public virtual void Init()
	{
	}

	public virtual void Release()
	{
	}

	public virtual TipDotNodeBase Clone()
	{
		return null;
	}

	public void SetNodeKey(TipDotNodeKey nodeKey)
	{
		NodeKey = nodeKey;
	}

	public void SetPresetData(TipDotNodePresetData presetData)
	{
		PresetData = presetData;
	}

	protected virtual void AddScopeDataCollector()
	{
	}

	protected virtual void RemoveScopeDataCollector()
	{
	}

	public void EnsureParents()
	{
		if (Parents == null)
		{
			Parents = new List<TipDotNodeKey>();
		}
	}

	public void EnsureChilds()
	{
		if (Childs == null)
		{
			Childs = new List<TipDotNodeKey>();
		}
	}

	public void InitNode()
	{
		try
		{
			Init();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		List<TipDotNodeKey> childs = Childs;
		if (childs != null)
		{
			foreach (TipDotNodeKey item in childs)
			{
				TipDotNodeBase tipDotNodeBase = NodeManager.FindNodeOrCreate(item);
				if (tipDotNodeBase != null)
				{
					List<TipDotNodeKey> parents = tipDotNodeBase.Parents;
					if (parents == null || !parents.Contains(NodeKey))
					{
						tipDotNodeBase.AddParent(this);
					}
					tipDotNodeBase.OnStateChanged += new Action<TipDotStateInfo>(OnChildStateChanged);
				}
			}
		}
		List<TipDotNodeKey> parents2 = Parents;
		if (parents2 != null)
		{
			foreach (TipDotNodeKey item2 in parents2)
			{
				TipDotNodeBase tipDotNodeBase2 = NodeManager.FindNodeOrCreate(item2);
				if (tipDotNodeBase2 != null)
				{
					List<TipDotNodeKey> childs2 = tipDotNodeBase2.Childs;
					if (childs2 == null || !childs2.Contains(NodeKey))
					{
						tipDotNodeBase2.AddChild(this);
					}
					tipDotNodeBase2.OnActiveChanged += new Action(OnParentActiveChanged);
				}
			}
		}
		OnParentActiveChanged();
		UpdateNode();
	}

	public void ClearNode()
	{
		if (Childs != null)
		{
			foreach (TipDotNodeKey child in Childs)
			{
				TipDotNodeBase tipDotNodeBase = NodeManager.FindNode(child);
				if (tipDotNodeBase != null)
				{
					tipDotNodeBase.OnStateChanged -= new Action<TipDotStateInfo>(OnChildStateChanged);
					tipDotNodeBase.RemoveParent(this);
				}
			}
		}
		if (Parents != null)
		{
			foreach (TipDotNodeKey parent in Parents)
			{
				TipDotNodeBase tipDotNodeBase2 = NodeManager.FindNode(parent);
				if (tipDotNodeBase2 != null)
				{
					tipDotNodeBase2.OnActiveChanged -= new Action(OnParentActiveChanged);
					tipDotNodeBase2.RemoveChild(this);
				}
			}
		}
		Parents?.Clear();
		Childs?.Clear();
		Parents = null;
		Childs = null;
	}

	public void DestroyNode()
	{
		try
		{
			Release();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		ClearNode();
	}

	public virtual void UpdateNode()
	{
		ETipDotLevelType eTipDotLevelType = ETipDotLevelType.None;
		int num = -999;
		string text = "";
		if (Childs != null)
		{
			foreach (TipDotNodeKey child in Childs)
			{
				TipDotNodeBase tipDotNodeBase = NodeManager.FindNodeOrCreate(child);
				if (tipDotNodeBase == null)
				{
					return;
				}
				if (NeedAllShow && !tipDotNodeBase.IsShowTip)
				{
					eTipDotLevelType = ETipDotLevelType.None;
					break;
				}
				List<ETipDotLevelType> ignoreLevels = PresetData.IgnoreLevels;
				if ((ignoreLevels == null || !ignoreLevels.Contains(tipDotNodeBase.TipDotInfo.TipLevel)) && ((tipDotNodeBase.Priority > num && tipDotNodeBase.TipDotInfo.TipLevel > ETipDotLevelType.None) || (tipDotNodeBase.Priority == num && tipDotNodeBase.TipDotInfo.TipLevel > eTipDotLevelType) || (NodeKey == tipDotNodeBase.NodeKey && TipDotInfo.Data != tipDotNodeBase.TipDotInfo.Data)))
				{
					eTipDotLevelType = ((PresetTipLevel == ETipDotLevelType.None) ? tipDotNodeBase.TipDotInfo.TipLevel : PresetTipLevel);
					text = tipDotNodeBase.TipDotInfo.Data;
					num = tipDotNodeBase.Priority;
				}
			}
		}
		if (IsTipDotBan())
		{
			eTipDotLevelType = ETipDotLevelType.None;
			text = "";
		}
		if (eTipDotLevelType != TipDotInfo.TipLevel || TipDotInfo.Data != text)
		{
			TipDotInfo.TipLevel = eTipDotLevelType;
			TipDotInfo.Data = text;
			IsShowTip = TipDotInfo.TipLevel != ETipDotLevelType.None;
			FireStateChanged();
		}
	}

	protected void FireStateChanged()
	{
		OnStateChanged?.Invoke(TipDotInfo);
	}

	private void OnChildStateChanged(TipDotStateInfo info)
	{
		UpdateNode();
	}

	private void OnParentActiveChanged()
	{
		if (UiLinkCount > 0)
		{
			if (!IsActive)
			{
				IsActive = true;
			}
			return;
		}
		try
		{
			List<TipDotNodeKey> parents = Parents;
			if (parents != null)
			{
				foreach (TipDotNodeKey item in parents)
				{
					TipDotNodeBase tipDotNodeBase = NodeManager.FindNodeOrCreate(item);
					if (tipDotNodeBase != null && tipDotNodeBase.IsActive)
					{
						IsActive = true;
						OnActiveChanged?.Invoke();
						return;
					}
				}
			}
			IsActive = false;
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
			IsActive = true;
		}
	}

	private void OnSelfActiveChanged(bool isActive)
	{
		if (UiLinkCount > 0)
		{
			if (!IsActive)
			{
				IsActive = true;
			}
			return;
		}
		if (!isActive)
		{
			List<TipDotNodeKey> parents = Parents;
			if (parents != null)
			{
				foreach (TipDotNodeKey item in parents)
				{
					TipDotNodeBase tipDotNodeBase = NodeManager.FindNodeOrCreate(item);
					if (tipDotNodeBase != null && tipDotNodeBase.IsActive)
					{
						return;
					}
				}
			}
		}
		IsActive = isActive;
	}

	public bool IncludeChildNode(TipDotNodeKey nodeKey)
	{
		if (Childs == null || Childs.Count <= 0)
		{
			return false;
		}
		return Childs.Contains(nodeKey);
	}

	public void AddChild(TipDotNodeBase node)
	{
		if (node != null)
		{
			EnsureChilds();
			Childs.Add(node.NodeKey);
			node.OnStateChanged += new Action<TipDotStateInfo>(OnChildStateChanged);
		}
	}

	public void RemoveChild(TipDotNodeBase node)
	{
		if (node != null && Childs != null)
		{
			Childs.Remove(node.NodeKey);
			node.OnStateChanged -= new Action<TipDotStateInfo>(OnChildStateChanged);
		}
	}

	public void AddParent(TipDotNodeBase node)
	{
		if (node != null)
		{
			EnsureParents();
			Parents.Add(node.NodeKey);
			node.OnActiveChanged += new Action(OnParentActiveChanged);
			OnParentActiveChanged();
		}
	}

	public void RemoveParent(TipDotNodeBase node)
	{
		if (node != null && Parents != null)
		{
			Parents.Remove(node.NodeKey);
			node.OnActiveChanged -= new Action(OnParentActiveChanged);
		}
	}

	public void AddUiStateListener(Action<TipDotStateInfo> onStateChangeCall)
	{
		OnStateChanged += onStateChangeCall;
		UiLinkCount++;
		if (!IsActive && UiLinkCount > 0)
		{
			OnSelfActiveChanged(isActive: true);
		}
	}

	public void RemoveUiStateListener(Action<TipDotStateInfo> onStateChangeCall)
	{
		OnStateChanged -= onStateChangeCall;
		UiLinkCount = Math.Max(0, UiLinkCount - 1);
		if (IsActive && UiLinkCount <= 0)
		{
			OnSelfActiveChanged(isActive: false);
		}
	}

	protected T GetInstanceInfo<T>() where T : INodeInstanceInfo
	{
		INodeInstanceInfo instanceInfo = NodeKey.InstanceInfo;
		if (instanceInfo is T)
		{
			return (T)instanceInfo;
		}
		return default;
	}

	protected bool IsTipDotBan()
	{
		return TipDotBanManager?.IsTipDotBan(NodeKey) ?? false;
	}
}
}
