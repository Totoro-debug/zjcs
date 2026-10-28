using System;
using System.Collections.Generic;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Consumed original grid/character surfaces. The formal map provides actual
    // original body offsets, the player entity and controller-change events.
    public sealed class MiniMapBodyOffset { public Pos2d Pos; }
    public interface IMiniMapEntityBody
    {
        Pos2d Pos { get; }
        IEnumerable<MiniMapBodyOffset> BodyRangeInfos { get; }
    }
    public interface IMiniMapCharacterController { MyGridTransform MyGridTransform { get; } }
    public interface IMiniMapCharacterManager
    {
        IMiniMapCharacterController MyMeUiCharacter { get; }
        event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;
    }
}
