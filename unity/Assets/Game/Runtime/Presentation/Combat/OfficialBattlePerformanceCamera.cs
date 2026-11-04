using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEngine;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Original FightCamera switching and GameObjectInput blocking, scoped to one battle.</summary>
    public sealed class OfficialBattlePerformanceCamera : IDisposable, IBattleCameraPerformanceHost
    {
        [Serializable] private sealed class Catalog
        {
            public string manifest;
            public Binding[] cameras;
        }
        [Serializable] private sealed class Binding
        {
            public string sceneName, cameraJson, offsetJson;
            public Node[] transforms;
        }
        [Serializable] private sealed class Node
        {
            public string name;
            public bool active;
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        private static Catalog catalog;
        private readonly GameObject root, normalCamera;
        private readonly CinemachineVirtualCamera camera;
        private readonly CinemachineCameraOffset offset;
        private readonly HashSet<long> inputBlocks = new HashSet<long>();
        private uint signatureSeed;
        private bool disposed;

        public bool IsActive => !disposed && camera.gameObject.activeSelf;
        public bool AllowsGameObjectInput => inputBlocks.Count == 0;

        public OfficialBattlePerformanceCamera(Transform parent, string sceneName, GameObject normalCamera)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.normalCamera = normalCamera != null ? normalCamera
                : throw new ArgumentNullException(nameof(normalCamera));
            if (catalog == null)
            {
                var asset = Resources.Load<TextAsset>("Combat/performance-camera-bindings");
                if (asset == null) throw new InvalidDataException("Original performance camera bindings are missing.");
                catalog = JsonUtility.FromJson<Catalog>(asset.text);
            }
            if (catalog == null || catalog.manifest != "884_156692" || catalog.cameras == null)
                throw new InvalidDataException("Original performance camera bindings have the wrong source.");
            var binding = Array.Find(catalog.cameras, value => value.sceneName == sceneName);
            if (binding?.transforms == null || binding.transforms.Length == 0
                || string.IsNullOrEmpty(binding.cameraJson) || string.IsNullOrEmpty(binding.offsetJson))
                throw new InvalidDataException("Original performance camera is missing: " + sceneName);
            try
            {
                Transform current = parent;
                foreach (Node node in binding.transforms)
                {
                    var go = new GameObject(node.name);
                    if (root == null) root = go;
                    go.SetActive(false);
                    go.transform.SetParent(current, false);
                    go.transform.localPosition = node.position;
                    go.transform.localRotation = node.rotation;
                    go.transform.localScale = node.scale;
                    current = go.transform;
                }
                camera = current.gameObject.AddComponent<CinemachineVirtualCamera>();
                JsonUtility.FromJsonOverwrite(binding.cameraJson, camera);
                offset = current.gameObject.AddComponent<CinemachineCameraOffset>();
                JsonUtility.FromJsonOverwrite(binding.offsetJson, offset);
                // The verified original pipeline has only the Cinemachine marker,
                // with no body, aim or noise components and no Follow/LookAt target.
                Transform ancestor = current;
                for (int i = binding.transforms.Length - 1; i >= 0; --i)
                {
                    ancestor.gameObject.SetActive(binding.transforms[i].active);
                    ancestor = ancestor.parent;
                }
                if (camera.gameObject.activeSelf)
                    throw new InvalidDataException("The original performance camera must start inactive: " + sceneName);
            }
            catch
            {
                if (root != null) OfficialBattleSceneBindings.DestroyCreatedObject(root);
                throw;
            }
        }

        public void SwitchInPerformanceCamera()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattlePerformanceCamera));
            // GameCameraController uses direct toggles, independently of input tokens.
            normalCamera.SetActive(false);
            camera.gameObject.SetActive(true);
        }

        public void SwitchOutPerformanceCamera()
        {
            if (disposed) return;
            normalCamera.SetActive(true);
            camera.gameObject.SetActive(false);
        }

        public void SetOffset(Vector3 value) => offset.m_Offset = new Vector3(value.x, value.y, 0f);

        public long BlockGameObjectInput()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattlePerformanceCamera));
            // InputOperationModule.Block, source = InputOperationLayer.GameObjectInput (1).
            long signature;
            do
            {
                signatureSeed = signatureSeed < uint.MaxValue ? signatureSeed + 1 : 0u;
                signature = (1L << 32) | signatureSeed;
            } while (!inputBlocks.Add(signature));
            return signature;
        }

        public void UnblockGameObjectInput(long operationId)
        {
            if ((int)(operationId >> 32) == 1) inputBlocks.Remove(operationId);
        }

        public void Dispose()
        {
            if (disposed) return;
            SwitchOutPerformanceCamera();
            disposed = true;
            inputBlocks.Clear();
            if (root != null) OfficialBattleSceneBindings.DestroyCreatedObject(root);
        }
    }
}
