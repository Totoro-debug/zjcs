// Ported from the verified official source; only dependency/serialization adapters differ.
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
[Serializable]


public class TipDotGraphNode : ProfessionGraphAssetNode
{

	public ProfessionGraphAssetNode Parents;

	public ProfessionGraphAssetNode Childs;

	public bool IsBan;

	public string NodeDesc;

	public string SelectedType;

	public string NodeName;

	public int TipDotNodeId;

	[SerializeReference]
	public TipDotNodeBase TipDotNode;

	[SerializeReference]
	public TipDotNodeBase RuntimeTipDotNode;

	[SerializeField]
	public TipDotNodePresetData PresetData = new TipDotNodePresetData();

	[NonSerialized]
	public bool IsShowInGraph = true;

	[NonSerialized]
	public bool IsChildExpend = true;

	[NonSerialized]
	public bool IsSelfExpend = true;

	[NonSerialized]
	protected List<string> mSelectableNodeType;

	public virtual void InitNode(TipDotNodeBase node)
	{
		node.SetPresetData(PresetData);
	}

	public virtual void InitRelationNode(Dictionary<TipDotGraphNode, TipDotNodeBase> mapperDict)
	{
		if (!mapperDict.TryGetValue(this, out var value))
		{
			UnityEngine.Debug.LogError("Original profession graph binding is invalid.");
			return;
		}
		value.EnsureParents();
		value.EnsureChilds();
		value.Parents.Clear();
		value.Childs.Clear();
		foreach (ProfessionGraphPort connection in GetPort("Parents").GetConnections())
		{
			TipDotGraphNode tipDotGraphNode = connection.node as TipDotGraphNode;
			if (!tipDotGraphNode.IsBan)
			{
				value.Parents.Add(mapperDict[tipDotGraphNode].NodeKey);
			}
		}
		foreach (ProfessionGraphPort connection2 in GetPort("Childs").GetConnections())
		{
			TipDotGraphNode tipDotGraphNode2 = connection2.node as TipDotGraphNode;
			if (!tipDotGraphNode2.IsBan)
			{
				value.Childs.Add(mapperDict[tipDotGraphNode2].NodeKey);
			}
		}
	}

	public virtual List<string> GetSelecableType()
	{
		if (mSelectableNodeType != null)
		{
			return mSelectableNodeType;
		}
		mSelectableNodeType = new List<string> { "TipDotCommonNode" };
		return mSelectableNodeType;
	}

	public bool IsConditionNode()
	{
		if (TipDotNode != null)
		{
			return TipDotNode is TipDotConditionNodeBase;
		}
		return false;
	}

	public TipDotNodeKey GetNodeKey()
	{
		if (TipDotNode == null)
		{
			UnityEngine.Debug.LogError("Original profession graph binding is invalid.");
			return default;
		}
		string nodeName = (PresetData.IsRoot ? "0" : TipDotNodeId.ToString());
		return new TipDotNodeKey(((Object)graph).name, nodeName, BaseNodeInstanceInfo.kDefault);
	}

	public override object GetValue(ProfessionGraphPort port)
	{
		return null;
	}
}
}
