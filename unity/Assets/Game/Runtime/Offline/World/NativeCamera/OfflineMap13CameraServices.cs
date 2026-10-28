using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.World.NativeCamera;

namespace Zjcs.Offline.World.NativeCamera
{
    public sealed class OfflineMap13CameraServices : ICameraControlManager, IMapRootManagerProxy
    {
        private readonly Func<Vector3, Task> prepare;
        private readonly CameraSettingsValue settings;
        private readonly GmCameraControlData gm = new GmCameraControlData();
        public CameraControlBrain ControlBrain { get; private set; }
        public GameObject MapRoot { get; }
        public CameraGridGround GridGround { get; }
        public OfflineCameraInput Input { get; } = new OfflineCameraInput();
        public DragControllerManager DragControllers { get; } = new DragControllerManager();
        public Func<Vector3> PointerPosition { get; set; } = () => UnityEngine.Input.mousePosition;
        public Func<int> TouchCount { get; set; } = () => UnityEngine.Input.touchCount;

        public OfflineMap13CameraServices(CameraControlSettings configuration, GameObject mapRoot,
            Vector3 gridOrigin, Vector2Int gridSize, Func<Vector3, Task> prepareInstantly)
        {
            settings = new CameraSettingsValue(configuration);
            MapRoot = mapRoot ? mapRoot : throw new ArgumentNullException(nameof(mapRoot));
            GridGround = new CameraGridGround { Info = new GridGroundComponentInfo { OriginPos = gridOrigin, Size = gridSize } };
            prepare = prepareInstantly ?? throw new ArgumentNullException(nameof(prepareInstantly));
        }
        public Task PrepareInstantly(Vector3 position) => prepare(position);
        public void Init(CameraControlBrain cameraControlBrain) => ControlBrain = cameraControlBrain;
        public void Inject(object component)
        {
            if (component is CameraControlBrain brain)
            { brain.CameraControlManager = this; brain.InjectDependencies = Inject; }
            if (component is CameraBaseController controller)
            { controller.InputOperationModule = Input; controller.mCameraControlSettings = settings; controller.GmCameraControlData = gm; }
            if (component is WorldCameraBaseController world) world.MapRootManagerProxy = this;
            if (component is WorldCameraDragController dragMode) dragMode.MapRootManagerProxy = this;
            if (component is CameraStoryController story) story.MapRootManagerProxy = this;
            if (component is DragControllerBase drag)
            { drag.DragControllerManager = DragControllers; drag.PointerPosition = () => PointerPosition(); drag.TouchCount = () => TouchCount(); }
            if (component is IgnoreUIDragController ignoreUi) ignoreUi.InputOperationModule = Input;
        }
    }

    // Local dispatch boundary for the copied typed result consumers. Like the original
    // InputOperationModule, invoke callbacks in reverse hook order and leave removed
    // signatures as tombstones until dispatch. Windows input routing is supplied by the host.
    public sealed class OfflineCameraInput : IInputOperationModule
    {
        private readonly Dictionary<Type, List<long>> buffers = new Dictionary<Type, List<long>>();
        private readonly Dictionary<long, Delegate> callbacks = new Dictionary<long, Delegate>();
        private readonly Dictionary<Delegate, long> signatures = new Dictionary<Delegate, long>();
        private long next;
        public void HookOnInput<T>(Action<T> callback) where T : IInputOperationResult
        {
            if (signatures.ContainsKey(callback)) { Debug.LogError(callback + " 被重复Hook!"); return; }
            if (!buffers.TryGetValue(typeof(T), out var buffer)) buffers.Add(typeof(T), buffer = new List<long>());
            long id = ++next;
            buffer.Add(id); callbacks.Add(id, callback); signatures.Add(callback, id);
        }
        public void UnhookOnInput<T>(Action<T> callback) where T : IInputOperationResult
        {
            if (!signatures.TryGetValue(callback, out long id)) return;
            signatures.Remove(callback); callbacks.Remove(id);
        }
        public void Dispatch<T>(T result) where T : IInputOperationResult
        {
            if (!buffers.TryGetValue(typeof(T), out var buffer)) return;
            for (int index = buffer.Count - 1; index >= 0; index--)
            {
                if (!callbacks.TryGetValue(buffer[index], out var callback)) buffer.RemoveAt(index);
                else ((Action<T>)callback)(result);
            }
        }
    }
}
