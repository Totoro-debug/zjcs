using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Offline.World.MainlineStoryPlayables;
using Zjcs.Official.StoryPlayableRequests;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private MainlineStoryPlayableAssets mainlineStoryPlayableAssets;
        private MainlineStoryPlayableScope mainlineStoryPlayableScope;
        private CancellationTokenSource mainlineStoryPlayableCancellation;
        private readonly List<MainlineStoryRequest> mainlineStoryRequests = new List<MainlineStoryRequest>();
        private sealed class MainlineStoryRequest
        {
            public PlayPlayableNode Node;
            public bool Started, Ended, Cleaned;
            public void End() { if (Started && !Ended) { Node.OnEnd(); Ended = true; } }
        }
        private sealed class MainlineStoryPlayableVisibility : IStoryPlayableEntityVisibility
        {
            private readonly Action<bool> update;
            public MainlineStoryPlayableVisibility(Action<bool> update) =>
                this.update = update ?? throw new ArgumentNullException(nameof(update));
            public void SetForceVisible(bool visible) => update(visible);
        }

        private void ValidateMainlineStoryPlayables()
        {
            mainlineStoryPlayableAssets = MainlineStoryPlayableAssets.Load(mapClassId,
                MapProfile.StoryDataManifest, catalogName =>
                {
                    var catalog = Resources.Load<Zjcs.Offline.World.NativeMap13.NativeMap13AssetCatalog>(
                        "Map" + mapClassId + "/Native/" + catalogName);
                    if (!catalog || catalog.Bindings == null) return null;
                    return catalog.Bindings.Select(binding => binding == null
                        ? default : new MainlineStoryPlayableCatalogEntry(binding.OfficialPath, binding.Asset)).ToArray();
                });
            foreach (var graph in stories.Values.Concat(storySubgraphs.Values))
                foreach (var node in graph.nodes.Where(n => n.type == "PlayPlayableNode"))
                    mainlineStoryPlayableAssets.CreateRequest(node.sourceJson);
        }

        private IEnumerator RunMainlineStoryPlayable(Map10RuntimeStory graph, Map10RuntimeStoryNode source)
        {
            Action<int> resume = storyResume;
            if (!mainlineStoryPlayableScope)
            {
                mainlineStoryPlayableScope = mapRoot.gameObject.AddComponent<MainlineStoryPlayableScope>();
                mainlineStoryPlayableScope.Initialize();
            }
            if (mainlineStoryPlayableCancellation == null)
                mainlineStoryPlayableCancellation = new CancellationTokenSource();
            var request = mainlineStoryPlayableAssets.CreateRequest(source.sourceJson);
            var lifetime = new MainlineStoryRequest { Node = request };
            mainlineStoryRequests.Add(lifetime);
            request.StartContext = () => lifetime.Started = true;
            request.EndContext = () => lifetime.Ended = true;
            request.CleanContext = () => lifetime.Cleaned = true;
            var proxy = new StoryHandleProxy();
            proxy.BindMyPlayableManager(new Zjcs.Official.StoryPlayableManager.ActivePlayableManagerInfo
                { MyPlayableManager = mainlineStoryPlayableScope.Manager });
            request.StoryHandleProxy = proxy;
            var actors = new Dictionary<string, StoryPlayableEntityBinding>();
            request.InputValue = port => actors.TryGetValue(port, out var actor) ? actor : null;
            // Preserve both original input shapes: grouped ports read all connected
            // graph outputs, while indexed ports are bound by their exact names.
            request.InputValues = name =>
            {
                var port = source.ports.SingleOrDefault(p => p.name == name);
                return port?.connections?.Length > 0
                    ? port.connections.Select(c => ResolveMainlinePlayableEntity(graph, c.nodeId)).ToArray() : null;
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
                        actors.Add(port.name, ResolveMainlinePlayableEntity(graph, port.connections[0].nodeId));
                request.OnStart();
                pending = request.RunAsync(mainlineStoryPlayableCancellation.Token);
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
            if (storyRunning && ReferenceEquals(activeStory, graph)) resume(StoryNext(source, "Exit"));
        }

        private StoryPlayableEntityBinding ResolveMainlinePlayableEntity(Map10RuntimeStory graph, int id)
        {
            if (id == 0) return null;
            var node = FindStoryNode(graph, id) ?? throw new InvalidDataException("Original mainline actor port node is missing.");
            GameObject actor;
            bool isAvatar = false;
            if (node.type == "AddMapExclusiveNpcAtPosNode" || node.type == "AddMapExclusiveNpcAroundPlayerNode")
            {
                if (!storyEntities.TryGetValue(id, out actor) || !actor)
                    throw new InvalidDataException("Original Map" + mapClassId + " story actor was not created: " + graph.key + ":" + id);
            }
            else if (node.type == "GetEntityNode")
            {
                var fields = StorySpatialFields(node);
                if (fields.SelectGetType == 0) { actor = avatar; isAvatar = true; }
                else
                {
                    int placement;
                    if (fields.SelectGetType == 4) placement = fields.LoEntityId;
                    else if (fields.SelectGetType == 5)
                        placement = mapLogicTags.Values.SingleOrDefault(t => t.Tag == fields.Tag)?.LogicId
                            ?? throw new InvalidDataException("Original Map" + mapClassId + " actor tag is missing: " + fields.Tag);
                    else throw new InvalidDataException("Unsupported original Map" + mapClassId + " actor selector: " + fields.SelectGetType);
                    actor = entityView.FindEntityObject(placement);
                }
            }
            else throw new InvalidDataException("Unsupported original Map" + mapClassId + " actor output: " + node.type);
            var binding = actor ? actor.GetComponent<StoryPlayableEntityBinding>() : null;
            if (actor && !binding && isAvatar)
            {
                binding = actor.AddComponent<StoryPlayableEntityBinding>();
                binding.ClientVisibilitySupporter = new MainlineStoryPlayableVisibility(visible =>
                {
                    if (visible) actor.SetActive(true);
                });
            }
            if (!binding || binding.ClientVisibilitySupporter == null)
                throw new InvalidDataException("Original Map" + mapClassId + " actor view/visibility binding is missing: " + graph.key + ":" + id);
            return binding;
        }

        private void CleanMainlineStoryPlayables()
        {
            mainlineStoryPlayableCancellation?.Cancel();
            foreach (var request in mainlineStoryRequests)
            {
                request.End();
                if (!request.Cleaned) request.Node.CleanWhileStoryAllEnd();
            }
            mainlineStoryRequests.Clear();
            mainlineStoryPlayableCancellation?.Dispose();
            mainlineStoryPlayableCancellation = null;
        }
    }
}
