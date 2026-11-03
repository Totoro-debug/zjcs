// Selected official game implementation; see partner-tipdot-runtime-source-manifest.json.
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.PartnersUI
{
public class TipDotNodeDataRepository
{
	private Dictionary<TipDotNodeKey, TipDotNodeBase> mDataDict;

	private readonly List<TipDotNodeKey> mKeysToRemoveBuffer = new List<TipDotNodeKey>();

	public int TotalNodeCount => mDataDict.Count;

	public void Init()
	{
		if (mDataDict == null)
		{
			mDataDict = new Dictionary<TipDotNodeKey, TipDotNodeBase>();
		}
	}

	public void Clear()
	{
		if (mDataDict == null)
		{
			return;
		}
		foreach (TipDotNodeBase value in mDataDict.Values)
		{
			value.DestroyNode();
		}
		mDataDict.Clear();
	}

	public List<TipDotNodeKey> GetNodeAllParents(TipDotNodeKey nodeKey)
	{
		TipDotNodeBase nodeData = GetNodeData(nodeKey);
		if (nodeData == null)
		{
			return null;
		}
		List<TipDotNodeKey> list = new List<TipDotNodeKey>();
		HashSet<TipDotNodeBase> hashSet = new HashSet<TipDotNodeBase>();
		Stack<TipDotNodeBase> stack = new Stack<TipDotNodeBase>();
		if (!hashSet.Contains(nodeData))
		{
			TopologySort(nodeData, hashSet, stack, new HashSet<TipDotNodeBase>());
		}
		while (stack.Count > 0)
		{
			TipDotNodeBase tipDotNodeBase = stack.Pop();
			list.Add(tipDotNodeBase.NodeKey);
		}
		return list;
	}

	public TipDotNodeBase GetNodeData(TipDotNodeKey nodeKey)
	{
		if (nodeKey.InstanceInfo == null)
		{
			nodeKey = nodeKey.SetInstanceInfo(BaseNodeInstanceInfo.kDefault);
		}
		mDataDict.TryGetValue(nodeKey, out var value);
		return value;
	}

	public void AddNodeData(TipDotNodeBase node)
	{
		if (!mDataDict.TryAdd(node.NodeKey, node))
		{
			UnityEngine.Debug.LogWarning("Official tip graph Warn");
		}
		else
		{
			mDataDict[node.NodeKey] = node;
		}
	}

	public void RemoveNodeData(TipDotNodeKey nodeKey)
	{
		if (mDataDict.TryGetValue(nodeKey, out var value))
		{
			value.DestroyNode();
			mDataDict.Remove(nodeKey);
		}
	}

	private void TopologySort(TipDotNodeBase node, HashSet<TipDotNodeBase> visited, Stack<TipDotNodeBase> stack, HashSet<TipDotNodeBase> recursionStack)
	{

		if (recursionStack.Contains(node))
		{
			UnityEngine.Debug.LogError("Official tip graph Error");
		}
		else
		{
			if (visited.Contains(node))
			{
				return;
			}
			recursionStack.Add(node);
			List<TipDotNodeKey> parents = node.Parents;
			if (parents != null)
			{
				foreach (TipDotNodeKey item in parents)
				{
					TipDotNodeBase nodeData = GetNodeData(item);
					if (nodeData == null)
					{
						UnityEngine.Debug.LogWarning("Official tip graph Warn");
					}
					else
					{
						TopologySort(nodeData, visited, stack, recursionStack);
					}
				}
			}
			recursionStack.Remove(node);
			visited.Add(node);
			stack.Push(node);
		}
	}

	public int RemoveNodeDatas(string graphName, INodeInstanceInfo instanceInfo)
	{
		mKeysToRemoveBuffer.Clear();
		foreach (TipDotNodeKey key in mDataDict.Keys)
		{
			if (key.GraphName == graphName && object.Equals(key.InstanceInfo, instanceInfo))
			{
				mKeysToRemoveBuffer.Add(key);
			}
		}
		int num = 0;
		foreach (TipDotNodeKey item in mKeysToRemoveBuffer)
		{
			if (mDataDict.TryGetValue(item, out var value))
			{
				value.DestroyNode();
				mDataDict.Remove(item);
				num++;
			}
		}
		return num;
	}

	public List<TipDotNodeBase> GetAllDataNodes()
	{
		return mDataDict.Values.ToList();
	}
}
}
