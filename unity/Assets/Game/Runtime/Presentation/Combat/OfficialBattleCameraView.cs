using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    // Replays the original GameCameraIniter aspect adapter and group framing with the
    // installed Cinemachine implementation. Unit positions come from the same scene as their HUD.
    internal sealed class OfficialBattleCameraView : IDisposable, IBattleCameraOffsetHost
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
        private readonly CinemachineCameraOffset cameraOffset;
        private readonly CinemachineRecomposer recomposer;
        private readonly CinemachineFramingTransposer.FramingMode framingMode;
        private readonly CinemachineBrain brain;
        private readonly bool addedBrain, previousBrainEnabled;
        private readonly Func<int, Transform> unitRoot;
        private readonly Dictionary<int, Transform> followers = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Vector3> cameraOffsets = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, List<int>> cameraOffsetGroups = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, float> unitWeights = new Dictionary<int, float>();
        private readonly Dictionary<Transform, float> externalFollowers = new Dictionary<Transform, float>();
        private readonly Dictionary<int, Transform> unitRoots = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> cameraFollowers = new Dictionary<int, Transform>();
        private readonly Vector3 initialGroupPosition;
        private int nextCameraOffsetIndex;
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
            cameraOffset = cameraObject.AddComponent<CinemachineCameraOffset>();
            recomposer = cameraObject.AddComponent<CinemachineRecomposer>();
            recomposer.m_ZoomScale = 1f;
            brain = output.GetComponent<CinemachineBrain>();
            addedBrain = brain == null;
            if (addedBrain) brain = output.gameObject.AddComponent<CinemachineBrain>();
            previousBrainEnabled = brain.enabled;
        }

        public int RegisterCameraOffset(Vector3 offset, int group)
        {
            int index = ++nextCameraOffsetIndex;
            cameraOffsets.Add(index, offset);
            if (!cameraOffsetGroups.TryGetValue(group, out List<int> indexes))
                cameraOffsetGroups.Add(group, indexes = new List<int>());
            indexes.Add(index);
            ApplyCameraOffsets();
            return index;
        }

        public void SetCameraOffset(int index, Vector3 offset)
        {
            if (!cameraOffsets.ContainsKey(index)) return;
            cameraOffsets[index] = offset;
            ApplyCameraOffsets();
        }

        public void UnregisterCameraOffset(int index)
        {
            if (!cameraOffsets.Remove(index)) return;
            foreach (List<int> indexes in cameraOffsetGroups.Values) indexes.Remove(index);
            ApplyCameraOffsets();
        }

        public bool TryChangeFollowerWeight(Transform follower, float weight, out float previousWeight)
        {
            previousWeight = -1f;
            if (follower == null) return false;
            foreach (KeyValuePair<int, Transform> item in unitRoots)
            {
                if (item.Value != follower) continue;
                unitWeights.TryGetValue(item.Key, out previousWeight);
                if (!unitWeights.ContainsKey(item.Key)) previousWeight = 1f;
                unitWeights[item.Key] = weight;
                if (cameraFollowers.TryGetValue(item.Key, out Transform target))
                    SetTargetWeight(target, weight);
                return true;
            }
            if (!externalFollowers.TryGetValue(follower, out previousWeight)) return false;
            externalFollowers[follower] = weight;
            SetTargetWeight(follower, weight);
            return true;
        }

        public bool AddExternalFollower(Transform follower, float weight)
        {
            if (follower == null || externalFollowers.ContainsKey(follower)) return false;
            externalFollowers.Add(follower, weight);
            var targets = new List<CinemachineTargetGroup.Target>(group.m_Targets)
            {
                new CinemachineTargetGroup.Target { target = follower, weight = weight, radius = .5f }
            };
            group.m_Targets = targets.ToArray();
            return true;
        }

        public void RemoveExternalFollower(Transform follower)
        {
            if (follower == null || !externalFollowers.Remove(follower)) return;
            var targets = new List<CinemachineTargetGroup.Target>(group.m_Targets);
            targets.RemoveAll(value => value.target == follower);
            group.m_Targets = targets.ToArray();
        }

        private void SetTargetWeight(Transform target, float weight)
        {
            CinemachineTargetGroup.Target[] targets = group.m_Targets;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i].target != target) continue;
                targets[i].weight = weight;
                group.m_Targets = targets;
                return;
            }
        }

        private void ApplyCameraOffsets()
        {
            Vector3 total = Vector3.zero;
            foreach (KeyValuePair<int, List<int>> group in cameraOffsetGroups)
            {
                if (group.Key == 3)
                {
                    foreach (int index in group.Value)
                        if (cameraOffsets.TryGetValue(index, out Vector3 offset)) total += offset;
                }
                else if (group.Value.Count > 0
                    && cameraOffsets.TryGetValue(group.Value[group.Value.Count - 1], out Vector3 offset))
                    total += offset;
            }
            cameraOffset.m_Offset = new Vector3(total.x, total.y, 0f);
            recomposer.m_ZoomScale = total.z + 1f;
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
            unitRoots.Clear();
            cameraFollowers.Clear();
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
                unitRoots[model.GetInstanceID()] = model;
                cameraFollowers[model.GetInstanceID()] = follower;
                float radius = .5f;
                foreach (FightGridPoint point in unit.BodyOffsets)
                    radius = Mathf.Max(radius, (Mathf.Abs(point.X) + 1) / 2f, (Mathf.Abs(point.Y) + 1) / 2f);
                float weight = unitWeights.TryGetValue(model.GetInstanceID(), out float configuredWeight)
                    ? configuredWeight : 1f;
                targets.Add(new CinemachineTargetGroup.Target { target = follower, weight = weight, radius = radius });
            }
            foreach (KeyValuePair<Transform, float> external in externalFollowers)
                targets.Add(new CinemachineTargetGroup.Target { target = external.Key, weight = external.Value, radius = .5f });
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
