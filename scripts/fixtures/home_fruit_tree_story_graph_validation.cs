using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.HomeStoryDialog;

public static class HomeFruitTreeStoryGraphValidation
{
    private sealed class Host : HomeFurnitureStoryGraph.IHost
    {
        public readonly List<int> Visited = new List<int>();
        public readonly Queue<int> Gates = new Queue<int>();
        public Action<int?> Pending;
        public void Execute(string story, HomeFurnitureStoryGraph.Node node, Action<int?> completed)
        {
            Visited.Add(node.Id);
            if (Gates.Count > 0 && Gates.Peek() == node.Id)
            {
                Gates.Dequeue();
                Pending = completed;
            }
            else completed(null);
        }
    }

    public static void Run()
    {
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Official/Home/Source/fruit-tree-unlock-stories.json");
        Require(source, "Original source is imported by Unity");
        var host = new Host();
        var graph = new HomeFurnitureStoryGraph(source, host);
        int finished = 0;
        host.Gates.Enqueue(11);
        host.Gates.Enqueue(24);
        graph.Play("FruitTreeUnlock1", () => finished++);
        Require(graph.IsRunning && host.Visited.SequenceEqual(new[] { 1, 6, 7, 12, 10, 17, 18, 8, 19, 11 }),
            "Production graph stops at repair before the subsequent dialogue and collection mask");
        var repair = host.Pending;
        repair(null);
        Require(graph.IsRunning && finished == 0 && host.Visited.Last() == 24,
            "Repair completion reaches the original fruit reward gate");
        repair(null);
        Require(graph.IsRunning && host.Visited.Last() == 24, "Stale repair completion cannot bypass collection");
        var reward = host.Pending;
        reward(null);
        reward(null);
        Require(!graph.IsRunning && finished == 1 && host.Visited.SequenceEqual(
            new[] { 1, 6, 7, 12, 10, 17, 18, 8, 19, 11, 20, 21, 22, 23, 24, 25, 26, 27 }),
            "Reward completion follows mask closure and final dialogue once");
        host.Visited.Clear();
        host.Gates.Enqueue(31);
        graph.Play("FruitTreeUnlock2", () => finished++);
        Require(graph.IsRunning && host.Visited.SequenceEqual(new[] { 1, 21, 25, 22, 24, 23, 26, 39, 31 }),
            "Pet tree waits at its separate official repair node");
        var stale = host.Pending;
        graph.Cancel();
        stale(null);
        Require(!graph.IsRunning && finished == 1, "Cancellation invalidates pending pet repair completion");
        host.Visited.Clear();
        graph.Play("FruitTreeUnlock2", () => finished++);
        Require(!graph.IsRunning && finished == 2 && host.Visited.SequenceEqual(
            new[] { 1, 21, 25, 22, 24, 23, 26, 39, 31, 38, 27 }),
            "Pet graph resumes as a fresh execution and preserves its post-repair dialogue");
        Debug.Log("FRUIT_TREE_STORY_GRAPH_PASSED: original paths, repair/reward waits, duplicate callbacks and cancellation.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
