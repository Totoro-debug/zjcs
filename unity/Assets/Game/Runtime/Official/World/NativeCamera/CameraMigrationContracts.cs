using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Zjcs.Official.World.NativeCamera
{
    // Local dependency contracts for the copied official controllers. Map preparation
    // must finish before a discontinuous camera move; callers supply actual map data.
    public interface IInputOperationModule
    {
        void HookOnInput<T>(Action<T> callback) where T : IInputOperationResult;
        void UnhookOnInput<T>(Action<T> callback) where T : IInputOperationResult;
    }
    public interface IMapRootManagerProxy
    {
        GameObject MapRoot { get; }
        CameraGridGround GridGround { get; }
        Task PrepareInstantly(Vector3 position);
    }
    public sealed class CameraGridGround { public GridGroundComponentInfo Info; }
    public sealed class GridGroundComponentInfo
    {
        public Vector3 OriginPos;
        public Vector2Int Size;
    }
    public sealed class CameraSettingsValue
    {
        public CameraControlSettings Value { get; }
        public CameraSettingsValue(CameraControlSettings value)
        { Value = value ? value : throw new ArgumentNullException(nameof(value)); }
    }
    internal static class CameraVectorCast
    {
        public static Vector3 Convert(Vector2 value) => value;
        public static Vector2 Convert(Vector3 value) => value;
    }
    internal static class CameraAsync
    {
        // UniTask.Forget reports asynchronous errors. Keep that behavior when
        // using Unity's synchronization context with the local Task-based loader.
        public static async void Observe(Task operation)
        {
            try { await operation; }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
