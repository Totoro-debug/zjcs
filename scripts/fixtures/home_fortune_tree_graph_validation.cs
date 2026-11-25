using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

public static class HomeFortuneTreeGraphValidation
{
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    public static void Run()
    {
        try
        {
            const string path = "Assets/Official/Home/FruitTree/Native/Graph/FortuneTree.asset";
            var graph = AssetDatabase.LoadAssetAtPath<TipDotNodeGraph>(path);
            Require(graph != null, "The original FortuneTree graph asset must import.");
            var nodes = graph.nodes.Cast<TipDotGraphNode>().ToArray();
            Require(nodes.Length == 24, "The original FortuneTree graph must retain all 24 nodes.");
            Require(nodes.All(node => node != null && node.TipDotNode != null), "Every original graph node must retain its serialized tip node.");
            Require(nodes.Any(node => node.NodeName == "FruitTreeCanUnlock" && node.TipDotNode is TipDotCommonNode), "Fruit-tree unlock node must retain its original key and node type.");
            Require(nodes.Any(node => node.NodeName == "PetTreeCanUnlock" && node.TipDotNode is TipDotCommonNode), "Pet-tree unlock node must retain its original key and node type.");
            Require(nodes.Any(node => node.NodeName == "PetGachaTabHasTip" && node.TipDotNode is TipDotCommonNode), "Pet-gacha indicator node must retain its original key and node type.");
            Require(nodes.All(node => node.graph == graph), "Every original node must point back to FortuneTree.");
            Debug.Log("HOME_FORTUNE_TREE_GRAPH_PASSED"); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
