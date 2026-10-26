using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        [Serializable] private sealed class Map11PlayerViewSource
        {
            public Vector3 modelScale, modelOffset, cameraSafeExtents;
            public int sortingOrder, cameraAdaptPriority;
            public float cameraDefaultSize, cameraNear, cameraFar;
        }
        private Map11PlayerViewSource map11WorldViewSource;
        private Vector2Int map11CameraResolution;

        private void RefreshMap11CameraResolution()
        {
            if (!MapProfile.HasNativeUi || storyCameraActive) return;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (resolution == map11CameraResolution) return;
            map11CameraResolution = resolution;
            worldCamera.orthographicSize = DefaultWorldCameraSize();
        }

        // OrthographicCameraResolutionAdaptor.AdaptOrthographicSize, original
        // priority and safe bounds read from WorldCamera/controller.
        private float DefaultWorldCameraSize()
        {
            if (!MapProfile.HasNativeUi) return 7.5f;
            var data = map11WorldViewSource ?? throw new InvalidDataException("Official camera source is not loaded.");
            float aspect = (float)Screen.height / Screen.width;
            float size = data.cameraAdaptPriority == 1 ? data.cameraSafeExtents.y
                : data.cameraAdaptPriority == 2 ? data.cameraSafeExtents.x * aspect : data.cameraDefaultSize;
            if (size < data.cameraSafeExtents.y) size = data.cameraSafeExtents.y;
            else if (size / aspect < data.cameraSafeExtents.x) size = data.cameraSafeExtents.x * aspect;
            return size;
        }

        // 3001_view/Model/Offset/Player_Model_Map. The native model factory returns
        // the skeleton alone; the world prefab owns its model offset, scale and layer.
        private GameObject MountMap11Player(GameObject model)
        {
            var source = Resources.Load<TextAsset>(MapResource("BaseGround/player-view"));
            if (source == null) throw new InvalidDataException("Official world player view is not bound.");
            var data = JsonUtility.FromJson<Map11PlayerViewSource>(source.text);
            map11WorldViewSource = data;
            var root = new GameObject("Official Map" + mapClassId + " player view");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = data.modelOffset;
            model.transform.localScale = data.modelScale;
            var sorting = root.AddComponent<SortingGroup>();
            sorting.sortingOrder = data.sortingOrder;
            return root;
        }
    }
}
