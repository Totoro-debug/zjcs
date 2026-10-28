using UnityEngine;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Official.StorySourceFade;

namespace Zjcs.Offline.World.NativeMap13EntityViews
{
    // Original application fading uses this existing explicit actor boundary.
    // The independent prop geometry owns all slot/attachment/mesh evaluation.
    public sealed class NativeMap13PropFadeRenderer : NativeActorFadeRenderer
    {
        public NativeMap13PropMecanim Renderer;
        private PropColor color;
        public override NativeActorFadeColor Skeleton
        {
            get
            {
                if (!Renderer || !Renderer.IsValid) return null;
                return color ?? (color = new PropColor(Renderer));
            }
        }
        private sealed class PropColor : NativeActorFadeColor
        {
            private readonly NativeMap13PropMecanim renderer;
            public PropColor(NativeMap13PropMecanim renderer) { this.renderer = renderer; }
            private float Read(int channel) => renderer.View.SkeletonColor[channel];
            private void Write(int channel, float value)
            { var next = renderer.View.SkeletonColor; next[channel] = value; renderer.View.SkeletonColor = next; }
            public override float R { get => Read(0); set => Write(0, value); }
            public override float G { get => Read(1); set => Write(1, value); }
            public override float B { get => Read(2); set => Write(2, value); }
            public override float A { get => Read(3); set => Write(3, value); }
        }
    }
}
