using UnityEngine;
namespace Zjcs.Official.StorySourceFade
{
    // Explicit boundary to the independent native actor. No SDK implementation.
    public abstract class NativeActorFadeColor
    {
        public abstract float R { get; set; }
        public abstract float G { get; set; }
        public abstract float B { get; set; }
        public abstract float A { get; set; }
    }
    public abstract class NativeActorFadeRenderer : MonoBehaviour
    {
        public abstract NativeActorFadeColor Skeleton { get; }
    }
    internal static class NativeActorFadeColorExtensions
    {
        public static void SetColor(NativeActorFadeColor target, Color value)
        {
            target.R=value.r; target.G=value.g; target.B=value.b; target.A=value.a;
        }
    }
}
