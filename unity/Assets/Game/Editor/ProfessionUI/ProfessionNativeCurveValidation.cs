using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionNativeCurveValidation
    {
        private const string Active = "Zjcs.ProfessionNativeCurve.Active";
        private static bool ready;
        [Serializable] private sealed class Fixture { public Track[] curves; }
        [Serializable] private sealed class Track
        {
            public string sex, animation, bone, channel, axis;
            public ProfessionNativeCurve.Key[] keys;
            public Sample[] samples;
        }
        [Serializable] private sealed class Sample { public float time, value; public bool applies; }

        static ProfessionNativeCurveValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Application.runInBackground = true;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 120));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Native profession curve fixture did not finish.");
                if (!ready || !EditorApplication.isPlaying) return;
                Validate(); Finish(0);
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Validate()
        {
            var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(ProfessionValidationWorkspace.Fixture("native-curves-validation.json")));
            Require(fixture.curves != null && fixture.curves.Length > 1000, "complete six idle scalar timeline fixture");
            int samples = 0; float maxAbsolute = 0, maxNormalized = 0;
            foreach (var track in fixture.curves)
            {
                var curve = new ProfessionNativeCurve(track.keys);
                Require(curve.KeyCount == track.keys.Length, "retain every original source key");
                foreach (var sample in track.samples)
                {
                    bool applies = curve.TryEvaluate(sample.time, out float value);
                    string location = track.sex + "/" + track.animation + "/" + track.bone + "/" + track.channel + "/" + track.axis + " at " + sample.time;
                    Require(applies == sample.applies, "before-first-key non-application: " + location);
                    if (applies)
                    {
                        float difference = Mathf.Abs(value - sample.value);
                        maxAbsolute = Mathf.Max(maxAbsolute, difference);
                        maxNormalized = Mathf.Max(maxNormalized, difference / Mathf.Max(1, Mathf.Abs(sample.value)));
                        Require(difference <= .0002f + .000005f * Mathf.Abs(sample.value), "native curve differs from independently inverted source Bezier: " + location + "; actual=" + value + ", expected=" + sample.value + ", difference=" + difference);
                    }
                    samples++;
                }
            }
            ValidateBoundaries();
            Debug.Log("Profession native weighted curves passed: " + fixture.curves.Length + " original male/female idle scalar tracks; " + samples + " independent samples. Maximum absolute difference=" + maxAbsolute.ToString("R") + "; normalized=" + maxNormalized.ToString("R") + ". Numerical bone timelines only; character world poses, constraints, rendering and full UI are not covered.");
        }
        private static void ValidateBoundaries()
        {
            var key = new ProfessionNativeCurve.Key { time = 2, value = 7 };
            var constant = new ProfessionNativeCurve(new[] { key });
            key.value = 99;
            Require(!constant.TryEvaluate(1.999f, out _) && constant.TryEvaluate(2, out float at) && at == 7 && constant.TryEvaluate(300, out at) && at == 7, "constant source value, boundaries and immutable native copy");
            var stepped = new ProfessionNativeCurve(new[] { new ProfessionNativeCurve.Key { time = 0, value = 3, mode = 1 }, new ProfessionNativeCurve.Key { time = 1, value = -5 } });
            Require(stepped.TryEvaluate(.999f, out at) && at == 3 && stepped.TryEvaluate(1, out at) && at == -5, "step changes exactly on the next key");
            var linear = new ProfessionNativeCurve(new[] { new ProfessionNativeCurve.Key { time = 0, value = -2 }, new ProfessionNativeCurve.Key { time = 4, value = 6 } });
            Require(linear.TryEvaluate(1, out at) && Mathf.Abs(at) < .000001f, "linear segment is not smoothed");
            var cubic = new ProfessionNativeCurve(new[] { new ProfessionNativeCurve.Key { time = 0, value = 0, mode = 2, controls = new Vector4(.25f, 0, .75f, 1) }, new ProfessionNativeCurve.Key { time = 1, value = 1 } });
            Require(cubic.TryEvaluate(.5f, out at) && Mathf.Abs(at - .5f) < .000001f, "analytical symmetric cubic midpoint");
            Expect<ArgumentException>(() => new ProfessionNativeCurve(Array.Empty<ProfessionNativeCurve.Key>()));
            Expect<ArgumentException>(() => new ProfessionNativeCurve(new[] { key, key }));
            Expect<ArgumentException>(() => constant.TryEvaluate(float.NaN, out _));
            var vertical = new ProfessionNativeCurve(new[] { new ProfessionNativeCurve.Key { time = 0, mode = 2,
                controls = new Vector4(0, 1, .5f, 1) }, new ProfessionNativeCurve.Key { time = 1 } });
            Require(vertical.TryEvaluate(.0859375f, out at) && Mathf.Abs(at - .5625f) < .000001f,
                "vertical source handle retains the original cubic rather than being rejected");
        }
        private static void Expect<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected explicit source-curve rejection: " + typeof(T).Name); }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Native profession curves: " + message); }
        private static void Finish(int code)
        { SessionState.SetBool(Active, false); EditorApplication.Exit(code); }
    }
}
