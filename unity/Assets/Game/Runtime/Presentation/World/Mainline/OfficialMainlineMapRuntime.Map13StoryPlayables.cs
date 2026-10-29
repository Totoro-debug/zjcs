using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13StoryPlayables;
using Zjcs.Official.StoryPlayableRequests;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private NativeMap13StoryPlayableAssets map13StoryPlayableAssets;
        private NativeMap13StoryPlayableScope map13StoryPlayableScope;
        private CancellationTokenSource map13StoryPlayableCancellation;
        private readonly List<Map13StoryRequest> map13StoryRequests = new List<Map13StoryRequest>();
        private sealed class Map13StoryRequest
        {
            public PlayPlayableNode Node;
            public bool Started, Ended, Cleaned;
            public void End() { if (Started && !Ended) { Node.OnEnd(); Ended = true; } }
        }

        private void ValidateMap13StoryPlayables()
        {
            map13StoryPlayableAssets = NativeMap13StoryPlayableAssets.Load();
            foreach (var graph in stories.Values.Concat(storySubgraphs.Values))
                foreach (var node in graph.nodes.Where(n => n.type == "PlayPlayableNode"))
                    map13StoryPlayableAssets.CreateRequest(node.sourceJson);
        }

        private IEnumerator RunMap13StoryPlayable(Map10RuntimeStory graph, Map10RuntimeStoryNode source)
        {
            if (!map13StoryPlayableScope)
            {
                map13StoryPlayableScope = mapRoot.gameObject.AddComponent<NativeMap13StoryPlayableScope>();
                map13StoryPlayableScope.Initialize();
            }
            if (map13StoryPlayableCancellation == null) map13StoryPlayableCancellation = new CancellationTokenSource();
            var request = map13StoryPlayableAssets.CreateRequest(source.sourceJson);
            var lifetime = new Map13StoryRequest { Node = request };
            map13StoryRequests.Add(lifetime);
            request.StartContext = () => lifetime.Started = true;
            request.EndContext = () => lifetime.Ended = true;
            request.CleanContext = () => lifetime.Cleaned = true;
            var proxy = new StoryHandleProxy();
            proxy.BindMyPlayableManager(new Zjcs.Official.StoryPlayableManager.ActivePlayableManagerInfo
                { MyPlayableManager = map13StoryPlayableScope.Manager });
            request.StoryHandleProxy = proxy;
            var actors = new Dictionary<string, StoryPlayableEntityBinding>();
            request.InputValue = port => actors.TryGetValue(port, out var actor) ? actor : null;
            // XNode.Node.GetInputValues reads the exact grouped port. It does
            // not gather indexed dynamic ports; all 29 Map13 grouped ports are
            // unconnected, so the original result is null.
            request.InputValues = name =>
            {
                var port = source.ports.SingleOrDefault(p => p.name == name);
                return port?.connections?.Length > 0
                    ? port.connections.Select(c => ResolveMap13PlayableEntity(graph, c.nodeId)).ToArray() : null;
            };
            request.GetWorldStageSupport = entity => entity ? entity.GetComponents<MonoBehaviour>()
                .OfType<IStoryWorldStagePlayableSupport>().SingleOrDefault() : null;
            Exception failure = null;
            UniTask<StoryRunNodeResultCode> pending = default;
            try
            {
                // Resolve the graph's actual view dependencies before starting
                // visibility/stage lifecycle. Missing views leave no half-started node.
                foreach (var port in source.ports.Where(p => p.name.StartsWith("SourceEntitys ", StringComparison.Ordinal)
                    || p.name.StartsWith("TargetEntitys ", StringComparison.Ordinal)))
                    if (port.connections.Length > 0)
                        actors.Add(port.name, ResolveMap13PlayableEntity(graph, port.connections[0].nodeId));
                request.OnStart();
                pending = request.RunAsync(map13StoryPlayableCancellation.Token);
            }
            catch (Exception error) { failure = error; }
            if (failure == null)
            {
                while (pending.Status == UniTaskStatus.Pending) yield return null;
                try { pending.GetAwaiter().GetResult(); }
                catch (Exception error) { failure = error; }
            }
            lifetime.End();
            if (failure != null)
            {
                if (storyRunning)
                {
                    Debug.LogException(failure, this);
                    AbortStoryGraph();
                    hud.ShowOfficialNotice("剧情暂时无法继续");
                }
                yield break;
            }
            if (storyRunning && ReferenceEquals(activeStory, graph)) RunStoryGraph(StoryNext(source, "Exit"));
        }

        private StoryPlayableEntityBinding ResolveMap13PlayableEntity(Map10RuntimeStory graph, int id)
        {
            if (id == 0) return null;
            var node = FindStoryNode(graph, id) ?? throw new InvalidDataException("Original Map13 actor port node is missing.");
            GameObject actor;
            if (node.type == "AddMapExclusiveNpcAtPosNode" || node.type == "AddMapExclusiveNpcAroundPlayerNode")
            {
                if (!storyEntities.TryGetValue(id, out actor) || !actor)
                    throw new InvalidDataException("Original Map13 story actor was not created: " + graph.key + ":" + id);
            }
            else if (node.type == "GetEntityNode")
            {
                var fields = StorySpatialFields(node);
                if (fields.SelectGetType == 0) actor = avatar;
                else
                {
                    int placement;
                    if (fields.SelectGetType == 4) placement = fields.LoEntityId;
                    else if (fields.SelectGetType == 5)
                        placement = mapLogicTags.Values.SingleOrDefault(t => t.Tag == fields.Tag)?.LogicId
                            ?? throw new InvalidDataException("Original Map13 actor tag is missing: " + fields.Tag);
                    else throw new InvalidDataException("Unsupported original Map13 actor selector: " + fields.SelectGetType);
                    actor = entityView.FindEntityObject(placement);
                }
            }
            else throw new InvalidDataException("Unsupported original Map13 actor output: " + node.type);
            var binding = actor ? actor.GetComponent<StoryPlayableEntityBinding>() : null;
            if (!binding || binding.ClientVisibilitySupporter == null)
                throw new InvalidDataException("Original Map13 actor view/visibility binding is missing: " + graph.key + ":" + id);
            return binding;
        }

        private void CleanMap13StoryPlayables()
        {
            map13StoryPlayableCancellation?.Cancel();
            foreach (var request in map13StoryRequests)
            {
                request.End();
                if (!request.Cleaned) request.Node.CleanWhileStoryAllEnd();
            }
            map13StoryRequests.Clear();
            map13StoryPlayableCancellation?.Dispose();
            map13StoryPlayableCancellation = null;
        }
    }
}
