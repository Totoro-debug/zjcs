using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Official.ProfessionUI;

public static class PartnerTipDotCatalogValidation
{
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Original partner graph catalog: " + message); }
    public static void Check()
    {
        var catalog = OfficialPartnerTipDotAssetCatalog.LoadDefault();
        Require(catalog.Definitions.Count == 376 && catalog.GetAllGraphs().Length == 376,
            "complete selected tip_dot table retained in original order");
        Require(catalog.Definitions.Select(value => value.GraphName).Distinct().Count() == 376,
            "graph lookup identities are unique");
        Require(catalog.Definitions.Where(value => value.Graph).Select(value => value.GraphName).OrderBy(value => value)
            .SequenceEqual(new[] { "DiningTable", "NpcDetailItem", "NpcProfileItem" }), "only actual original partner assets are bound");
        int nodes = 0;
        foreach (string name in new[] { "DiningTable", "NpcDetailItem", "NpcProfileItem" })
        {
            var graph = catalog.LoadGraph(name);
            Require(graph && graph.name == name && graph.nodes.Count > 0, "native graph loaded: " + name);
            var all = graph.nodes.Cast<TipDotGraphNode>().ToList();
            Require(all.All(node => node && node.graph == graph && node.TipDotNode != null), "native graph node and managed-reference bindings: " + name);
            Require(all.All(node => !node.PresetData.IsRoot && node.GetNodeKey() ==
                new TipDotNodeKey(name, node.TipDotNodeId.ToString(), BaseNodeInstanceInfo.kDefault)),
                "original selected graphs use numbered entry nodes without synthesizing a zero root: " + name);
            var identifiers = new HashSet<int>(all.Select(node => node.TipDotNodeId));
            Require(identifiers.Count == all.Count, "original node identifiers are unique: " + name);
            foreach (var node in all)
                foreach (string direction in new[] { "Parents", "Childs" })
                {
                    var port = node.GetPort(direction);
                    Require(port != null, "original serialized graph port: " + name + "/" + direction);
                    foreach (var connection in port.GetConnections())
                        Require(connection != null && all.Contains(connection.node as TipDotGraphNode), "original connection resolves within its graph");
                }
            nodes += all.Count;
        }
        Require(catalog.GetGraphType("DiningTable") == TipDotGraphType.Static &&
            catalog.GetGraphType("NpcDetailItem") == TipDotGraphType.Dynamic &&
            catalog.GetGraphType("NpcProfileItem") == TipDotGraphType.Dynamic &&
            catalog.LoadGraph("NpcDetailItem").Type == TipDotGraphType.Static,
            "runtime configuration is preserved independently of original graph authoring fields");
        Require(catalog.GetGraphType("UnconfiguredValidationGraph") == null &&
            !catalog.LoadGraph("UnconfiguredValidationGraph"), "unknown graph follows original missing-configuration boundary");
        bool missingBinding = false;
        try { catalog.LoadGraph("Home"); }
        catch (InvalidOperationException error) { missingBinding = error.Message.Contains("Home/Home"); }
        Require(missingBinding, "configured graph outside this module fails explicitly when unbound");
        Debug.Log("Original partner tip graph catalog checks passed: 376 rows, 3 graphs, " + nodes + " native nodes");
    }
}
