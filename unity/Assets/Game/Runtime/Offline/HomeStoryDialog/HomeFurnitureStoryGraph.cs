using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.Story;

namespace Zjcs.Offline.HomeStoryDialog
{
    // The scene host executes each original node; this adapter only follows its
    // serialized XNode connections and preserves the official option routing.
    public sealed class HomeFurnitureStoryGraph
    {
        public sealed class Node
        {
            public int Id { get; internal set; }
            public string Type { get; internal set; }
            public JObject Data { get; internal set; }
        }

        public interface IHost
        {
            // The callback receives a selected option index, or null for Exit.
            void Execute(string story, Node node, Action<int?> completed);
        }

        private sealed class Graph
        {
            public string Name;
            public int Entry;
            public readonly Dictionary<int, Node> Nodes = new Dictionary<int, Node>();
        }

        private readonly Dictionary<string, Graph> graphs = new Dictionary<string, Graph>();
        private readonly StoryModule<Graph, Node> module;
        private readonly IHost host;

        public bool IsRunning => module.IsRunning;
        public string ActiveStory => module.ActiveGraph?.Name;

        public HomeFurnitureStoryGraph(TextAsset source, IHost host)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            JObject root = JObject.Parse(source.text);
            if (!(root["graphs"] is JObject data))
                throw new InvalidDataException("The selected original furniture stories are missing.");
            foreach (JProperty story in data.Properties())
            {
                var graph = new Graph { Name = story.Name };
                if (!(story.Value["nodes"] is JObject nodes)) throw new InvalidDataException("Story nodes are missing: " + story.Name);
                foreach (JProperty item in nodes.Properties())
                {
                    if (!int.TryParse(item.Name, out int id) || !(item.Value is JObject value)
                        || (int?)value["NodeId"] != id || string.IsNullOrEmpty((string)value["_type"]))
                        throw new InvalidDataException("Invalid original story node: " + story.Name + "/" + item.Name);
                    var node = new Node { Id = id, Type = (string)value["_type"], Data = value };
                    graph.Nodes.Add(id, node);
                    if (node.Type == "StartStoryNode")
                    {
                        if (graph.Entry != 0) throw new InvalidDataException("Duplicate original story entry: " + story.Name);
                        graph.Entry = id;
                    }
                }
                if (graph.Entry == 0) throw new InvalidDataException("Original story entry is missing: " + story.Name);
                graphs.Add(graph.Name, graph);
            }
            module = new StoryModule<Graph, Node>(graph => graph.Entry,
                (graph, id) => graph.Nodes.TryGetValue(id, out var node) ? node : null, Execute);
        }

        public void Play(string name, Action completed)
        {
            if (!graphs.TryGetValue(name, out var graph)) throw new InvalidDataException("Unknown original furniture story: " + name);
            module.Play(graph, completed);
        }

        public void Cancel() => module.Cancel();

        private StoryTransition<Graph> Execute(Graph graph, Node node, Action<int> resume)
        {
            if (node.Type == "FinishStoryNode") return StoryTransition<Graph>.Finish;
            host.Execute(graph.Name, node, option =>
            {
                string port = "Exit";
                if (node.Type == "OptionDialogNode" && option.HasValue && option.Value >= 0)
                {
                    var options = node.Data["Options"] as JArray;
                    if (options != null && option.Value < options.Count) port = "Options " + option.Value;
                }
                resume(Next(graph, node, port));
            });
            return StoryTransition<Graph>.Wait;
        }

        private static int Next(Graph graph, Node node, string port)
        {
            var links = node.Data["ports"]?[port]?["connections"] as JArray;
            if (links == null || links.Count == 0) return 0;
            if (!int.TryParse((string)links[0]["node"], out int next) || !graph.Nodes.ContainsKey(next))
                throw new InvalidDataException("Invalid original story connection: " + graph.Name + "/" + node.Id + "/" + port);
            return next;
        }
    }
}
