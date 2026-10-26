using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.State;
using Zjcs.Presentation.Opening;

namespace Zjcs.Presentation.CharacterCreation
{
    // Explicit diagnostic invocation uses its own profiles, never the user's saves.
    public sealed class SaveSelectionProbe : MonoBehaviour
    {
        private static string output;
        private static bool restart;
        public static bool Finished { get; private set; }
        public static bool Passed { get; private set; }
        public static Action<int, int> ResizeView;
        public static string ProfileRoot => output == null ? Application.persistentDataPath : Path.Combine(output, "profiles");
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            output = null;
            Finished = Passed = false;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-saves");
            if (index >= 0 && index + 1 < args.Length) output = Path.GetFullPath(args[index + 1]);
#if UNITY_EDITOR
            output = Environment.GetEnvironmentVariable("ZJCS_SAVE_SELECTION_PROBE_OUTPUT") ?? output;
#endif
            if (output == null) return;
            Application.runInBackground = true;
            restart = File.Exists(Path.Combine(output, "result.json"));
            Directory.CreateDirectory(output);
            if (!File.Exists(Path.Combine(output, "fixtures-ready"))) PrepareProfiles();
            var host = new GameObject("SaveSelectionProbe");
            DontDestroyOnLoad(host);
            var probe = host.AddComponent<SaveSelectionProbe>();
            probe.deadline = Time.realtimeSinceStartup + 120f;
            probe.StartCoroutine(probe.Guard(probe.Run()));
        }

