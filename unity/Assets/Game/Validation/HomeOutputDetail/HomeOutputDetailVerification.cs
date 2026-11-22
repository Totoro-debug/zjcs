using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeOutputDetailUI;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;
using Group = Zjcs.Official.HomeOutputAddition.OutputGroupType;

namespace Zjcs.Validation.HomeOutputDetail
{
    // Explicit diagnostic only; all records, input and files belong to its own
    // validation directory. The normal game never creates these test records.
    [UnityEngine.Scripting.Preserve]
    public sealed class HomeOutputDetailVerification : MonoBehaviour
    {
        const string Argument = "--zjcs-validate-home-output-detail";
        sealed class Pointer : IHomePointerSnapshot
        {
            public bool LeftDown { get; set; }
            public Vector2 Position { get; set; }
            public int Width => Screen.width;
            public int Height => Screen.height;
        }
        [Serializable] sealed class Result { public bool passed; public string error; public int checks; }
        HomeFeatureRuntime home;
        RenderTexture render;
        string output, saves, slot;
        int checks;
        readonly List<string> errors = new List<string>();
        readonly Pointer pointer = new Pointer();
        OfflineHomeHangUpWindows Windows => Field<OfflineHomeHangUpWindows>(home.Hud, "nativeBedWindows");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Argument);
            if (index < 0) return;
            if (index + 1 >= args.Length) throw new ArgumentException("Output-detail diagnostic requires an owned output directory.");
            string directory = Path.GetFullPath(args[index + 1]);
            if (!File.Exists(Path.Combine(directory, ".zjcs-validation.json")))
                throw new InvalidOperationException("Output-detail diagnostic requires an owned validation workspace.");
            var root = new GameObject("OriginalOutputDetailVerification");
            DontDestroyOnLoad(root);
            root.AddComponent<HomeOutputDetailVerification>().output = directory;
        }

        IEnumerator Start()
        {
            AudioListener.volume = 0;
            Application.runInBackground = true;
            Application.logMessageReceived += Observe;
            var stack = new Stack<IEnumerator>(); stack.Push(Check());
            Exception failure = null;
            while (stack.Count > 0)
            {
                object step = null;
                try
                {
                    if (!stack.Peek().MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    step = stack.Peek().Current;
                }
                catch (Exception error) { failure = error; break; }
                if (step is IEnumerator child) stack.Push(child);
                else yield return step;
            }
            if (failure == null && errors.Count != 0) failure = new Exception(string.Join("\n", errors));
            try { home?.Close(); if (render) { render.Release(); Destroy(render); } }
            catch (Exception error) { if (failure == null) failure = error; }
            Application.logMessageReceived -= Observe;
            File.WriteAllText(Path.Combine(output, "output-detail-result.json"), JsonUtility.ToJson(new Result {
                passed = failure == null, error = failure?.ToString(), checks = checks }, true));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(failure == null ? 0 : 1);
#else
            Application.Quit(failure == null ? 0 : 1);
#endif
        }

        IEnumerator Check()
        {
            saves = Path.Combine(output, "saves-" + Guid.NewGuid().ToString("N"));
            var creation = CharacterCreationOfflineService.OpenDefault(saves);
            slot = creation.CreateSlot();
            var draft = creation.OpenDraft(slot); draft.name = "家园测试";
            Require(creation.TryCreateCharacter(slot, draft, out string error), error);
            Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
            var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saves), slot);
            var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
            Require(profile.ApplyCharacterMutation(player => { player.Level = 3;
                player.Experience = character.CumulativeLevelExperience[3]; player.SubRank = 1; }, "detail:fixture:level").IsApplied, "isolated level fixture");
            Open(); yield return new WaitForSecondsRealtime(1);
            Require(home.TryRepair(0), "real bed repair transaction");
            home.Hud.CloseDialog();
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            var source = new OfflineHomeOutputAddition(home.Session, () => DateTime.UtcNow, _ => false);
            Require(source.Groups(Group.Bed).Count == 0 && source.ReadState().Records.Count == 0,
                "new profile does not synthesize activity records");
            var button = Windows.Bed.GetComponentInChildren<OutputAdditionDetailButton>(true);
            Require(button && !Field<GameObject>(button, "mContext").activeSelf, "original empty bonus context is hidden");
            Windows.CloseLast(); yield return new WaitForSecondsRealtime(.7f);
            var row = JObject.Parse(Resources.Load<TextAsset>("Home/output-addition-rules").text)["rows"]
                .First(value => (int)value["OutputGroupType"] == (int)Group.Bed && (int)value["OutputAdditionPropVal"] > 0);
            for (int id = 1; id <= 12; id++)
                Require(source.ApplyRecord(Record(id, row, DateTime.UtcNow.AddHours(1)), "detail:fixture:bonus:" + id), "durable bonus fixture");
            string saved = home.Session.Save.OutputAdditionStateJson;
            long experience = home.Profile.Save.Player.Experience;
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            Windows.OutputDetails.PointerSource = pointer;
            button = Windows.Bed.GetComponentInChildren<OutputAdditionDetailButton>(true);
            Require(Field<GameObject>(button, "mContext").activeSelf, "original bonus context observes saved records");
            var open = Field<Button>(button, "mBtn");
            if (!open.gameObject.activeInHierarchy)
            {
                Click(Windows.Bed.OutputFoldoutBtn);
                yield return new WaitForSecondsRealtime(.5f);
            }
            yield return WaitForButton(open);
            yield return Capture("detail-bed-entry.png");
            pointer.Position = Point(open.transform);
            Click(open); yield return new WaitForSecondsRealtime(.8f);
            var popup = Object.FindObjectOfType<PopTipOutputAddition>();
            Require(popup, "native bed button opens original detail popup");
            var scroll = Field<Zjcs.Official.SkillsUI.LoopVerticalScrollWithMaxHeight>(popup, "mScroll");
            Require(scroll.totalCount == 12 && popup.GetComponentsInChildren<OutputAdditionItem>().Length > 0,
                "original scroll receives all saved groups and binds visible rows");
            Require(Field<Image>(popup, "mBgImage").enabled, "original multi-group background");
            yield return Capture("detail-groups.png");
            Require(popup.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("%") && !text.text.Contains("{1}")),
                "original property row formats its configured percentage");
            Require(popup.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("小时") || text.text.Contains("分钟")),
                "original countdown resolves localized time units");
            float before = scroll.content.anchoredPosition.y;
            var wheel = new PointerEventData(EventSystem.current) { position = Point(scroll.transform), scrollDelta = new Vector2(0, -8) };
            ExecuteEvents.Execute(scroll.gameObject, wheel, ExecuteEvents.scrollHandler);
            yield return new WaitForSecondsRealtime(.4f);
            Require(Mathf.Abs(scroll.content.anchoredPosition.y - before) > .1f, "simulated wheel scrolls the original list");
            yield return Capture("detail-scrolled.png");
            pointer.Position = Point(Field<RectTransform>(popup, "mRect")); pointer.LeftDown = true;
            yield return null; pointer.LeftDown = false; yield return null;
            Require(popup && popup.gameObject.activeInHierarchy, "process-local inside pointer retains popup");
            pointer.Position = new Vector2(1, 1); pointer.LeftDown = true;
            yield return null; pointer.LeftDown = false; yield return new WaitForSecondsRealtime(.8f);
            Require(!Object.FindObjectOfType<PopTipOutputAddition>() && Windows.Bed,
                "original global pointer closes only the outer detail and retains bed");
            Require(saved == home.Session.Save.OutputAdditionStateJson && experience == home.Profile.Save.Player.Experience,
                "viewing, scrolling and closing grant no reward and do not rewrite activity records");
            home.Close(); home = null; yield return null;
            Open(); yield return new WaitForSecondsRealtime(.8f);
            source = new OfflineHomeOutputAddition(home.Session, () => DateTime.UtcNow, _ => false);
            Require(source.Groups(Group.Bed).Count == 12 && home.Session.Save.OutputAdditionStateJson == saved,
                "cold reopen restores the same durable activity records");
            foreach (var record in source.ReadState().Records)
            {
                record.EndTime = DateTime.UtcNow.AddSeconds(-1);
                Require(source.ApplyRecord(record, "detail:fixture:expire:" + record.Id), "expired-record fixture");
            }
            Require(source.ApplyRecord(Record(1, row, DateTime.UtcNow.AddHours(1)), "detail:fixture:single"), "single-record fixture");
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            Windows.OutputDetails.PointerSource = pointer;
            open = Field<Button>(Windows.Bed.GetComponentInChildren<OutputAdditionDetailButton>(true), "mBtn");
            if (!open.gameObject.activeInHierarchy)
            {
                Click(Windows.Bed.OutputFoldoutBtn);
                yield return new WaitForSecondsRealtime(.5f);
            }
            yield return WaitForButton(open);
            yield return Capture("detail-reopened-entry.png");
            pointer.Position = Point(open.transform); Click(open); yield return new WaitForSecondsRealtime(.8f);
            popup = Object.FindObjectOfType<PopTipOutputAddition>();
            Require(popup && !Field<Image>(popup, "mBgImage").enabled
                && Field<Zjcs.Official.SkillsUI.LoopVerticalScrollWithMaxHeight>(popup, "mScroll").totalCount == 1,
                "expired records are filtered and original single-group background is hidden");
            yield return Capture("detail-single.png");
            Require(errors.Count == 0, "no runtime error or assertion");
        }

        static PlayerOutputAdditionDataWrap Record(int id, JToken row, DateTime end)
        {
            var type = (OutputAdditionPropType)(int)row["OutputAdditionPropType"];
            return new PlayerOutputAdditionDataWrap { Id = id, StartTime = DateTime.UtcNow.AddHours(-1), EndTime = end,
                OutputActionType = (OutputActionType)(int)row["OutputActionType"],
                OutputAdditionPropDict = new Dictionary<OutputAdditionPropType, PlayerOutputAdditionPropTypeDataWrap> {
                    { type, new PlayerOutputAdditionPropTypeDataWrap { ClassId = (int)row["ClassId"], StartTime = DateTime.UtcNow.AddHours(-1), EndTime = end } } } };
        }
        void Open()
        {
            home = HomeFeatureRuntime.Begin(slot, saves, null);
            home.TutorialAutoStartEnabled = false;
            if (!render) render = new RenderTexture(Screen.width, Screen.height, 24);
            home.Scene.Camera.targetTexture = render;
            var canvas = home.Hud.CanvasTransform.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = home.Scene.Camera;
            canvas.planeDistance = 1;
        }
        Vector2 Point(Transform node) => RectTransformUtility.WorldToScreenPoint(home.Scene.Camera,
            node.TransformPoint(((RectTransform)node).rect.center));
        IEnumerator WaitForButton(Button button)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (Time.realtimeSinceStartup < deadline)
            {
                Canvas.ForceUpdateCanvases();
                var data = new PointerEventData(EventSystem.current) { position = Point(button.transform) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                if (hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)))
                    yield break;
                yield return null;
            }
            yield return Capture("detail-unreachable-button.png");
            Click(button);
        }
        void Click(Button button)
        {
            var data = new PointerEventData(EventSystem.current) { position = Point(button.transform), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Require(hits.Count > 0 && (hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),
                "simulated pointer reaches " + button.name + " at " + data.position + "; active=" + button.gameObject.activeInHierarchy
                + "; hits=" + string.Join(",", hits.Take(5).Select(value => value.gameObject.name)));
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
        }
        IEnumerator Capture(string name)
        {
            // A camera capture must emulate overlay canvases being above world
            // renderers. Preserve every production sorting value after capture.
            var canvases = home.Hud.CanvasTransform.GetComponentsInChildren<Canvas>(true);
            var layers = canvases.Select(value => value.sortingLayerID).ToArray();
            var orders = canvases.Select(value => value.sortingOrder).ToArray();
            try
            {
                foreach (var canvas in canvases)
                {
                    canvas.sortingLayerID = SortingLayer.layers.Last().id;
                    if (canvas.sortingOrder < 30000) canvas.sortingOrder += 30000;
                }
                Canvas.ForceUpdateCanvases();
                // SRP submits camera work during the frame; retain the capture
                // sorting until an actual rendered frame reaches the texture.
                yield return null;
                yield return null;
                var previous = RenderTexture.active; RenderTexture.active = render;
                var image = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG());
                RenderTexture.active = previous; Destroy(image);
            }
            finally
            {
                for (int index = 0; index < canvases.Length; index++)
                { canvases[index].sortingLayerID = layers[index]; canvases[index].sortingOrder = orders[index]; }
            }
        }
        static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        void Observe(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
    }
}
