using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Home;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    /// <summary>
    /// Play Mode acceptance driver for the home module.  It is only created when
    /// Unity is launched with ``-homeAcceptance`` and drives the real runtime with
    /// an isolated save, captures Game-view screenshots and writes a JSON report.
    /// </summary>
    public sealed class HomeAcceptanceDriver : MonoBehaviour
    {
        private static string Root => Path.Combine(Path.GetTempPath(), "zjcs-home-acceptance");
        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Output => Path.Combine(ProjectRoot, "output", "home-acceptance");

        private readonly List<string> log = new List<string>();
        private readonly Dictionary<string, object> report = new Dictionary<string, object>();

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Output);
            var slots = new ProfileSlotCatalog(Root);
            string slotId = slots.GetSlotIds().FirstOrDefault();
            HomeFeatureRuntime runtime = null;
            bool ok = true;
            string setupError = null;
            try
            {
                if (string.IsNullOrEmpty(slotId))
                    throw new InvalidOperationException("The acceptance profile was not created by the launcher.");
                runtime = HomeFeatureRuntime.Begin(slotId, Root, null, null);
            }
            catch (Exception error)
            {
                setupError = error.ToString();
                ok = false;
            }
            if (runtime == null)
            {
                log.Add("FATAL " + setupError);
                Finish(false);
                yield break;
            }
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            HideForeignCanvases();
            DumpFullscreen();
            ok &= Check("catalog", () =>
            {
                Assert(runtime.Catalog.Scene.background.Length == 384, "background layer count");
                Assert(runtime.Catalog.Scene.furniture.Length == 12, "furniture type count");
                Assert(runtime.Catalog.Scene.click_regions.Length == 16, "click region count");
                Assert(runtime.Catalog.Ui.panels.Length >= 40, "ui panel count");
                Assert(runtime.Rules.Data.items.Length == 12694, "item table size");
                return runtime.Catalog.Scene.background.Length + " layers, " +
                       runtime.Catalog.Ui.panels.Length + " panels, " + runtime.Rules.Data.items.Length + " items";
            });
            ok &= Check("scene", () => runtime.transform.childCount + " runtime roots");
            ok &= Check("repair", () =>
            {
                Assert(runtime.TryRepair(10), "cart repair");
                Assert(runtime.TryRepair(7), "storage box repair");
                Assert(runtime.TryRepair(0), "bed repair");
                Assert(runtime.Session.OwnsFurniture(0), "bed ownership persisted");
                return "bed/cart/storage repaired";
            });
            ok &= Check("income", () =>
            {
                runtime.Session.Mutate("acceptance:rewind", save =>
                    save.LastLocalUnixSeconds = Math.Max(0, save.LastLocalUnixSeconds - 3 * 3600));
                runtime.Session.AdvanceTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                HangUpIncomePreview preview = runtime.Session.Preview(1);
                int unlocked = runtime.Session.Definitions.Count(value => value.Unlocked);
                Assert(preview.Count > 0, "bed preview count=" + preview.Count + " defs="
                    + runtime.Session.Definitions.Count + " unlocked=" + unlocked
                    + " capacity=" + preview.Capacity + " hourly=" + preview.HourlyRate);
                long collected = runtime.TryCollect(1);
                Assert(collected > 0, "bed collect");
                return "preview " + preview.Count + ", collected " + collected;
            });
            ok &= Check("unlock-levelup", () =>
            {
                int hangUpId = runtime.Rules.HangUpsOf(10).Select(value => value.id)
                    .FirstOrDefault(id => runtime.Rules.HangUp(id).canLevelUp);
                Assert(hangUpId > 0, "levelable cart hang-up");
                if (!runtime.Session.IsHangUpUnlocked(hangUpId))
                    runtime.TryUnlockHangUp(hangUpId);
                HomeLevelUpResult result = runtime.TryLevelUpHangUp(hangUpId);
                Assert(result.Success || result.Error == "items" || result.Error == "home_level",
                    "level up attempt: " + result.Error);
                return "hang-up " + hangUpId + " level " + runtime.Session.HangUpLevel(hangUpId);
            });
            ok &= Check("environment", () =>
            {
                Assert(runtime.Environment != null, "environment view");
                Assert(runtime.Environment.Config.stages.Length >= 4, "day stages");
                Assert(runtime.Environment.Config.lightTimes.Length >= 2, "light times");
                return "stage " + runtime.Environment.Stage + " time " + runtime.Environment.Hour + ":" +
                       runtime.Environment.Minute + " moon " + runtime.Environment.MoonPhase;
            });
            ok &= Check("input", () =>
            {
                int bed = runtime.Scene.HitTest(new Vector2(8.4f, -1.62f));
                int cart = runtime.Scene.HitTest(new Vector2(-8.9f, -4.5f));
                Assert(bed == 0, "bed hit=" + bed);
                Assert(cart == 10, "cart hit=" + cart);
                Assert(runtime.Scene.HitTest(new Vector2(0f, 40f)) == -1, "empty area must not hit");
                Vector3 start = runtime.Scene.Position;
                runtime.Scene.MoveCamera(new Vector2(-100000f, 0f));
                Vector3 right = runtime.Scene.Position;
                Assert(right.x > start.x, "drag moves camera");
                runtime.Scene.MoveCamera(new Vector2(200000f, 0f));
                Vector3 back = runtime.Scene.Position;
                Assert(back.x <= right.x, "clamped drag");
                return "bed/cart quads ok, clamp x " + right.x.ToString("0.00") + " -> " + back.x.ToString("0.00");
            });            yield return CheckUi(runtime, value => ok &= value);
            ok &= Check("persistence", () =>
            {
                string homePath = slots.GetHomePath(slotId);
                Assert(File.Exists(homePath), "home save exists");
                OfflineHomeSave saved = JsonUtility.FromJson<OfflineHomeSave>(File.ReadAllText(homePath));
                Assert(saved != null && saved.Furniture.Any(value => value.Type == 0 && value.Owned),
                    "furniture ownership written");
                Assert(saved.Income.Count > 0, "income checkpoint written");
                return "save bytes " + new FileInfo(homePath).Length;
            });
            Finish(ok);
        }

        private static void HideForeignCanvases()
        {
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (canvas == null || canvas.name == "Home Canvas") continue;
                string name = canvas.name;
                if (name.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("Opening", StringComparison.OrdinalIgnoreCase) < 0) continue;
                canvas.gameObject.SetActive(false);
            }
        }
        private void DumpFullscreen()
        {
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (image == null || !image.isActiveAndEnabled || image.color.a < 0.5f) continue;
                RectTransform rect = image.rectTransform;
                if (rect == null || rect.rect.width < 900 || rect.rect.height < 1200) continue;
                log.Add("IMG " + PathOf(rect) + " sprite=" + (image.sprite == null ? "null" : image.sprite.name)
                        + " color=" + image.color + " size=" + rect.rect.width + "x" + rect.rect.height);
            }
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera != null && camera.name == "Home Camera")
                    log.Add("CAM clear=" + camera.clearFlags + " bg=" + camera.backgroundColor
                            + " pos=" + camera.transform.position);
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
        private void Finish(bool ok)
        {
            report["success"] = ok;
            report["log"] = log;
            File.WriteAllText(Path.Combine(Output, "report.json"),
                JsonUtility.ToJson(new SerializedReport { success = ok, lines = log.ToArray() }, true));
            Debug.Log("[HomeAcceptance] success=" + ok + "\n" + string.Join("\n", log));
            Quit(ok ? 0 : 1);
        }
        private IEnumerator CheckUi(HomeFeatureRuntime runtime, Action<bool> result)
        {
            bool ok = true;
            ok &= Ui(() => runtime.Hud.ShowBed(), "open bed");
            yield return null;
            yield return null;
            Capture("home-bed.png");
            ok &= Ui(() =>
            {
                Assert(runtime.Hud.AnyModalOpen, "bed modal");
                runtime.Hud.CloseDialog();
                runtime.Hud.ShowCart();
            }, "open cart");
            yield return null;
            yield return null;
            Capture("home-cart.png");
            ok &= Ui(() =>
            {
                runtime.Hud.CloseDialog();
                runtime.Hud.ShowMail();
            }, "open mail");
            yield return null;
            yield return null;
            Capture("home-mail.png");
            ok &= Ui(() =>
            {
                runtime.Hud.CloseDialog();
                runtime.Hud.ToggleFunctions(true);
            }, "open functions");
            yield return null;
            Capture("home-functions.png");
            ok &= Ui(() =>
            {
                runtime.Hud.ToggleFunctions(false);
            }, "close functions");
            yield return null;
            Capture("home-scene.png");
            if (ok) log.Add("PASS ui: bed/cart/mail/functions screenshots");
            result(ok);
        }

        private bool Ui(Action action, string name)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception error)
            {
                log.Add("FAIL ui/" + name + ": " + error.Message);
                return false;
            }
        }
        private bool Check(string name, Func<object> body)
        {
            try
            {
                object value = body();
                log.Add("PASS " + name + ": " + value);
                return true;
            }
            catch (Exception error)
            {
                log.Add("FAIL " + name + ": " + error.Message);
                return false;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Capture(string file)
        {
            string path = Path.Combine(Output, file);
            RenderTexture target = null;
            Camera capture = null;
            var changed = new List<Canvas>();
            try
            {
                Camera source = FindSceneCamera();
                if (source == null) throw new InvalidOperationException("no scene camera");
                target = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
                var go = new GameObject("AcceptanceCaptureCamera");
                capture = go.AddComponent<Camera>();
                capture.CopyFrom(source);
                capture.targetTexture = target;
                capture.depth = source.depth + 10;
                capture.clearFlags = CameraClearFlags.SolidColor;
                capture.backgroundColor = source.backgroundColor;
                foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                {
                    if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    changed.Add(canvas);
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = capture;
                    canvas.planeDistance = 1f;
                }
                capture.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(1080, 1920, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[HomeAcceptance] screenshot failed: " + error.Message);
            }
            finally
            {
                foreach (Canvas canvas in changed)
                {
                    if (canvas == null) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                RenderTexture.active = null;
                if (capture != null)
                {
                    capture.targetTexture = null;
                    UnityEngine.Object.Destroy(capture.gameObject);
                }
                if (target != null) UnityEngine.Object.Destroy(target);
            }
            Debug.Log("[HomeAcceptance] screenshot " + path + " exists=" + File.Exists(path));
        }

        private static Camera FindSceneCamera()
        {
            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
                if (camera != null && camera.name == "Home Camera") return camera;
            return Camera.main;
        }
        private static void Quit(int code)
        {
            Type application = Type.GetType("UnityEditor.EditorApplication, UnityEditor");
            if (application != null)
            {
                PropertyInfo playing = application.GetProperty("isPlaying", BindingFlags.Public | BindingFlags.Static);
                if (playing != null) playing.SetValue(null, false);
                MethodInfo exit = application.GetMethod("Exit", BindingFlags.Public | BindingFlags.Static);
                if (exit != null)
                {
                    exit.Invoke(null, new object[] { code });
                    return;
                }
            }
            Application.Quit(code);
        }
    }

    [Serializable]
    public sealed class SerializedReport
    {
        public bool success;
        public string[] lines;
    }

    public static class HomeAcceptanceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Any(value =>
                    string.Equals(value, "-homeAcceptance", StringComparison.OrdinalIgnoreCase))) return;
            if (UnityEngine.Object.FindObjectOfType<HomeAcceptanceDriver>() != null) return;
            var go = new GameObject("HomeAcceptanceDriver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<HomeAcceptanceDriver>();
        }
    }
}
