using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.HomeStoryDialog;

public static class HomeStoryGraphValidation
{
    private sealed class Host : HomeFurnitureStoryGraph.IHost
    {
        public readonly List<int> Visited = new List<int>();
        public int Choice, PauseAt;
        public Action<int?> Pending;
        public HomeFurnitureStoryGraph.Node PausedNode;
        public void Execute(string story, HomeFurnitureStoryGraph.Node node, Action<int?> completed)
        {
            Visited.Add(node.Id);
            if (node.Id == PauseAt) { Pending = completed; PausedNode = node; return; }
            completed(node.Type == "OptionDialogNode" ? (node.Id == 49 ? Choice : 0) : (int?)null);
        }
    }

    public static HomeFurnitureStoryGraph.Node StoreOptionNode(TextAsset source)
    {
        var host = new Host { PauseAt = 49 };
        var graph = new HomeFurnitureStoryGraph(source, host);
        graph.Play("SenSenHomeStore", null);
        Require(host.PausedNode != null, "The original store option node is available.");
        graph.Cancel();
        return host.PausedNode;
    }

    public static void Check(TextAsset source)
    {
        var host = new Host { PauseAt = 23 };
        var graph = new HomeFurnitureStoryGraph(source, host);
        int finishes = 0;
        graph.Play("UnlockCommunication", () => finishes++);
        Require(graph.IsRunning && graph.ActiveStory == "UnlockCommunication" && finishes == 0
            && host.Visited.SequenceEqual(new[] { 1, 45, 44, 17, 46, 18, 23 }), "Dining story waits for actual repair.");
        Action<int?> repair = host.Pending;
        repair(null);
        repair(null);
        Require(!graph.IsRunning && finishes == 1
            && host.Visited.SequenceEqual(new[] { 1, 45, 44, 17, 46, 18, 23, 24, 25 }), "Dining story advances once after repair.");
        for (int choice = 0; choice <= 1; choice++)
        {
            host.Visited.Clear(); host.PauseAt = 49; host.Choice = choice; host.Pending = null;
            graph.Play("SenSenHomeStore", () => finishes++);
            Require(graph.IsRunning && host.Visited.SequenceEqual(new[] { 1, 36, 37, 48, 49 }), "Store story waits for an option.");
            host.Pending(choice);
            Require(!graph.IsRunning && host.Visited.SequenceEqual(new[] { 1, 36, 37, 48, 49,
                choice == 0 ? 50 : 51, 53, 54 }), "Store story follows the selected original connection.");
        }
        host.Visited.Clear(); host.PauseAt = 49; host.Pending = null;
        graph.Play("SenSenHomeStore", () => finishes++);
        Action<int?> stale = host.Pending;
        graph.Cancel(); stale(0);
        Require(!graph.IsRunning && finishes == 3 && host.Visited.SequenceEqual(new[] { 1, 36, 37, 48, 49 }),
            "Cancellation invalidates the pending option callback.");
        Debug.Log("HOME_STORY_GRAPH_PASSED: original graph branches, repair wait and cancellation.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
