// Explicit replacement for the original current-player dependency-injection lookup.
using UnityEngine;
namespace Zjcs.Official.StoryAnimatorTimeline
{
    public interface IPlayerAnimatorTimelineServices
    {
        GameObject PlayerEntity { get; }
    }
    public static class PlayerAnimatorTimelineServices
    {
        public static IPlayerAnimatorTimelineServices Current { get; set; }
    }
}
