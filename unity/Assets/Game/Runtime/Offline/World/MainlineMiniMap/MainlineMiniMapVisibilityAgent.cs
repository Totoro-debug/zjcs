using System;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Borrows the world's live component. The minimap never decodes or owns a
    // second exploration state, and closing its UI must not stop world visibility.
    public sealed class MainlineMiniMapVisibilityAgent : MiniMapVisibilityAgent, IDisposable
    {
        private GridVisibilityAgentComponent source;
        private bool disposed;
        public override GridVisibilityAgentComponent Component => source
            ?? throw new InvalidOperationException("The live map visibility component is not bound.");
        public override event Action<Pos2d, bool, bool> OnPosVisibilityUpdate;

        public void Bind(GridVisibilityAgentComponent component)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MainlineMiniMapVisibilityAgent));
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (ReferenceEquals(source, component)) return;
            if (source != null) throw new InvalidOperationException("Recreate the minimap when its world visibility instance changes.");
            source = component;
            source.OnPosVisibilityUpdate += Forward;
        }

        private void Forward(Pos2d position, bool previous, bool visible, bool region) =>
            OnPosVisibilityUpdate?.Invoke(position, previous, visible);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (source != null) source.OnPosVisibilityUpdate -= Forward;
            source = null;
            OnPosVisibilityUpdate = null;
        }

        private void OnDestroy() => Dispose();
    }
}
