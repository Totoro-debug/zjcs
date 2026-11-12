using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zjcs.Validation.Combat
{
    // Compiled only for Editor tests or an explicitly enabled validation Player.
    public sealed class OfficialBattlePublicPlayerValidation : MonoBehaviour
    {
        private string output;
        private Exception failure;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [UnityEngine.Scripting.Preserve]
        public static void StartDiagnostic()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-public-battle");
            if (index < 0) return;
            if (index + 1 >= args.Length) throw new ArgumentException("A validation output directory is required.");
            string output = Path.GetFullPath(args[index + 1]);
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(workspace)
                || output != Path.GetFullPath(workspace)
                || !File.Exists(Path.Combine(output, ".zjcs-validation.json")))
                throw new ArgumentException("Use the active marked validation workspace.");
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            AudioListener.volume = 0f;
            Application.runInBackground = true;
            var host = new GameObject("Public battle Player validation");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<OfficialBattlePublicPlayerValidation>();
            runner.output = output;
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run()
        {
            Application.logMessageReceived += Observe;
            var stack = new Stack<IEnumerator>();
            stack.Push(OfficialBattlePublicFlowValidation.Steps(name =>
                ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"))));
            float deadline = Time.realtimeSinceStartup + 300f;
            while (stack.Count > 0 && failure == null)
            {
                object next = null;
                try
                {
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Public battle Player validation timed out.");
                    var current = stack.Peek();
                    if (!current.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    next = current.Current;
                }
                catch (Exception error) { failure = error; }
                if (failure == null) yield return next;
            }
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception error) { if (failure == null) failure = error; }
            }
            yield return new WaitForEndOfFrame();
            Application.logMessageReceived -= Observe;
            File.WriteAllText(Path.Combine(output, "public-battle-result.json"),
                JsonUtility.ToJson(new Result { passed = failure == null, error = failure?.ToString() ?? "" }, true));
            if (failure != null) Debug.LogException(failure);
            Application.Quit(failure == null ? 0 : 1);
        }

        private void Observe(string message, string trace, LogType type)
        {
            if (failure == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                failure = new InvalidOperationException(message + "\n" + trace);
        }
    }
}
