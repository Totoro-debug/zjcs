using UnityEngine;
namespace Zjcs.Official.StoryTimeline
{
    public interface IStoryTimelineBindings
    {
        GameObject ResolveNode(string key);
        Transform PlayerTransform { get; }
        float GetCameraZ(int stage);
    }
}
