using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Offline.Startup
{
    // Explicit diagnostic invocation validates the exported Player through its
    // own EventSystem, including on desktops that reject foreground activation.
    public sealed class StartupPlayerValidation : MonoBehaviour
    {
        private string output;
        private float deadline;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-startup");
            if (index < 0 || index + 1 >= args.Length) return;
            var host = new GameObject("StartupPlayerValidation");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<StartupPlayerValidation>();
            runner.output = Path.GetFullPath(args[index + 1]);
            runner.deadline = Time.realtimeSinceStartup + 60f;
            Directory.CreateDirectory(runner.output);
            runner.StartCoroutine(runner.Guard(runner.Run()));
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object next;
                try
                {
                    Require(Time.realtimeSinceStartup < deadline, "Player startup validation timed out.");
                    if (!routine.MoveNext()) break;
                    next = routine.Current;
                }
                catch (Exception error) { Debug.LogException(error); Application.Quit(1); yield break; }
                yield return next;
            }
        }
        private IEnumerator Run()
        {
            OfflineStartupFlow flow = null;
            while (flow == null) { flow = FindObjectOfType<OfflineStartupFlow>(); yield return null; }
            Require(flow.Phase == "BootSplash", "Player bypassed the original splash.");
            yield return new WaitForEndOfFrame(); Capture("boot");
            while (flow.Phase == "BootSplash") yield return null;
            Click(FindButton("AgeTipBtn"));
            yield return null;
            Require(flow.HasOpenDialog, "Player age-tip pointer click did not open the original dialog.");
            while (flow.Phase != "Ready") yield return null;
            Require(flow.Progress == 1f, "Player did not finish local resource loading.");
            yield return new WaitForEndOfFrame(); Capture("age");
            Click(FindButton("CommonBtn_12"));
            yield return null; yield return new WaitForEndOfFrame(); Capture("loading-bar");
            while (GameObject.Find("CharacterCreationCanvas") == null) yield return null;
            yield return null; yield return new WaitForEndOfFrame(); Capture("save-selector");
            Require(FindObjectOfType<OfflineStartupFlow>() == null, "Player retained the startup input layer.");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"pointer\":\"EventSystem raycast/down/up/click\",\"saveWrites\":false}");
            Debug.Log("Startup Windows Player validation passed: original splash/local progress, simulated age dialog open/close, local save-selector handoff.");
            Application.Quit(0);
        }
        private static GameObject FindButton(string name) => Resources.FindObjectsOfTypeAll<Button>()
            .First(b => b.name == name && b.gameObject.activeInHierarchy).gameObject;
        private static void Click(GameObject control)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = control.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No Player UI hit for " + control.name);
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Require(receiver == control, "Player UI is obstructed: " + control.name);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }
        private void Capture(string name)
        {
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
