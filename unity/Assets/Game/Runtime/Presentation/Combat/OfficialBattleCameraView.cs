using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // Replays the original GameCameraIniter aspect adapter and group framing with the
    // installed Cinemachine implementation. Unit positions come from the same scene as their HUD.
    internal sealed class OfficialBattleCameraView : IDisposable
    {
        [Serializable] private sealed class Catalog
        {
            public bool defaultFollow;
            public float[] viewBound;
            public Binding[] cameras;
        }
        [Serializable] private sealed class Binding
        {
            public int gameId, priority;
            public float[] baseAspect;
            public float baseSize, yOffset;
            public bool includeDead;
            public LensSettings lens;
            public string framingJson, groupJson;
        }
        private static Catalog catalog;
        private readonly Binding binding;
        private readonly GameObject root;
        private readonly CinemachineVirtualCamera virtualCamera;
        private readonly CinemachineTargetGroup group;
        private readonly CinemachineFramingTransposer framing;
        private readonly CinemachineFramingTransposer.FramingMode framingMode;
        private readonly CinemachineBrain brain;
        private readonly bool addedBrain, previousBrainEnabled;
        private readonly Func<int, Transform> unitRoot;
        private readonly Dictionary<int, Transform> followers = new Dictionary<int, Transform>();
        private readonly Vector3 initialGroupPosition;
        private bool following;

        internal OfficialBattleCameraView(Camera output, Transform worldRoot, int gameId,
            Vector3 cameraPosition, Func<int, Transform> unitRoot)
        {
            if (catalog == null)
            {
                TextAsset data = Resources.Load<TextAsset>("Combat/battle-camera-bindings");
                if (data == null) throw new InvalidDataException("Original battle camera bindings are missing.");
                catalog = JsonUtility.FromJson<Catalog>(data.text);
            }
            binding = Array.Find(catalog.cameras, value => value.gameId == gameId);
            if (binding?.baseAspect == null || binding.baseAspect.Length != 2 || binding.baseAspect[1] <= 0
                || binding.baseSize <= 0 || catalog.viewBound?.Length != 2)
                throw new InvalidDataException("Original battle camera settings are missing: " + gameId);
            this.unitRoot = unitRoot;
            root = new GameObject("Official Battle Camera " + gameId);
            root.SetActive(false);
            root.transform.SetParent(worldRoot, false);
            var cameraObject = new GameObject("CM vcam1");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = cameraPosition;
            cameraObject.transform.rotation = output.transform.rotation;
            virtualCamera = cameraObject.AddComponent<CinemachineVirtualCamera>();
            virtualCamera.m_Lens = binding.lens;
            float originalAspect = binding.baseAspect[0] / binding.baseAspect[1];
            virtualCamera.m_Lens.OrthographicSize = binding.baseSize
                * Mathf.Max(1f, originalAspect / output.aspect);
            virtualCamera.Priority = binding.priority;
            var target = new GameObject("TargetGroup");
            target.transform.SetParent(root.transform, false);
            initialGroupPosition = new Vector3(cameraPosition.x, cameraPosition.y, worldRoot.position.z);
            target.transform.position = initialGroupPosition;
            group = target.AddComponent<CinemachineTargetGroup>();
            JsonUtility.FromJsonOverwrite(binding.groupJson, group);
            virtualCamera.Follow = group.transform;
            framing = virtualCamera.AddCinemachineComponent<CinemachineFramingTransposer>();
            JsonUtility.FromJsonOverwrite(binding.framingJson, framing);
            framingMode = framing.m_GroupFramingMode;
            brain = output.GetComponent<CinemachineBrain>();
            addedBrain = brain == null;
            if (addedBrain) brain = output.gameObject.AddComponent<CinemachineBrain>();
            previousBrainEnabled = brain.enabled;
        }

        internal void Refresh(FightBattleCheckpoint checkpoint, FightBattlePhase phase)
        {
            bool follow = catalog.defaultFollow && phase != FightBattlePhase.Preparing;
            if (following != follow)
            {
                following = follow;
                framing.m_GroupFramingMode = follow ? framingMode : CinemachineFramingTransposer.FramingMode.None;
                if (!follow) group.transform.position = initialGroupPosition;
                virtualCamera.PreviousStateIsValid = false;
            }
            if (!follow) return;
            var targets = new List<CinemachineTargetGroup.Target>();
            Transform focus = unitRoot(1);
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                Transform model = unitRoot(unit.Id);
                if (model == null || !model.gameObject.activeInHierarchy || unit.Ended && unit.Hp > 0
                    || (!binding.includeDead && unit.Hp <= 0 && unit.Id != 1)) continue;
                if (focus != null)
                {
                    Vector3 delta = focus.position - model.position;
                    if (Mathf.Abs(delta.x) > catalog.viewBound[0] || Mathf.Abs(delta.y) > catalog.viewBound[1]) continue;
                }
                if (!followers.TryGetValue(unit.Id, out Transform follower))
                {
                    follower = new GameObject("CameraGroupFollow").transform;
                    follower.SetParent(root.transform, false);
                    followers.Add(unit.Id, follower);
                }
                follower.position = model.position + Vector3.up * binding.yOffset;
                float radius = .5f;
                foreach (FightGridPoint point in unit.BodyOffsets)
                    radius = Mathf.Max(radius, (Mathf.Abs(point.X) + 1) / 2f, (Mathf.Abs(point.Y) + 1) / 2f);
                targets.Add(new CinemachineTargetGroup.Target { target = follower, weight = 1f, radius = radius });
            }
            group.m_Targets = targets.ToArray();
        }

        internal void SetVisible(bool visible)
        {
            if (root == null) return;
            root.SetActive(visible);
            brain.enabled = visible || previousBrainEnabled && !addedBrain;
            if (visible) virtualCamera.PreviousStateIsValid = false;
        }

        public void Dispose()
        {
            if (brain != null)
            {
                brain.enabled = previousBrainEnabled && !addedBrain;
                if (addedBrain) OfficialBattleSceneBindings.DestroyCreatedObject(brain);
            }
            if (root != null) OfficialBattleSceneBindings.DestroyCreatedObject(root);
        }
    }
}
