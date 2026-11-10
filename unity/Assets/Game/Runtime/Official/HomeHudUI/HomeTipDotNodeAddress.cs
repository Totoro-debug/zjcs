using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHudUI
{
    // The original UI selector reads only graph.name and TipDotNodeId. These
    // addresses keep that binding independent from loading a graph runtime.
    // Complete decoded graph objects remain in sourceGraphData; an address is
    // never a substitute for executing the original condition-node classes.
    public sealed class HomeTipDotNodeAddress : TipDotGraphNode
    {
        public TextAsset sourceGraphData;
        public string originalIdentity;
        public override void InitNode(TipDotNodeBase node)
            => throw new InvalidOperationException("Load the original condition graph, not its UI selector address.");
        public override void InitRelationNode(Dictionary<TipDotGraphNode, TipDotNodeBase> mapperDict)
            => throw new InvalidOperationException("UI selector addresses cannot evaluate condition graphs.");
    }
}
