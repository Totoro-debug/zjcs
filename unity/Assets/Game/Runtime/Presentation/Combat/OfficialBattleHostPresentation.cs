using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    /// <summary>The caller supplies its visible world/HUD roots and camera. The battle module
    /// owns their temporary suspension and restores the captured state on exit or backgrounding.</summary>
    public sealed class OfficialBattleHostPresentation
    {
        private readonly Camera camera;
        private readonly Transform worldRoot, hudRoot;
        private readonly Action<bool> suspendHost;
        private readonly Transform[] additionalHudRoots;

        public OfficialBattleHostPresentation(Camera camera, Transform worldRoot, Transform hudRoot,
            Action<bool> suspendHost = null, params Transform[] additionalHudRoots)
        {
            this.camera = camera;
            this.worldRoot = worldRoot;
            this.hudRoot = hudRoot;
            this.suspendHost = suspendHost;
            this.additionalHudRoots = additionalHudRoots;
        }

        internal Scope Capture(Transform battleRoot)
        {
            return new Scope(camera, worldRoot, hudRoot, battleRoot, suspendHost, additionalHudRoots);
        }

        internal sealed class Scope : IDisposable
        {
            private readonly Camera camera;
            private readonly Action<bool> suspendHost;
            private readonly List<GameObject> suspended = new List<GameObject>();
            private readonly Vector3 previousPosition;
            private readonly Quaternion previousRotation;
            private readonly float previousSize;
            private readonly float previousNear, previousFar, previousFov;
            private readonly Vector2 previousLensShift, previousSensorSize;
            private readonly bool previousOrthographic, previousPhysical;
            private readonly Camera.GateFitMode previousGateFit;
            private Vector3 battlePosition;
            private float battleSize;
            private bool hasBattleCamera, visible, disposed;
            internal Camera Camera => camera;

            internal Scope(Camera camera, Transform worldRoot, Transform hudRoot, Transform battleRoot,
                Action<bool> suspendHost, Transform[] additionalHudRoots)
            {
                this.camera = camera;
                this.suspendHost = suspendHost;
                if (camera != null)
                {
                    previousPosition = camera.transform.position;
                    previousRotation = camera.transform.rotation;
                    previousSize = camera.orthographicSize;
                    previousNear = camera.nearClipPlane;
                    previousFar = camera.farClipPlane;
                    previousFov = camera.fieldOfView;
                    previousLensShift = camera.lensShift;
                    previousSensorSize = camera.sensorSize;
                    previousOrthographic = camera.orthographic;
                    previousPhysical = camera.usePhysicalProperties;
                    previousGateFit = camera.gateFit;
                }
                CaptureChildren(worldRoot, battleRoot);
                if (hudRoot != worldRoot) CaptureChildren(hudRoot, battleRoot);
                foreach (Transform root in additionalHudRoots ?? Array.Empty<Transform>())
                    if (root != worldRoot && root != hudRoot) CaptureChildren(root, battleRoot);
            }

            private void CaptureChildren(Transform root, Transform battleRoot)
            {
                if (root == null) return;
                foreach (Transform child in root)
                {
                    if (!child.gameObject.activeSelf || suspended.Contains(child.gameObject)
                        || (camera != null && camera.transform.IsChildOf(child))
                        || (battleRoot != null && battleRoot.IsChildOf(child))) continue;
                    suspended.Add(child.gameObject);
                }
            }

            internal void SetCamera(Vector3 position, float size)
            {
                battlePosition = position;
                battleSize = size;
                hasBattleCamera = true;
            }

            internal void SetVisible(bool value)
            {
                if (disposed || visible == value) return;
                visible = value;
                suspendHost?.Invoke(value);
                foreach (GameObject item in suspended) if (item != null) item.SetActive(!value);
                if (camera == null) return;
                if (value && hasBattleCamera)
                {
                    camera.transform.position = battlePosition;
                    camera.orthographicSize = battleSize;
                }
                else if (!value)
                {
                    camera.transform.position = previousPosition;
                    camera.transform.rotation = previousRotation;
                    camera.orthographicSize = previousSize;
                    camera.orthographic = previousOrthographic;
                    camera.nearClipPlane = previousNear;
                    camera.farClipPlane = previousFar;
                    camera.usePhysicalProperties = previousPhysical;
                    camera.sensorSize = previousSensorSize;
                    camera.lensShift = previousLensShift;
                    camera.gateFit = previousGateFit;
                    camera.fieldOfView = previousFov;
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                SetVisible(false);
                disposed = true;
                suspended.Clear();
            }
        }
    }
}
