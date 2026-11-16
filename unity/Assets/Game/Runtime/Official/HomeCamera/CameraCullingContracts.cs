using System;
using UnityEngine;
namespace Zjcs.Official.HomeCamera
{
public interface ICameraPopup { bool FullScreenDisplay { get; } }
public interface ICameraWindowEvents { event Action<GameObject> OnWindowOpened; event Action<GameObject> OnWindowClosing; bool TryGetPopup(GameObject go, out ICameraPopup popup); }
public interface ICameraSceneEvents { event Action<CameraSceneData> OnPreLoadScene; }
public sealed class CameraSceneData { public SceneType SceneType; }
public interface ICameraPerformanceSetting { bool BloomEnabled { get; } }
}
