using System;
using System.Collections.Generic;
using System.IO;

namespace Zjcs.Offline.Story
{
    // Offline adaptation of StoryGraph.AsyncRun and SubgraphRunNode.InternalRun.
    // Graph/node payloads remain the official chapter configuration, owned by the adapter.
    public sealed class StoryModule<TGraph, TNode> where TGraph : class where TNode : class
    {
        private readonly Func<TGraph, int> entry;
        private readonly Func<TGraph, int, TNode> find;
        private readonly Func<TGraph, TNode, Action<int>, StoryTransition<TGraph>> execute;
        private readonly Stack<Frame> frames = new Stack<Frame>();
        private readonly int synchronousNodeLimit;
        private Action completed;
        private long generation, step;
        private bool pumping, pending;
        private int next;
        private struct Frame { public TGraph Graph; public int Next; }

        public StoryModule(Func<TGraph, int> entry, Func<TGraph, int, TNode> find,
            Func<TGraph, TNode, Action<int>, StoryTransition<TGraph>> execute, int synchronousNodeLimit = 256)
        {
            this.entry = entry ?? throw new ArgumentNullException(nameof(entry));
            this.find = find ?? throw new ArgumentNullException(nameof(find));
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            if (synchronousNodeLimit < 1) throw new ArgumentOutOfRangeException(nameof(synchronousNodeLimit));
            this.synchronousNodeLimit = synchronousNodeLimit;
        }

        public bool IsRunning { get; private set; }
        public TGraph ActiveGraph { get; private set; }

        // Main-thread interface. A resume belongs to one node invocation, is single-use,
        // and becomes inert on cancel, completion, or a later node. Synchronous resumes
        // are drained iteratively so native panels may complete during their opening.
        public void Play(TGraph graph, Action completed)
        {
            if (IsRunning) throw new InvalidOperationException("A story is already running.");
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            generation++;
            ActiveGraph = graph;
            this.completed = completed;
            IsRunning = true;
            try
            {
                next = entry(graph);
                pending = true;
                Pump();
            }
            catch { Cancel(); throw; }
        }

        public void Cancel()
        {
            generation++;
            step++;
            IsRunning = false;
            ActiveGraph = null;
            completed = null;
            pending = false;
            frames.Clear();
        }

        private void Pump()
        {
            if (pumping) return;
            pumping = true;
            try
            {
                int count = 0;
                while (IsRunning && pending)
                {
                    if (++count > synchronousNodeLimit)
                        throw new InvalidDataException("Official story exceeded its synchronous node limit.");
                    pending = false;
                    if (next == 0)
                    {
                        if (frames.Count > 0)
                        {
                            Frame frame = frames.Pop();
                            ActiveGraph = frame.Graph;
                            next = frame.Next;
                            pending = true;
                            continue;
                        }
                        Action done = completed;
                        Cancel();
                        // Permit completion to start another story without inheriting this pump's budget.
                        pumping = false;
                        done?.Invoke();
                        return;
                    }
                    TNode node = find(ActiveGraph, next);
                    if (node == null) throw new InvalidDataException("Official story node is missing: " + next);
                    long invocation = ++step, run = generation;
                    bool resumed = false;
                    Action<int> resume = target =>
                    {
                        if (!IsRunning || generation != run || step != invocation || resumed) return;
                        resumed = true;
                        next = target;
                        pending = true;
                        Pump();
                    };
                    StoryTransition<TGraph> result = execute(ActiveGraph, node, resume);
                    if (!IsRunning || generation != run) continue;
                    if (resumed && result.Kind != StoryTransitionKind.Wait)
                        throw new InvalidOperationException("A story adapter both resumed and returned a transition.");
                    switch (result.Kind)
                    {
                        case StoryTransitionKind.Wait: break;
                        case StoryTransitionKind.Next: resume(result.NextNodeId); break;
                        case StoryTransitionKind.Finish: resume(0); break;
                        case StoryTransitionKind.Call:
                            if (result.Graph == null) throw new InvalidDataException("Official story subgraph is missing.");
                            frames.Push(new Frame { Graph = ActiveGraph, Next = result.NextNodeId });
                            ActiveGraph = result.Graph;
                            resume(entry(ActiveGraph));
                            break;
                        default: throw new InvalidOperationException("Unknown story transition.");
                    }
                }
            }
            catch
            {
                Cancel();
                throw;
            }
            finally { pumping = false; }
        }
    }

    public enum StoryTransitionKind { Wait, Next, Finish, Call }

    public readonly struct StoryTransition<TGraph> where TGraph : class
    {
        public StoryTransitionKind Kind { get; }
        public int NextNodeId { get; }
        public TGraph Graph { get; }
        private StoryTransition(StoryTransitionKind kind, int next, TGraph graph = null)
        { Kind = kind; NextNodeId = next; Graph = graph; }
        public static StoryTransition<TGraph> Wait => default;
        public static StoryTransition<TGraph> Next(int node) => new StoryTransition<TGraph>(StoryTransitionKind.Next, node);
        public static StoryTransition<TGraph> Finish => new StoryTransition<TGraph>(StoryTransitionKind.Finish, 0);
        public static StoryTransition<TGraph> Call(TGraph graph, int next) => new StoryTransition<TGraph>(StoryTransitionKind.Call, next, graph);
    }
}
