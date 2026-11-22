// Original selected native scene input; explicit pointer, camera and UI owners.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeSceneInput
{
using Object = UnityEngine.Object;
public interface IHomeSceneClickPointer : IHomePointerSnapshot
{
    bool LeftUp { get; }
}
public interface IHomeSceneInputWorld
{
    Camera Camera { get; }
    bool EventSystemValid { get; }
    bool TryGetFirstRaycastResult(Vector2 screenPos, out RaycastResult result);
}
public interface IHomeSceneClickContext
{
    bool InMyHome { get; }
    bool AllNoTipWindowsClosed { get; }
}

}
