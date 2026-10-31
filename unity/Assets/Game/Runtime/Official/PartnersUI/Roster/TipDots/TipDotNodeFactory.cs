// Ported from active official client; see partner-tipdot-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.Partners;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public class TipDotNodeFactory
{
    private readonly Func<string, TipDotNodeGraph> graphLookup;
    private readonly Action<TipDotNodeBase> injectNode;
    public TipDotNodeFactory(Func<string, TipDotNodeGraph> graphLookup, Action<TipDotNodeBase> injectNode)
    {
        this.graphLookup = graphLookup ?? throw new ArgumentNullException(nameof(graphLookup));
        this.injectNode = injectNode ?? throw new ArgumentNullException(nameof(injectNode));
    }

	public List<TipDotNodeBase> ProductNodes(string graphName, INodeInstanceInfo instanceInfo)
    {
        TipDotNodeGraph nodeGraph = graphLookup(graphName);
        if (nodeGraph == null)
        {
            UnityEngine.Debug.LogError("Original tip-dot graph is not registered: " + graphName);
            return null;
        }
        return ProductNodes(nodeGraph, instanceInfo);
    }

	public List<TipDotNodeBase> ProductNodes(TipDotNodeGraph nodeGraph, INodeInstanceInfo instanceInfo)
	{
		IEnumerable<TipDotGraphNode> enumerable = nodeGraph.nodes.Select((ProfessionGraphAssetNode item) => item as TipDotGraphNode);
		Dictionary<TipDotGraphNode, TipDotNodeBase> dictionary = new Dictionary<TipDotGraphNode, TipDotNodeBase>();
		if (instanceInfo == null)
		{
			instanceInfo = BaseNodeInstanceInfo.kDefault;
		}
		foreach (TipDotGraphNode item in enumerable)
		{
			if (item.IsBan)
			{
				continue;
			}
			string nodeName = (item.PresetData.IsRoot ? "0" : item.TipDotNodeId.ToString());
			TipDotNodeKey tipDotNodeKey = new TipDotNodeKey(((Object)item.graph).name, nodeName, instanceInfo);
			TipDotNodeBase tipDotNodeBase = item.TipDotNode.Clone();
			if (tipDotNodeBase == null)
			{
				UnityEngine.Debug.LogError("Clone Node Failed! nodeKey: " + tipDotNodeKey);
			}
			else
			{
				tipDotNodeBase.SetNodeKey(tipDotNodeKey);
				item.InitNode(tipDotNodeBase);
				dictionary.Add(item, tipDotNodeBase);
			}
		}
		TipDotGraphNode tipDotGraphNode = default;
		TipDotNodeBase tipDotNodeBase2 = default;
		foreach (KeyValuePair<TipDotGraphNode, TipDotNodeBase> item2 in dictionary)
		{
			tipDotGraphNode = item2.Key;
			tipDotNodeBase2 = item2.Value;
			TipDotGraphNode tipDotGraphNode2 = tipDotGraphNode;
			TipDotNodeBase tipDotNodeBase3 = tipDotNodeBase2;
			tipDotGraphNode2.InitRelationNode(dictionary);
			injectNode(tipDotNodeBase3);
		}
		return dictionary.Values.ToList();
	}
}

}
