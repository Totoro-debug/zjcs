using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Read-only ports for the original map/controller components. Their
    // lifecycle and world interpolation are owned by the formal map host.
    public abstract class MiniMapCharacterContext:MiniMapMaskContext
    {
        public abstract bool HasMapRoot { get; }
        public abstract Object GetMoveAgent(IMiniMapCharacterController controller);
        public abstract Vector3 GetCharacterWorldPosition(IMiniMapCharacterController controller);
    }
}
