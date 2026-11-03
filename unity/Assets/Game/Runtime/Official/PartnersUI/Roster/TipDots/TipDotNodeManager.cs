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
public class TipDotNodeManager : IPartnerDynamicTipDotNodeManager, IDisposable
{

	private IDisposable mSubscriberDisposable;

	private Dictionary<string, List<INodeInstanceInfo>> mExistGraphInstanceInfoDict = new Dictionary<string, List<INodeInstanceInfo>>();

	private static readonly List<INodeInstanceInfo> sEmptyInstanceInfoList = new List<INodeInstanceInfo>();

	private readonly HashSet<TipDotNodeBase> mCycleVisited = new HashSet<TipDotNodeBase>();

	private readonly Queue<TipDotNodeBase> mCycleQueue = new Queue<TipDotNodeBase>();

	private HashSet<TipDotConditionNodeBase> mDirtyConditionNodeSet = new HashSet<TipDotConditionNodeBase>();

	private List<TipDotConditionNodeBase> mProcessDirtyConditionNodeList = new List<TipDotConditionNodeBase>();

	private PartnerTipDotGraphInfo[] mAllPartnerTipDotGraphInfos;

	private bool mInited;

	private bool mStartConditionNodeUpdate;

	public Action<TipDotNodeKey> OnAddNode { get; set; }

	public Action<TipDotNodeKey> OnReduceNode { get; set; }

	public Action OnAddScopeDataCollector { get; set; }

	public Action OnRemoveScopeDataCollector { get; set; }

	public int TotalNodeCount => Repository.TotalNodeCount;
	private TipDotNodeDataRepository Repository { get; set; }
	private TipDotNodeFactory NodeFactory { get; set; }
	private TipDotGraphHandler GraphHandler { get; set; }
	public object ScopeDataCollector { get; private set; }

	private PartnerTipDotGraphInfo[] AllPartnerTipDotGraphInfos
	{
		get
		{

			if (mAllPartnerTipDotGraphInfos == null)
			{
				PartnerTipDotGraphInfo[] all = bindings.GetAllGraphs();
				if (all == null)
				{
					UnityEngine.Debug.LogError("Official tip graph Error");
					return null;
				}
				mAllPartnerTipDotGraphInfos = all;
			}
			return mAllPartnerTipDotGraphInfos;
		}
	}

	public bool Inited => mInited;