        private static void PrepareProfiles()
        {
            var service = CharacterCreationOfflineService.OpenDefault(ProfileRoot);
            for (int i = 1; i <= 15; i++)
            {
                string id = service.CreateSlot();
                if (i > 2) continue;
                CharacterCreationDraft draft = service.OpenDraft(id);
                draft.name = i == 1 ? "测试勇者" : "测试法师";
                draft.body_type = i == 1 ? "male" : "female";
                if (!service.TryCreateCharacter(id, draft, out string error)) throw new InvalidOperationException(error);
                if (!service.TrySetInitialWeapon(id, CharacterCreationOfflineService.WandInitialWeaponType, out error)) throw new InvalidOperationException(error);
            }
            var slots = new ProfileSlotCatalog(ProfileRoot);
            File.WriteAllText(slots.GetProgressPath("slot-4"), "{corrupt profile fixture");
            ProfileSelectionPreference.Write(ProfileRoot, "slot-2");
            File.WriteAllText(Path.Combine(output, "fixtures-ready"), "ready");
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object next;
                try
                {
                    Require(Time.realtimeSinceStartup < deadline, "Save selection probe timed out.");
                    if (!routine.MoveNext()) break;
                    next = routine.Current;
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString());
                    Complete(false);
                    yield break;
                }
                yield return next;
            }
        }

        private IEnumerator Run()
        {
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12, "Save selection validation requires DX12.");
            Dictionary<string, string> before = HashProfiles();
            OfficialSaveSelectionView view = null;
            while (view == null || !view.IsReady) { view = FindObjectOfType<OfficialSaveSelectionView>(); yield return null; }
            yield return new WaitForEndOfFrame();
            Capture(restart ? "restart-saves" : "saves");
            if (restart)
            {
                Require(view.Panel.RoleItemSlot.childCount == 16, "Cold restart lost profiles.");
                Require(view.Panel.RoleItemSlot.GetChild(0).name == "SaveSlot_slot-16", "Cold restart lost the last selected draft.");
                Require(HashProfiles().OrderBy(p => p.Key).SequenceEqual(before.OrderBy(p => p.Key)), "Cold restart changed profiles before selection.");
                CharacterOpeningFlow.SkipOpeningForAcceptance = true;
                Click(view.Panel.RoleItemSlot.GetChild(0).gameObject);
                yield return null;
                Require(GameObject.Find("OfficialCreatePlayerPanel") != null, "Cold restart did not resume the saved draft.");
                yield return new WaitForEndOfFrame(); Capture("restart-resumed");
                File.WriteAllText(Path.Combine(output, "restart-result.json"), "{\"passed\":true,\"dx12\":true,\"draftRecovered\":true}");
                Complete(true);
                yield break;
            }
            Require(view.Panel.RoleItemSlot.childCount == 15, "Profile list lost rows.");
            Require(view.Panel.RoleItemSlot.GetChild(0).name == "SaveSlot_slot-2", "Most recent profile was not restored.");
            foreach (string id in new[] { "slot-1", "slot-2" })
            {
                var row = view.Panel.RoleItemSlot.Find("SaveSlot_" + id);
                RawImage image = row.Find("PublicPlayerIcon/IconGo/ImgMask/PlayerHeadGo/HeadImg").GetComponent<RawImage>();
                Require(image.texture is Texture2D texture && texture.width == 100 && texture.height == 120, "Original head photography was not rendered.");
                Require(image.uvRect == new Rect(0, 0, 1, 1), "Original head photography UV was not restored.");
            }
            if (ResizeView != null)
            {
                foreach (Vector2Int size in new[] { new Vector2Int(675, 1200), new Vector2Int(900, 800), new Vector2Int(360, 1000), new Vector2Int(450, 800) })
                {
                    ResizeView(size.x, size.y);
                    yield return null; yield return null; yield return null;
                    yield return new WaitForEndOfFrame(); Capture("size-" + size.x + "x" + size.y);
                    var corners = new Vector3[4]; view.Panel.GetComponent<RectTransform>().GetWorldCorners(corners);
                    Require(corners.All(p => p.x >= -1 && p.x <= Screen.width + 1 && p.y >= -1 && p.y <= Screen.height + 1), "Save panel extends beyond the window.");
                }
            }
            GameObject tab = view.Panel.transform.Find("BgDialog1 (1)/Content/TabGroup/TabGroup/TabGroup/GiftMailDialogTabGroupItem").gameObject;
            Click(tab);
            yield return new WaitForSecondsRealtime(.4f);
            Require(view.Panel.IsCreatePage, "New profile tab did not respond to simulated click.");
            yield return new WaitForEndOfFrame(); Capture("new-save");
            Click(view.Panel.transform.Find("BgDialog1 (1)/Content/TabGroup/TabGroup/TabGroup/NormalMailDialogTabGroupItem").gameObject);
            yield return new WaitForSecondsRealtime(.4f);
            Require(!view.Panel.IsCreatePage, "Existing profile tab did not respond.");
            var scroll = view.Panel.RoleItemSlot.GetComponentInParent<ScrollRect>();
            Scroll(scroll.gameObject, -14);
            yield return new WaitForSecondsRealtime(.3f);
            Require(scroll.verticalNormalizedPosition < .9f, "Profile scroll did not move through simulated input.");
            yield return new WaitForEndOfFrame(); Capture("scrolled");
            Scroll(scroll.gameObject, 100);
            yield return new WaitForSecondsRealtime(.3f);
            Require(HashProfiles().OrderBy(p => p.Key).SequenceEqual(before.OrderBy(p => p.Key)), "Browsing profiles wrote to save files.");
            Click(view.Panel.RoleItemSlot.Find("SaveSlot_slot-2").gameObject);
            yield return null;
            Require(GameObject.Find("Screen_角色创建完成") != null, "Existing completed profile did not resume.");
            yield return new WaitForEndOfFrame(); Capture("continued");
            Click(FindButton("返回存档"));
            view = null;
            while (view == null || !view.IsReady) { view = FindObjectOfType<OfficialSaveSelectionView>(); yield return null; }
            Click(view.Panel.RoleItemSlot.Find("SaveSlot_slot-4").gameObject);
            yield return new WaitForSecondsRealtime(.4f);
            view = FindObjectOfType<OfficialSaveSelectionView>();
            while (!view.IsReady) yield return null;
            Require(view.Panel.MessageText.text.Contains("原文件已保留"), "Corrupt profile did not show a readable error.");
            Require(File.ReadAllText(new ProfileSlotCatalog(ProfileRoot).GetProgressPath("slot-4")) == "{corrupt profile fixture", "Corrupt profile bytes were overwritten.");
            yield return new WaitForEndOfFrame(); Capture("corrupt");
            Click(view.Panel.CreateButton.gameObject);
            yield return new WaitForSecondsRealtime(.4f);
            CharacterOpeningFlow.SkipOpeningForAcceptance = true;
            Click(view.Panel.transform.Find("BgDialog1 (1)/Content/TabGroup/TabGroup/TabGroup/GiftMailDialogTabGroupItem").gameObject);
            yield return new WaitForSecondsRealtime(.3f);
            int count = new ProfileSlotCatalog(ProfileRoot).GetSlotIds().Count;
            Click(view.Panel.CreateButton.gameObject);
            yield return null;
            Require(new ProfileSlotCatalog(ProfileRoot).GetSlotIds().Count == count + 1 && GameObject.Find("OfficialCreatePlayerPanel") != null,
                "New profile button did not create an independent draft and enter creation.");
            yield return new WaitForEndOfFrame(); Capture("created");
            Debug.Log("Save selection validation passed: native UI, portraits, tabs, simulated scroll/click, no browse writes, continue, corrupt profile preservation and creation.");
            File.WriteAllText(Path.Combine(output, "result.json"), "{\"passed\":true,\"dx12\":true,\"simulatedInput\":true}");
            Complete(true);
        }

        private static GameObject FindButton(string text, bool required = true)
        {
            var button = Resources.FindObjectsOfTypeAll<Button>().FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<TMP_Text>()?.text == text);
            if (required) Require(button != null, "Missing button " + text);
            return button == null ? null : button.gameObject;
        }
        private static void Click(GameObject target)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = target.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No pointer hit for " + target.name);
            GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Require(receiver == target, "Pointer obstructed for " + target.name + " by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void Scroll(GameObject target, float amount)
        {
            RectTransform rect = target.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, amount),
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No scroll pointer hit.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.scrollHandler);
        }
        private static Dictionary<string, string> HashProfiles()
        {
            using (var sha = SHA256.Create()) return Directory.GetFiles(ProfileRoot, "*", SearchOption.AllDirectories)
                .ToDictionary(p => p, p => BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))));
        }
        private void Capture(string name)
        {
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            Destroy(texture);
        }
        public static void RecordPortrait(string id, Texture2D texture)
        {
            if (output != null) File.WriteAllBytes(Path.Combine(output, id + "-head.png"), texture.EncodeToPNG());
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Complete(bool passed)
        {
            Passed = passed;
            Finished = true;
            if (!Application.isEditor) Application.Quit(passed ? 0 : 1);
        }
    }
}
