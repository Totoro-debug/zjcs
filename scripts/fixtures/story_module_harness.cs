using System;
using System.Collections.Generic;
using System.IO;
using Zjcs.Offline.Story;

static class StoryModuleHarness
{
    sealed class Graph
    {
        public int Entry = 1;
        public readonly Dictionary<int, Func<Action<int>, StoryTransition<Graph>>> Nodes =
            new Dictionary<int, Func<Action<int>, StoryTransition<Graph>>>();
    }
    static void Require(bool value, string message)
    { if (!value) throw new Exception(message); }
    static StoryModule<Graph, Func<Action<int>, StoryTransition<Graph>>> Create(int limit = 256) =>
        new StoryModule<Graph, Func<Action<int>, StoryTransition<Graph>>>(g => g.Entry,
            (g, id) => g.Nodes.TryGetValue(id, out var n) ? n : null, (g, n, resume) => n(resume), limit);
    static void Main()
    {
        var module = Create();
        var root = new Graph();
        var child = new Graph();
        Action<int> animation = null, battle = null;
        int unlocks = 0, completions = 0;
        var order = new List<string>();
        root.Nodes[1] = resume => { order.Add("animation"); animation = resume; return StoryTransition<Graph>.Wait; };
        root.Nodes[2] = resume => StoryTransition<Graph>.Call(child, 3);
        root.Nodes[3] = resume => { order.Add("unlock"); unlocks++; return StoryTransition<Graph>.Next(4); };
        root.Nodes[4] = resume => { order.Add("guide"); resume(0); resume(3); return StoryTransition<Graph>.Wait; };
        child.Nodes[1] = resume => { order.Add("battle"); battle = resume; return StoryTransition<Graph>.Wait; };
        module.Play(root, () => completions++);
        Require(module.IsRunning && unlocks == 0, "animation must block unlock");
        animation(2);
        Require(ReferenceEquals(module.ActiveGraph, child), "child is active during battle");
        animation(3);
        Require(unlocks == 0, "duplicate animation callback must not bypass battle");
        battle(0); // An unconnected child exit returns to its parent.
        Require(!module.IsRunning && completions == 1 && unlocks == 1, "root completes once after guide");
        Require(string.Join(",", order) == "animation,battle,unlock,guide", "official action ordering");
        battle(3);
        Require(completions == 1 && unlocks == 1, "completed callbacks are inert");

        module.Play(root, () => completions++);
        var cancelled = animation;
        module.Cancel();
        module.Play(root, () => completions++);
        cancelled(3);
        Require(unlocks == 1 && module.IsRunning, "cancelled callback cannot advance a new run of the same graph");
        bool busy = false;
        try { module.Play(child, null); } catch (InvalidOperationException) { busy = true; }
        Require(busy, "concurrent play rejected without replacing current story");
        module.Cancel();

        var recursive = new Graph();
        recursive.Nodes[1] = resume => StoryTransition<Graph>.Call(recursive, 0);
        bool limited = false;
        try { module.Play(recursive, () => completions++); } catch (InvalidDataException) { limited = true; }
        Require(limited && !module.IsRunning && completions == 1, "recursive graphs abort without completion");
        var missing = new Graph();
        bool missingRejected = false;
        try { module.Play(missing, null); } catch (InvalidDataException) { missingRejected = true; }
        Require(missingRejected && !module.IsRunning, "missing nodes are errors, not silent completion");

        var instant = new Graph();
        instant.Nodes[1] = resume => { resume(0); return StoryTransition<Graph>.Wait; };
        module.Play(instant, () => module.Play(instant, () => completions++));
        Require(!module.IsRunning && completions == 2, "completion can start another story");
        instant.Nodes[1] = resume => throw new InvalidOperationException("adapter failure");
        bool failed = false;
        try { module.Play(instant, () => completions++); } catch (InvalidOperationException) { failed = true; }
        Require(failed && !module.IsRunning && completions == 2, "adapter error clears run without completing");
        Console.WriteLine("Story module: ordering, nested graphs, async waits, sync callbacks, cancellation, duplicate callbacks, reentry, failure and recursion limits passed.");
    }
}
