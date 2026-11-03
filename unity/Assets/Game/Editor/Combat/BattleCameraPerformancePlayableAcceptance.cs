using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Editor.Combat
{
    [InitializeOnLoad]
    public static class BattleCameraPerformancePlayableAcceptance
    {
        private const string Key = "Zjcs.BattleCameraPerformancePlayableAcceptance";

        static BattleCameraPerformancePlayableAcceptance()
        {
            if (SessionState.GetBool(Key, false))
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update += Verify;
        }

        private static void Verify()
        {
            EditorApplication.update -= Verify;
            SessionState.SetBool(Key, false);
            GameObject root = null, source = null, prefab = null;
            OfflineBattleNativePlayableScope scope = null;
            try
            {
                root = new GameObject("Camera performance Play Mode acceptance");
                source = new GameObject("Original skill source");
                source.transform.SetParent(root.transform, false);
                source.transform.localPosition = new Vector3(2, 3, 4);
                var sound = new SoundHost();
                var camera = new CameraHost();
                scope = new OfflineBattleNativePlayableScope(root.transform, sound,
                    cameraPerformanceHost: camera);

                prefab = new GameObject("Original camera performance playable");
                var config = prefab.AddComponent<MyPlayableFightCameraPerformance>();
                config.Duration = 2;
                config.IsMoveSource = true;
                config.MoveOffsetOfSource = new Vector3(4, 5, 6);

                var playable = scope.Play(prefab, source, null);
                Require(source.transform.localPosition == new Vector3(1004, 5, 6),
                    "source moves to the configured performance position");
                Require(playable.transform.localPosition == Vector3.zero,
                    "playable without an apply component leaves its root unmoved");
                Require(playable.GetDuration() == 2, "configured duration is retained");
                Require(camera.Events.Count == 2 && camera.Events[0] == "camera-in"
                    && camera.Events[1] == "block:41", "camera enters before game-object input is blocked");

                scope.Stop(playable, true);
                scope.Tick(0.01f);
                scope.LateTick();
                Require(source.transform.localPosition == new Vector3(2, 3, 4),
                    "source local position is restored after stop; actual=" + source.transform.localPosition);
                Require(playable.transform.localPosition == Vector3.zero,
                    "playable root remains at its local origin after stop");
                Require(camera.Events.Count == 4 && camera.Events[2] == "camera-out"
                    && camera.Events[3] == "unblock:41", "camera and input are restored in order");

                AppearanceCombineSkin appearance = source.AddComponent<AppearanceCombineSkin>();
                prefab = new GameObject("Original appearance-combine-skin playable");
                prefab.SetActive(false);
                prefab.AddComponent<ApplyBySource>();
                var skinConfig = prefab.AddComponent<MyPlayableAppearanceCombineSkin>();
                skinConfig.Duration = 2;
                skinConfig.SkinOperates = new[]
                {
                    new MyPlayableAppearanceCombineSkin.SkinOperate
                        { SkinName = "body_armor_a", OperateType = MyPlayableAppearanceCombineSkin.OperateType.Add },
                    new MyPlayableAppearanceCombineSkin.SkinOperate
                        { SkinName = "head_hood_a", OperateType = MyPlayableAppearanceCombineSkin.OperateType.Add },
                    new MyPlayableAppearanceCombineSkin.SkinOperate
                        { SkinName = "body_armor_a", OperateType = MyPlayableAppearanceCombineSkin.OperateType.Remove }
                };
                MyPlayable skinPlayable = scope.Play(prefab, source, source);
                Require(appearance.CombineSkins.Count == 1 && appearance.CombineSkins[0] == "head_hood_a",
                    "skin operations execute in serialized order through the original AppearanceCombineSkin API");
                Require(skinPlayable.GetDuration() == 2, "appearance playable duration is retained");
                scope.Stop(skinPlayable, true);
                scope.Tick(0.01f); scope.LateTick();
                Require(skinPlayable.myStatus == MyPlayable.PlayableStatus.Stop,
                    "appearance playable completes the original stop lifecycle");

                Debug.Log("Skill visual Play Mode acceptance passed: camera position/switch/input-block lifecycle and ordered appearance skin add/remove.");
                scope.Dispose(); scope = null;
                UnityEngine.Object.DestroyImmediate(prefab); prefab = null;
                UnityEngine.Object.DestroyImmediate(source); source = null;
                UnityEngine.Object.DestroyImmediate(root); root = null;
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                scope?.Dispose();
                if (prefab) UnityEngine.Object.DestroyImmediate(prefab);
                if (source) UnityEngine.Object.DestroyImmediate(source);
                if (root) UnityEngine.Object.DestroyImmediate(root);
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Camera performance Play Mode acceptance: " + message);
        }

        private sealed class SoundHost : IProfessionSoundHost
        {
            public uint PostEvent(uint id, GameObject emitter) => 1;
        }

        private sealed class CameraHost : IBattleCameraPerformanceHost
        {
            public readonly List<string> Events = new List<string>();
            public long BlockGameObjectInput() { Events.Add("block:41"); return 41; }
            public void UnblockGameObjectInput(long operationId) => Events.Add("unblock:" + operationId);
            public void SwitchInPerformanceCamera() => Events.Add("camera-in");
            public void SwitchOutPerformanceCamera() => Events.Add("camera-out");
        }
    }
}
