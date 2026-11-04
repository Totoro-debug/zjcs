using System;
using Zjcs.Official.World.Map13MiniMap;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    public sealed partial class MainlineMiniMap
    {
        private EventBindings eventBindings;

        // Container delegates retain their source identity. Player region events
        // belong to each root, so browsing roots cannot rebroadcast them through
        // a shared entity source or keep the player alive after disposal.
        private sealed class EventBindings : IDisposable
        {
            private MiniMapManagedContext state;
            internal MiniMapManagedEvents Events { get; }
            internal EventBindings(IMainlineMiniMapEntities entities, MiniMapManagedContext state)
            {
                this.state = state;
                Events = new MiniMapManagedEvents
                {
                    AddEntityEvent = entities.Events.AddEntityEvent,
                    RemoveEntityEvent = entities.Events.RemoveEntityEvent,
                    ReplaceEntityEvent = entities.Events.ReplaceEntityEvent
                };
                state.OnMapStatusUpdate += Events.FireMapStatusUpdate;
            }
            public void Dispose()
            {
                if (ReferenceEquals(state, null)) return;
                state.OnMapStatusUpdate -= Events.FireMapStatusUpdate;
                state = null;
            }
        }
    }
}
