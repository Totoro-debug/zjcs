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
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.HomeOutputAddition;
using Zjcs.Official.HomeOutputDetailUI;
using Zjcs.Official.HomeFastHangUpUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Home;
using Object = UnityEngine.Object;
using Group = Zjcs.Official.HomeOutputAddition.OutputGroupType;

namespace Zjcs.Validation.HomeOutputDetail
{
    // Explicit diagnostic only; all records, input and files belong to its own
    // validation directory. The normal game never creates these test records.
    [UnityEngine.Scripting.Preserve]
    public sealed partial class HomeOutputDetailVerification : MonoBehaviour
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
            yield return CheckBedCollection();
            yield return CheckBedFreeAcceleration();
            yield return CheckBedItemAcceleration();
            yield return CheckBedActivityIncome();
            yield return CheckBedCapacityExpiry();
            Require(errors.Count == 0, "no runtime error or assertion");
        }

        IEnumerator CheckBedCollection()
        {
            home.Hud.CloseDialog(); yield return null;
            var source = new OfflineHomeOutputAddition(home.Session, () => DateTime.UtcNow, _ => false);
            foreach (var record in source.ReadState().Records)
            {
                record.EndTime = DateTime.UtcNow.AddSeconds(-1);
                Require(source.ApplyRecord(record, "bed:fixture:expire:" + record.Id), "ordinary bed fixture expires activity records");
            }
            Require(home.Session.Mutate("bed:fixture:stored", save => {
                save.Income.RemoveAll(value => value.HangUpId == 1);
                save.Income.Add(new OfflineHomeIncomeEntry { HangUpId = 1, SettledCount = 1 });
            }), "isolated settled bed income");
            long before = home.Profile.Save.Player.Experience;
            long collected = 0; int published = 0;
            home.HangUpCollectedForTutorial += (id, count, receipt) => { if (id == 1) { collected += count; published++; } };
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            var bed = Windows.Bed;
            yield return WaitForButton(bed.GetHangUpRewardBtn);
            Require(bed.ImageSlot.childCount == 1 && bed.Name.GetComponent<TMP_Text>().text == "床",
                "bed displays its original bound model and item name");
            bool levelFoldout = bed.LevelFoldout;
            yield return WaitForButton(bed.LevelFoldoutBtn); Click(bed.LevelFoldoutBtn);
            Require(bed.LevelFoldout != levelFoldout, "native blessing foldout responds to simulated pointer");
            Click(bed.LevelFoldoutBtn);
            yield return WaitForButton(bed.GetHangUpRewardBtn); Click(bed.GetHangUpRewardBtn);
            yield return null;
            Require(published == 1 && collected == 1, "native collection publishes one committed receipt");
            Require(home.Profile.Save.Player.Experience == before + collected,
                "native collection grants exactly the committed experience once");
            Require(home.Session.Preview(1).Count == 0, "native collection consumes the saved bed income");
            yield return Capture("bed-collected.png");
            Require(home.Profile.Save.Player.Experience == before + collected,
                "original award presentation cannot grant experience again");
            home.Close(); home = null; yield return null;
            Open(); yield return new WaitForSecondsRealtime(.8f);
            Require(home.Profile.Save.Player.Experience == before + collected && home.Session.Preview(1).Count == 0,
                "cold reopen preserves granted experience and consumed bed income");
        }

        IEnumerator CheckBedFreeAcceleration()
        {
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            var services = Field<OfflineHomeBedServices>(home.Hud, "nativeBedServices");
            var progress = Field<OfflineHomeFastHangUpProgress>(home.Hud, "nativeFastProgress");
            int id = home.Rules.HangUp(1).fastHangUpId;
            var fast = home.Rules.FastHangUp(id);
            int used = home.Session.GetTodayFreeFastForwardCount(id);
            long before = home.Profile.Save.Player.Experience;
            long expected = (long)services.GetHangUpReward(1, fast.freeHours);
            Require(expected > 0 && home.Session.CanFreeFastForward(id), "actual configured free bed acceleration is available");
            yield return WaitForButton(Windows.Bed.FastHangUpBtn); Click(Windows.Bed.FastHangUpBtn);
            yield return null;
            var confirm = Object.FindObjectOfType<WorldConfirmDialog>();
            if (confirm)
            {
                yield return WaitForButton(confirm.ConfirmBtn); Click(confirm.ConfirmBtn);
            }
            Require(progress.IsOpen, "native free bed button opens the original acceleration animation");
            Require(home.Session.GetTodayFreeFastForwardCount(id) == used + 1,
                "free acceleration commits its daily use once");
            Require(home.Profile.Save.Player.Experience == before,
                "free acceleration holds the experience receipt until the original animation finishes");
            Require(home.Hud.CanvasTransform.Find("Content/Main").GetComponent<CanvasGroup>().alpha == 0,
                "original acceleration hides the owned home HUD");
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("bed-free-progress.png");
            float deadline = Time.realtimeSinceStartup + 30;
            while (progress.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!progress.IsOpen, "original animation event closes acceleration without a forced callback");
            Require(home.Hud.CanvasTransform.Find("Content/Main").GetComponent<CanvasGroup>().alpha == 1,
                "original acceleration completion restores the home HUD");
            Require(home.Profile.Save.Player.Experience == before + expected,
                "free acceleration applies exactly the configured experience after its animation");
            yield return Capture("bed-free-complete.png");
            home.Close(); home = null; yield return null;
            Open(); yield return new WaitForSecondsRealtime(.8f);
            Require(home.Session.GetTodayFreeFastForwardCount(id) == used + 1
                && home.Profile.Save.Player.Experience == before + expected,
                "cold reopen restores the consumed daily use and granted acceleration experience");
        }

        IEnumerator CheckBedItemAcceleration()
        {
            int id = home.Rules.HangUp(1).fastHangUpId;
            var fast = home.Rules.FastHangUp(id);
            while (home.Session.CanFreeFastForward(id))
                Require(home.TryFastForward(1) > 0, "consume remaining configured free uses for the item branch");
            var item = fast.items.OrderBy(value => value.hours).First();
            Require(home.Profile.ApplyInventoryTransaction("bed:item:fixture", save => {
                if (!OfflineInventoryOperations.Grant(save.Player, item.itemId, 2, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                    throw new InvalidOperationException("Cannot prepare isolated acceleration items.");
            }).IsApplied, "grant configured acceleration items only to the isolated profile");
            long capacity = home.Session.Preview(1).Capacity;
            Require(home.Session.Mutate("bed:item:stored", save => {
                var entry = save.Income.Single(value => value.HangUpId == 1);
                entry.StoredSeconds = 0; entry.SettledCount = capacity - 1;
            }), "prepare near-capacity bed income");
            home.Hud.ShowBed(); yield return new WaitForSecondsRealtime(.8f);
            var services = Field<OfflineHomeBedServices>(home.Hud, "nativeBedServices");
            var progress = Field<OfflineHomeFastHangUpProgress>(home.Hud, "nativeFastProgress");
            long before = home.Profile.Save.Player.Experience;
            long itemsBefore = home.Profile.InventoryCount(item.itemId);
            yield return WaitForButton(Windows.Bed.FastHangUpBtn); Click(Windows.Bed.FastHangUpBtn);
            yield return new WaitForSecondsRealtime(.5f);
            var confirm = Object.FindObjectOfType<WorldConfirmDialog>();
            if (confirm) { yield return WaitForButton(confirm.ConfirmBtn); Click(confirm.ConfirmBtn); }
            yield return new WaitForSecondsRealtime(.5f);
            var selection = Windows.FastSelection;
            Require(selection, "exhausted free uses open the original item acceleration window");
            var row = selection.GetComponentsInChildren<FurnitureUseItemSliderGroup>().Single(value => value.ItemId == item.itemId);
            var slider = Field<SetUseCountSlider>(row, "mSlider");
            while (row.UseCount > 1) { yield return WaitForButton(slider.MinusBtn); Click(slider.MinusBtn); }
            yield return WaitForButton(slider.AddBtn); Click(slider.AddBtn);
            Require(row.UseCount == 2, "native item quantity buttons update the selected count");
            long expected = (long)services.GetHangUpReward(1, item.hours * row.UseCount);
            yield return Capture("bed-item-selection.png");
            yield return WaitForButton(selection.FastBtn); Click(selection.FastBtn);
            yield return null;
            confirm = Object.FindObjectOfType<WorldConfirmDialog>();
            Require(confirm, "near-capacity item acceleration uses its original overflow confirmation");
            yield return WaitForButton(confirm.ConfirmBtn); Click(confirm.ConfirmBtn);
            Require(progress.IsOpen && home.Profile.InventoryCount(item.itemId) == itemsBefore - 2,
                "confirmed item acceleration consumes exactly the selected cost and opens the original animation");
            Require(home.Profile.Save.Player.Experience == before && home.Session.Preview(1).Count >= capacity - 1 + expected,
                "item acceleration stores its configured output in the bed without granting experience");
            yield return new WaitForSecondsRealtime(.6f);
            yield return Capture("bed-item-progress.png");
            float deadline = Time.realtimeSinceStartup + 30;
            while (progress.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
            Require(!progress.IsOpen && home.Profile.Save.Player.Experience == before,
                "item animation ends naturally and leaves its output for manual collection");
            long stored = home.Session.Preview(1).Count;
            Require(stored > capacity, "item acceleration retains settled output above ordinary capacity");
            home.Close(); home = null; yield return null;
            Open(); yield return new WaitForSecondsRealtime(.8f);
            Require(home.Profile.InventoryCount(item.itemId) == itemsBefore - 2
                && home.Profile.Save.Player.Experience == before && home.Session.Preview(1).Count == stored,
                "cold reopen retains item cost and overflow output without auto-collecting experience");
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
            var canvases = home.Hud.CanvasTransform.GetComponentsInChildren<Canvas>(true)
                .Concat(Object.FindObjectsOfType<Canvas>(true).Where(value => value.worldCamera == home.Scene.Camera)).Distinct().ToArray();
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
                    if (canvases[index])
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
