using System;
using System.Reflection;
using Cinemachine;
using UnityEditor;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    // Exercises the production camera constructor and real Play Mode updates.
    // The two position-only targets isolate framing from character rendering.
    [InitializeOnLoad]
    public static class OfficialBattleCameraTrackingAcceptance
    {
        private const string Key = "Zjcs.BattleCameraTracking";
        private static GameObject root, outputObject;
        private static IDisposable view;
        private static Camera output;
        private static Transform player;
        private static int startFrame, lastFrame, stage;
        private static Vector3 before;
        private static float previousDelta;
        private static Action refresh;

        static OfficialBattleCameraTrackingAcceptance()
        {
            if (SessionState.GetBool(Key, false))
                EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            EditorApplication.playModeStateChanged -= OnPlayMode;
            previousDelta = CinemachineCore.UniformDeltaTimeOverride;
            try
            {
                root = new GameObject("Production camera tracking fixture");
                player = new GameObject("Moving player target").transform;
                player.SetParent(root.transform, false);
                player.position = new Vector3(1.5f, -2, 0);
                var boss = new GameObject("Five by five boss target").transform;
                boss.SetParent(root.transform, false);
                boss.position = new Vector3(4.5f, 1, 0);
                outputObject = new GameObject("Tracking output");
                output = outputObject.AddComponent<Camera>();
                output.enabled = false;
                output.orthographic = true;
                output.aspect = 675f / 1463;
                output.transform.position = new Vector3(3, -.5f, -20);
                const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
                var type = typeof(OfficialBattleRuntimeController).Assembly.GetType(
                    "Zjcs.Presentation.Combat.OfficialBattleCameraView", true);
                view = (IDisposable)Activator.CreateInstance(type, hidden, null,
                    new object[] { output, root.transform, 902, output.transform.position,
                        new Func<int, Transform>(id => id == 1 ? player : boss) }, null);
                var checkpoint = new FightBattleCheckpoint();
                checkpoint.Units.Add(new FightBattleUnitState { Id = 1, Hp = 100 });
                var monster = new FightBattleUnitState { Id = 2, Hp = 100 };
                monster.BodyOffsets.Clear();
                for (int x = -2; x <= 2; x++)
                    for (int y = -2; y <= 2; y++) monster.BodyOffsets.Add(new FightGridPoint(x, y));
                checkpoint.Units.Add(monster);
                type.GetMethod("SetVisible", hidden).Invoke(view, new object[] { true });
                refresh = () => type.GetMethod("Refresh", hidden).Invoke(view,
                    new object[] { checkpoint, FightBattlePhase.Play });
                refresh();
                CinemachineCore.UniformDeltaTimeOverride = .02f;
                startFrame = lastFrame = Time.frameCount;
                stage = 0;
                EditorApplication.update += Observe;
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Observe()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (Time.frameCount - startFrame < 240) return;
                if (stage++ == 0)
                {
                    before = output.transform.position;
                    player.position += Vector3.left * 4;
                    refresh();
                    startFrame = Time.frameCount;
                    return;
                }
                Vector3 projected = output.WorldToViewportPoint(player.position);
                if (before.x - output.transform.position.x < .5f
                    || projected.x < 0 || projected.x > 1 || projected.z <= 0)
                    throw new InvalidOperationException("Production group camera did not follow the moving player: before="
                        + before + ", after=" + output.transform.position + ", player viewport=" + projected);
                Debug.Log("[Battle camera tracking] Runtime-created production camera follows a moving player alongside a five by five boss; player remains in the viewport. before="
                    + before + ", after=" + output.transform.position + ", viewport=" + projected);
                Finish(null);
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Finish(Exception exception)
        {
            EditorApplication.update -= Observe;
            CinemachineCore.UniformDeltaTimeOverride = previousDelta;
            view?.Dispose(); view = null;
            refresh = null;
            if (outputObject) UnityEngine.Object.DestroyImmediate(outputObject);
            if (root) UnityEngine.Object.DestroyImmediate(root);
            if (exception != null) Debug.LogException(exception);
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exception == null ? 0 : 1);
        }
    }
}