	private readonly PartnerTipDotRuntimeBindings bindings;
	private bool disposed;
	public TipDotNodeManager(TipDotNodeDataRepository repository, TipDotNodeFactory factory,
        TipDotGraphHandler graphs, PartnerTipDotRuntimeBindings bindings)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        NodeFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        GraphHandler = graphs ?? throw new ArgumentNullException(nameof(graphs));
        this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        PeriodicConditionNodeUpdater().Forget();
    }
    public void Dispose() { Release(); disposed = true; }

	public void Init()
	{

		if (!mInited)
		{
			Repository.Init();
			mStartConditionNodeUpdate = true;
			mInited = true;
			mSubscriberDisposable = bindings.SubscribeScope(InitScopeData, DisposeScopeData);
		}
	}

	public void Release()
	{
		if (mInited)
		{
			Repository.Clear();
			mExistGraphInstanceInfoDict.Clear();
			mDirtyConditionNodeSet.Clear();
			mProcessDirtyConditionNodeList.Clear();
			mStartConditionNodeUpdate = false;
			mInited = false;
			mSubscriberDisposable.Dispose();
		}
	}

	private void InitScopeData(object collector)
	{
		ScopeDataCollector = collector;
		OnAddScopeDataCollector?.Invoke();
	}

	private void DisposeScopeData()
	{
		OnRemoveScopeDataCollector?.Invoke();
		ScopeDataCollector = null;
	}

	private async UniTaskVoid PeriodicConditionNodeUpdater()
    {
        // Restored from the game's async state machine. Dispose ends only the
        // owner lifetime; Init/Release retain the original worker semantics.
        while (!disposed)
        {
            if (bindings.HasPlayer() && mStartConditionNodeUpdate && mDirtyConditionNodeSet.Count > 0)
            {
                mProcessDirtyConditionNodeList.Clear();
                mProcessDirtyConditionNodeList.AddRange(mDirtyConditionNodeSet);
                mDirtyConditionNodeSet.Clear();
                for (int i = 0; i < mProcessDirtyConditionNodeList.Count; i++)
                {
                    try { mProcessDirtyConditionNodeList[i]?.UpdateNode(); }
                    catch (Exception error) { UnityEngine.Debug.LogException(error); }
                }
                mProcessDirtyConditionNodeList.Clear();
            }
            await UniTask.DelayFrame(1, (PlayerLoopTiming)8, default(System.Threading.CancellationToken), false);
        }
    }

	public List<TipDotNodeBase> GetAllDataNodes()
	{
		return Repository.GetAllDataNodes();
	}

	public void AddNodes(List<TipDotNodeBase> nodes)
	{
		if (nodes == null || nodes.Count <= 0)
		{
			return;
		}
		foreach (TipDotNodeBase node in nodes)
		{
			Repository.AddNodeData(node);
		}
		foreach (TipDotNodeBase node2 in nodes)
		{
			node2.InitNode();
		}
	}

	public void RemoveNodes(List<TipDotNodeKey> keys)
	{

		if (keys == null || keys.Count <= 0)
		{
			return;
		}

		foreach (TipDotNodeKey key in keys)
		{

			TipDotNodeBase nodeData = Repository.GetNodeData(key);
			if (nodeData != null)
			{
				nodeData.DestroyNode();
				Repository.RemoveNodeData(key);
				OnReduceNode?.Invoke(key);
			}
		}
	}

	private void ConnectNode(TipDotNodeBase parentNode, TipDotNodeBase childNode)
	{
		if (parentNode != null && childNode != null)
		{
			parentNode.AddChild(childNode);
			childNode.AddParent(parentNode);
		}
	}

	private void DisConnectNode(TipDotNodeBase parentNode, TipDotNodeBase childNode)
	{
		if (parentNode != null && childNode != null)
		{
			parentNode.RemoveChild(childNode);
			childNode.RemoveParent(parentNode);
		}
	}

	public TipDotNodeBase FindNodeOrCreate(TipDotNodeKey nodeKey)
	{

		TipDotNodeBase nodeData = Repository.GetNodeData(nodeKey);
		if (nodeData == null)
		{
			CreateGraphNode(nodeKey.GraphName, nodeKey.InstanceInfo);
			nodeData = Repository.GetNodeData(nodeKey);
			if (nodeData == null)
			{
				UnityEngine.Debug.LogError("Official tip graph Error");
			}
		}
		return nodeData;
	}

	public TipDotNodeBase FindNode(TipDotNodeKey nodeKey)
	{
		return Repository.GetNodeData(nodeKey);
	}

	private bool WillCreateCycle(TipDotNodeBase from, TipDotNodeBase to)
	{
		if (from == to)
		{
			return true;
		}
		mCycleVisited.Clear();
		mCycleQueue.Clear();
		mCycleQueue.Enqueue(to);
		while (mCycleQueue.Count > 0)
		{
			TipDotNodeBase tipDotNodeBase = mCycleQueue.Dequeue();
			if (!mCycleVisited.Add(tipDotNodeBase))
			{
				continue;
			}
			List<TipDotNodeKey> parents = tipDotNodeBase.Parents;
			if (parents == null)
			{
				continue;
			}
			foreach (TipDotNodeKey item in parents)
			{
				TipDotNodeBase tipDotNodeBase2 = FindNodeOrCreate(item);
				if (tipDotNodeBase2 == from)
				{
					return true;
				}
				mCycleQueue.Enqueue(tipDotNodeBase2);
			}
		}
		return false;
	}

	public void ReconnectNodes(TipDotNodeKey parentNodeKey, List<TipDotNodeKey> childNodeKeys)
	{

		TipDotNodeBase tipDotNodeBase = FindNodeOrCreate(parentNodeKey);
		List<TipDotNodeKey> childs = tipDotNodeBase.Childs;
		if (childs != null)
		{
			for (int num = childs.Count - 1; num >= 0; num--)
			{
				TipDotNodeKey nodeKey = childs[num];
				TipDotNodeBase childNode = FindNodeOrCreate(nodeKey);
				DisConnectNode(tipDotNodeBase, childNode);
			}
		}

		for (int num2 = childNodeKeys.Count - 1; num2 >= 0; num2--)
		{
			TipDotNodeKey tipDotNodeKey = childNodeKeys[num2];
			TipDotNodeBase tipDotNodeBase2 = FindNodeOrCreate(tipDotNodeKey);
			ConnectNode(tipDotNodeBase, tipDotNodeBase2);
			if (WillCreateCycle(tipDotNodeBase2, tipDotNodeBase))
			{
				UnityEngine.Debug.LogWarning("Official tip graph Warn");
				DisConnectNode(tipDotNodeBase, tipDotNodeBase2);
				return;
			}
		}
		tipDotNodeBase.UpdateNode();
	}

	public void DisconnectNodes(TipDotNodeKey parentNodeKey, List<TipDotNodeKey> childNodeKeys)
	{
		TipDotNodeBase tipDotNodeBase = FindNodeOrCreate(parentNodeKey);
		foreach (TipDotNodeKey childNodeKey in childNodeKeys)
		{
			if (tipDotNodeBase.IncludeChildNode(childNodeKey))
			{
				TipDotNodeBase childNode = FindNodeOrCreate(childNodeKey);
				DisConnectNode(tipDotNodeBase, childNode);
			}
		}
		tipDotNodeBase.UpdateNode();
	}

	public void DisconnectNode(TipDotNodeKey parentNodeKey, TipDotNodeKey childNodeKey)
	{
		TipDotNodeBase tipDotNodeBase = FindNodeOrCreate(parentNodeKey);
		if (tipDotNodeBase.IncludeChildNode(childNodeKey))
		{
			TipDotNodeBase childNode = FindNodeOrCreate(childNodeKey);
			DisConnectNode(tipDotNodeBase, childNode);
			tipDotNodeBase.UpdateNode();
		}
	}

	public void ConnectNodes(TipDotNodeKey parentNodeKey, List<TipDotNodeKey> childNodeKeys)
	{

		TipDotNodeBase tipDotNodeBase = FindNodeOrCreate(parentNodeKey);

		for (int num = childNodeKeys.Count - 1; num >= 0; num--)
		{
			TipDotNodeKey tipDotNodeKey = childNodeKeys[num];
			if (!tipDotNodeBase.IncludeChildNode(tipDotNodeKey))
			{
				TipDotNodeBase tipDotNodeBase2 = FindNodeOrCreate(tipDotNodeKey);
				ConnectNode(tipDotNodeBase, tipDotNodeBase2);
				if (WillCreateCycle(tipDotNodeBase2, tipDotNodeBase))
				{
					UnityEngine.Debug.LogWarning("Official tip graph Warn");
					DisConnectNode(tipDotNodeBase, tipDotNodeBase2);
					return;
				}
			}
		}
		tipDotNodeBase.UpdateNode();
	}

	public void ConnectNode(TipDotNodeKey parentNodeKey, TipDotNodeKey childNodeKey)
	{
		ConnectNodes(parentNodeKey, new List<TipDotNodeKey> { childNodeKey });
	}

	public List<INodeInstanceInfo> GetExistGraphInstanceInfo(string graphName)
	{
		if (!mExistGraphInstanceInfoDict.TryGetValue(graphName, out var value))
		{
			return sEmptyInstanceInfoList;
		}
		return value;
	}

	private bool HasGraphInstanceInfo(string graphName, INodeInstanceInfo info = null)
	{
		if (mExistGraphInstanceInfoDict.TryGetValue(graphName, out var value))
		{
			return value.Find((INodeInstanceInfo item) => item.Equals(info)) != null;
		}
		return false;
	}

	public int CreateGraphNode(string graphName, INodeInstanceInfo info)
	{

		if (!bindings.HasPlayer())
		{
			UnityEngine.Debug.LogError("Official tip graph Error");
			return 0;
		}
		if (info == null)
		{
			info = BaseNodeInstanceInfo.kDefault;
		}
		if (HasGraphInstanceInfo(graphName, info))
		{
			UnityEngine.Debug.LogWarning("Official tip graph Warn");
			return 0;
		}
		List<TipDotNodeBase> list = NodeFactory.ProductNodes(graphName, info);
		if (list == null)
		{
			return 0;
		}
		AddNodes(list);
		if (!mExistGraphInstanceInfoDict.TryGetValue(graphName, out var value))
		{
			value = new List<INodeInstanceInfo>();
			mExistGraphInstanceInfoDict[graphName] = value;
		}
		value.Add(info);
		return list.Count;
	}

	public int DestoryGraphNode(string graphName, INodeInstanceInfo info)
	{
		if (!HasGraphInstanceInfo(graphName, info))
		{
			return 0;
		}
		if (info == null)
		{
			info = BaseNodeInstanceInfo.kDefault;
		}
		int result = Repository.RemoveNodeDatas(graphName, info);
		mExistGraphInstanceInfoDict[graphName].Remove(info);
		return result;
	}

	public List<string> CreateStaticGraphNodeWithScope(TipDotGraphScope scope)
	{
		BaseNodeInstanceInfo kDefault = BaseNodeInstanceInfo.kDefault;
		List<string> list = new List<string>();
		TipDotNodeGraph tipDotNodeGraph = ScriptableObject.CreateInstance<TipDotNodeGraph>();
		PartnerTipDotGraphInfo[] allPartnerTipDotGraphInfos = AllPartnerTipDotGraphInfos;
		foreach (PartnerTipDotGraphInfo tipDotInfo in allPartnerTipDotGraphInfos)
		{
			if (tipDotInfo.GraphType != TipDotGraphType.Static || tipDotInfo.GraphScope != scope)
			{
				continue;
			}
			TipDotNodeGraph nodeGraph = GraphHandler.GetNodeGraph(tipDotInfo.GraphName);
			if (!((Object)(object)nodeGraph == (Object)null))
			{
				tipDotNodeGraph.nodes.AddRange(nodeGraph.nodes);
				list.Add(tipDotInfo.GraphName);
				if (!mExistGraphInstanceInfoDict.TryGetValue(tipDotInfo.GraphName, out var value))
				{
					value = new List<INodeInstanceInfo>();
					mExistGraphInstanceInfoDict[tipDotInfo.GraphName] = value;
				}
				value.Add(kDefault);
			}
		}
		List<TipDotNodeBase> nodes = NodeFactory.ProductNodes(tipDotNodeGraph, kDefault);
		AddNodes(nodes);
		return list;
	}

	public List<string> DestoryStaticGraphNodeWithScope(TipDotGraphScope scope)
	{

		PartnerTipDotGraphInfo[] all = bindings.GetAllGraphs();
		if (all == null)
		{
			UnityEngine.Debug.LogError("Official tip graph Error");
			return null;
		}
		List<string> list = new List<string>();
		BaseNodeInstanceInfo kDefault = BaseNodeInstanceInfo.kDefault;
		PartnerTipDotGraphInfo[] array = all;
		foreach (PartnerTipDotGraphInfo tipDotInfo in array)
		{
			if (tipDotInfo.GraphType == TipDotGraphType.Static && tipDotInfo.GraphScope == scope)
			{
				System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
				DestoryGraphNode(tipDotInfo.GraphName, kDefault);
				list.Add(tipDotInfo.GraphName);
				stopwatch.Stop();
			}
		}
		return list;
	}

	public TipDotNodeBase GetRootNode(string graphName, INodeInstanceInfo instanceInfo)
	{
		TipDotNodeKey nodeKey = new TipDotNodeKey(graphName, "0", instanceInfo);
		return FindNode(nodeKey);
	}

	public void AddDirtyConditionNode(TipDotConditionNodeBase node)
	{
		mDirtyConditionNodeSet.Add(node);
	}

	public void RemoveDirtyConditionNode(TipDotConditionNodeBase node)
	{
		mDirtyConditionNodeSet.Remove(node);
	}
}
}
