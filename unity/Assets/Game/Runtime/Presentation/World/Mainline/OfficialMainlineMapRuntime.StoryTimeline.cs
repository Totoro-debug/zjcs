using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.World;
using Zjcs.Official.StoryTimeline;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private StoryTimelineModule storyTimelines;
        private NativeStoryTimelineHost storyTimelineHost;
        private bool storyTimelineCameraActive;

        private IEnumerator RunStoryTimeline(Map10RuntimeStoryNode node)
        {
            Action<int> resume = storyResume;
            var request = JsonUtility.FromJson<StoryTimelineRequest>(node.sourceJson);
            var output = node.ports.SingleOrDefault(port => port.name == "PlayableDirectorOutput");
            if (output?.connections?.Length > 0)
                throw new InvalidDataException("Bind original Timeline director output consumers before running this graph.");
            if (node.ports.Any(port => port.name.StartsWith("[Timeline]", StringComparison.Ordinal) && port.connections.Length > 0))
                throw new InvalidDataException("Bind connected original Timeline track inputs before running this graph.");
            // Force creation of the common native mask before creating its host.
            var ui = StoryUi;
            storyTimelineHost = new NativeStoryTimelineHost(mapRoot, worldCamera, avatar.transform, storyMasks.Manager);
            storyTimelines = new StoryTimelineModule(storyTimelineHost);
            storyTimelineCameraActive = true;
            SetStoryWorldUiHidden(true);
            bool safeEnd;
            var wait = storyTimelines.Play(request);
            try
            {
                while (wait.MoveNext()) yield return wait.Current;
                safeEnd = storyTimelines.SafeEnd;
            }
            finally
            {
                (wait as IDisposable)?.Dispose();
                CleanStoryTimelines();
                SetStoryWorldUiHidden(false);
            }
            if (safeEnd) AbortStoryGraph();
            else resume(StoryNext(node, "Exit"));
        }

        private void CleanStoryTimelines()
        {
            storyTimelines?.Dispose(); storyTimelines = null;
            storyTimelineHost?.Dispose(); storyTimelineHost = null;
            storyTimelineCameraActive = false;
        }
    }
}
