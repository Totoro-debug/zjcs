using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleWeaponHostMotionNumericalValidation
    {
        public static void Run()
        {
            try { Check(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Check()
        {
            string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "weapon-host-motion-observations.json");
            var observations = JsonUtility.FromJson<OfficialBattleWeaponHostMotionReferenceCapture.Observations>(File.ReadAllText(path));
            int frames = 0; float maximum = 0;
            foreach (var row in observations.cases)
            {
                var go = new GameObject("Independent native host observation check");
                try
                {
                    var motion = new ProfessionNativeScenePhysicsMovement(); motion.Reset(go.transform);
                    var spring = new ProfessionNativeSpringConstraint();
                    var setup = new ProfessionNativeSpringConstraint.Affine { A = 1, B = -4.371139E-08f, D = 1, X = 20, Y = 30, Length = 100 };
                    var p = new ProfessionNativeSpringConstraint.Parameters { Step = 1f / 60, Limit = 5000,
                        Inertia = .5f, Strength = 100, Damping = .85f, InverseMass = 1, Mix = 1, Turn = 1, Reference = 100, X = 1, Y = 1 };
                    spring.Reset(setup, 0); float clock = 0;
                    foreach (var frame in row.frames)
                    {
                        go.transform.localScale = frame.scale;
                        go.transform.SetPositionAndRotation(frame.position, Quaternion.Euler(0, 0, frame.rotation));
                        motion.Apply(go.transform, .01f, row.factor, row.rotationFactor, spring.Translate, spring.Rotate);
                        clock += frame.delta;
                        var result = spring.Evaluate(setup, p, clock);
                        var actual = new[] { result.A, result.B, result.C, result.D, result.X * .01f, result.Y * .01f };
                        for (int i = 0; i < actual.Length; i++)
                        {
                            float error = Mathf.Abs(actual[i] - frame.pose[i]);
                            if (error > .0002f || float.IsNaN(error)) throw new InvalidOperationException(
                                "Host motion observation differs: " + row.name + ", frame " + frames % row.frames.Length
                                + ", component " + i + ", actual " + actual[i].ToString("R") + ", expected " + frame.pose[i].ToString("R"));
                            maximum = Mathf.Max(maximum, error);
                        }
                        frames++;
                    }
                }
                finally { Object.DestroyImmediate(go); }
            }
            if (observations.cases.Length != 15 || frames != 480) throw new InvalidOperationException("Host observation coverage differs.");
            Debug.Log("Independent weapon host observations passed: 15 cases, 480 frames, maximum error " + maximum.ToString("R"));
        }
    }
}
