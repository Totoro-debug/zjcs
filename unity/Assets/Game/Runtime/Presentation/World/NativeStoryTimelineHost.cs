using System;
using System.Linq;
using Cinemachine;
using UnityEngine;
using Zjcs.Official.StoryMasks;
using Zjcs.Official.StoryTimeline;

namespace Zjcs.Presentation.World
{
    // Per-scene adapter. Authored shots and camera parameters live in the
    // original native prefab/Timeline; this owns only desktop scene bindings.
    public sealed class NativeStoryTimelineHost : IStoryTimelineHost, IDisposable
    {
        [Serializable] private sealed class Asset { public string guid, path; }
        [Serializable] private sealed class CameraZ { public int CameraStage; public float Z; }
        [Serializable] private sealed class Configuration
        {
            public string manifest;
            public Asset[] assets;
            public float defaultCameraZ;
            public CameraZ[] cameraZList;
        }
        private readonly Configuration config;
        private readonly Transform root;
        private readonly Camera camera;
        private readonly CinemachineBrain brain;
        private readonly bool ownedBrain, wasEnabled;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly float size, fieldOfView, near, far;
        public Transform PlayerTransform { get; }
        public IStoryMaskManager Masks { get; }

        public NativeStoryTimelineHost(Transform root, Camera camera, Transform player, IStoryMaskManager masks)
        {
            this.root = root ? root : throw new ArgumentNullException(nameof(root));
            this.camera = camera ? camera : throw new ArgumentNullException(nameof(camera));
            PlayerTransform = player ? player : throw new ArgumentNullException(nameof(player));
            Masks = masks ?? throw new ArgumentNullException(nameof(masks));
            var source = Resources.Load<TextAsset>("StoryUI/Timeline/bindings");
            if (!source) throw new InvalidOperationException("Original story Timeline resource bindings are absent.");
            config = JsonUtility.FromJson<Configuration>(source.text);
            if (config.manifest != "884_156692") throw new InvalidOperationException("Original story Timeline source version differs.");
            brain = camera.GetComponent<CinemachineBrain>();
            ownedBrain = !brain;
            if (ownedBrain) brain = camera.gameObject.AddComponent<CinemachineBrain>();
            wasEnabled = brain.enabled;
            position = camera.transform.position; rotation = camera.transform.rotation;
            size = camera.orthographicSize; fieldOfView = camera.fieldOfView;
            near = camera.nearClipPlane; far = camera.farClipPlane;
            brain.enabled = true;
        }
        public GameObject ResolveNode(string key) => key == "WorldCamera" ? camera.gameObject : null;
        public GameObject LoadPrefab(string guid)
        {
            var asset = config.assets.SingleOrDefault(row => row.guid == guid);
            if (asset == null) throw new InvalidOperationException("Unbound original story Timeline: " + guid);
            return Resources.Load<GameObject>(asset.path);
        }
        public Transform SceneRoot(int scene) => scene == 3 ? root : null;
        public float GetCameraZ(int stage)
        {
            foreach (var row in config.cameraZList)
                if (row.CameraStage == stage) return row.Z;
            return config.defaultCameraZ;
        }
        // The scene owner suspends its ordinary camera updater for this scope.
        // PlayableDirector's Cinemachine track drives this same camera's brain.
        public void EnterStoryCamera() { }
        public void ExitStoryCamera() { }
        public void Dispose()
        {
            if (brain)
            {
                brain.enabled = wasEnabled && !ownedBrain;
                if (ownedBrain) UnityEngine.Object.Destroy(brain);
            }
            if (camera)
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size; camera.fieldOfView = fieldOfView;
                camera.nearClipPlane = near; camera.farClipPlane = far;
            }
        }
    }
}
