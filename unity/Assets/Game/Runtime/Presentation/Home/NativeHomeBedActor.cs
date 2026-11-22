using System;
using Zjcs.Official.HomeSceneInput;

namespace Zjcs.Presentation.Home
{
    /// <summary>Offline owner for the original Bed prefab's scene click.</summary>
    public sealed class NativeHomeBedActor : ClickableHomeObj
    {
        public event Action Clicked;

        protected override void OnValidClick() => Clicked?.Invoke();
    }
}
