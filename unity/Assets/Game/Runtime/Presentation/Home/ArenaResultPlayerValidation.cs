using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    // Explicit Player diagnostic. Every profile and screenshot stays below the
    // supplied validation directory; the user's save selection is never opened.
    public sealed class ArenaResultPlayerValidation : MonoBehaviour
    {
        private string output, saves, slot;
        private Exception failure;
        private float deadline;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-arena-result");
            if (index < 0 || index + 1 >= args.Length) return;
            var root = new GameObject("ArenaResultPlayerValidation");
            DontDestroyOnLoad(root);
            var runner = root.AddComponent<ArenaResultPlayerValidation>();
            runner.output = Path.GetFullPath(args[index + 1]);
            runner.saves = Path.Combine(runner.output, "save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runner.output);
            Application.runInBackground = true;
            runner.deadline = Time.realtimeSinceStartup + 600;
            Application.logMessageReceived += runner.Observe;
            runner.StartCoroutine(runner.Guard(runner.Run()));
        }

        private void Observe(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                failure = new InvalidOperationException(message + "\n" + stack);
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            var pending = new Stack<IEnumerator>(); pending.Push(routine);
            while (true)
            {
                object next;
                try
                {
                    Check(Time.realtimeSinceStartup < deadline, "Arena Player validation timed out.");
                    if (failure != null) throw failure;
                    if (pending.Count == 0)
                    {
                        Finish(new Result { passed = true }); yield break;
                    }
                    var current = pending.Peek();
                    if (!current.MoveNext()) { pending.Pop(); continue; }
                    next = current.Current;
                    if (next is IEnumerator nested) { pending.Push(nested); continue; }
                }
                catch (Exception error) { Finish(new Result { error = error.ToString() }); yield break; }
                yield return next;
            }
        }
        private void Finish(Result result)
        {
            Application.logMessageReceived -= Observe;
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(result, true));
            Debug.Log("[Arena Player] " + (result.passed ? "passed" : result.error));
            Application.Quit(result.passed ? 0 : 1);
        }

        private IEnumerator Run()
        {
            while (!GameObject.Find("OfficialSaveSelectionPanel")) yield return null;
            var slots = new ProfileSlotCatalog(saves); slot = slots.CreateSlot();
            var assets = OfficialCharacterAssetCatalog.LoadDefault(); var catalog = assets.Load();
            var save = new OfflineMainlineSave(); var player = save.Player;
            player.CharacterCreated = true; player.CharacterName = "竞技验证";
            player.Level = 40; player.Experience = catalog.CumulativeLevelExperience[40];
            player.Rank = 1; player.Wardrobe.Profession = 1; player.InitialWeaponType = 200;
            player.SubRank = catalog.LevelLimits.Where(row => row.PlayerLevel >= 40 && row.PlayerBlessLevel == 0)
                .Min(row => row.SubRank);
            player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(slot, saves, null); home.TutorialAutoStartEnabled = false;
            Check(home.DailyDungeonProgression.InitializeWardrobe("arena-player-validation-wardrobe").IsApplied,
                "Fixture wardrobe was not initialized.");
            var identities = home.Profile.EnsureLocalActorIdentities();
            Check(identities.Status == ProgressionResultStatus.Applied
                || identities.Status == ProgressionResultStatus.Unchanged,
                "The local battle actor identities could not be initialized.");
            Check(home.DailyDungeonProgression.EquipSkill(10014, 0).IsApplied, "Fixture skill was not equipped.");
            foreach (var group in home.Arena.Catalog.function_limits.Where(row => row.Type == 58 && row.Condition != null)
                .GroupBy(row => row.Condition.MapClassId))
            {
                home.Profile.InitializeMap(group.Key, group.Select(row => row.Condition.Region).Distinct().ToArray());
                foreach (var row in group) home.Profile.SetMapRegionProgress(group.Key, row.Condition.Region, 10000);
            }
            yield return Settle();
            Click(home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>().First(value => value.name == "UnlockObj"
                && value.transform.parent.name == "TravelNote"));
            yield return Settle();
            yield return Capture("arena-travel-top");
            yield return ScrollTravelNotesToArena();
            yield return Capture("arena-travel-arena");
            Click(Button("ArenaAreaEntry", "EntryBtn")); yield return Settle();
            var guard = home.Arena.Catalog.runtime.guards.OrderBy(row => row.id).First();
            var formula = OfficialCombatAssetCatalog.LoadDefault().LoadDamageFormula();
            for (int kind = 0; kind < 4; kind++)
            {
                home.Arena.Save.opponents[0].guardTemplateId = guard.id;
                home.Arena.Save.opponents[0].rank = Math.Max(1, home.Arena.Rank - 10);
                int tickets = home.Arena.Tickets, wins = home.Arena.Wins, losses = home.Arena.Losses;
                long coins = home.ItemCount(home.Arena.Catalog.settings.CoinId);
                var parent = home.HomePlayerTransform.parent; var position = home.HomePlayerTransform.localPosition;
                Click(Button("OpponentItem", "FightBtn")); yield return Settle();
                var choice = Root("ArenaConfirm")?.GetComponent<WorldConfirmDialog>();
                Check(choice, "The challenge-mode dialog did not open.");
                Click(kind == 3 ? choice.ConfirmBtn : choice.CancelBtn); yield return Settle();
                var runner = FindObjectOfType<ArenaBattleRunner>();
                if (kind != 3)
                {
                    Check(runner, "A real arena runner was not started.");
                    var controller = runner.GetComponent<OfficialBattleRuntimeController>();
                    foreach (var unit in controller.Checkpoint.Units)
                    {
                        bool loser = kind == 0 ? unit.Id == 2 : kind == 1 && unit.Id == 1;
                        unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = loser ? 1 : 1000000000L;
                        unit.Turn.Speed = (kind == 1 ? unit.Id == 2 : unit.Id == 1) ? 1000 : 1;
                        unit.Turn.RankSpeedScale = 1;
                        foreach (var props in new[] { unit.Properties, unit.BaseProperties })
                        {
                            Set(props, formula.PropertyId("MaxHp"), unit.MaxHp);
                            Set(props, formula.PropertyId("Attack"), 1000000);
                            Set(props, formula.PropertyId("Defence"), 0);
                            Set(props, formula.PropertyId("Speed"), unit.Turn.Speed);
                        }
                    }
                    while (controller.Phase != FightBattlePhase.Play)
                    {
                        var start = FindObjectsOfType<Button>().FirstOrDefault(value => value.isActiveAndEnabled
                            && value.name == "Btn" && value.transform.parent.name == "StartBtn" && value.interactable);
                        if (start) Click(start);
                        yield return null;
                    }
                    if (kind == 2)
                    {
                        while (!controller.IsPresentingStep) yield return null;
                        Click(LeaveButton()); yield return Settle();
                        Click(FindObjectOfType<WorldConfirmDialog>().CancelBtn); yield return Settle();
                        Check(!controller.Checkpoint.Finished, "Cancelling leave ended the battle.");
                        Click(LeaveButton()); yield return Settle();
                        Click(FindObjectOfType<WorldConfirmDialog>().ConfirmBtn);
                    }
                }
                ArenaResultWindow window;
                while (!(window = FindObjectOfType<ArenaResultWindow>()) || !window.IsReady) yield return null;
                bool won = kind == 0 || kind == 3; var settled = home.Arena.LastResult;
                Check(settled.won == won && settled.fastVictory == (kind == 3)
                    && home.Arena.Tickets == tickets - 1 && home.Arena.Wins == wins + (won ? 1 : 0)
                    && home.Arena.Losses == losses + (won ? 0 : 1) && !home.Arena.HasActiveChallenge,
                    "Arena result/ticket transaction disagrees with actual play.");
                Check(home.ItemCount(settled.awardItemId) == coins + settled.awardCount
                    && !window.Opponent.Name.StartsWith("arena_guard_name_"), "Original reward or guard name is unbound.");
                var reopened = OfflineArenaSession.Open(slots, slot);
                Check(JsonUtility.ToJson(reopened.LastResult) == JsonUtility.ToJson(settled)
                    && !reopened.CompleteChallenge(won, home.GrantBattleReward), "Durable result was lost or granted twice.");
                yield return Capture("arena-result-" + kind);
                var native = window.Panel.GetComponent<GameEndPanelArena>();
                var actions = window.Panel.GetComponent<GameEndPanelBase>();
                if (kind != 3)
                {
                    Check(runner.FinalReport != null && runner.FinalCheckpoint.Finished, "The final report was not retained.");
                    if (kind == 2) Check(!runner.FinalCheckpoint.PartEnded && runner.FinalCheckpoint.Outcome.Reason == 2
                        && runner.FinalCheckpoint.Units.All(unit => unit.Hp > 0), "Forfeit fabricated deaths or partial rewards.");
                    string snapshot = FightBattleCheckpointSerializer.Save(runner.FinalCheckpoint);
                    for (int repeat = 0; repeat < 2; repeat++)
                    {
                        Click(actions.ReportButton); yield return Settle();
                        var report = Root("FightEndReportPanel") ?? Root("FightEndAllPlayerReportPanel");
                        Check(report, "The original report button did not open statistics.");
                        yield return Capture("arena-report-" + kind + "-" + repeat);
                        Click(report.GetComponentsInChildren<Button>().Single(button => button.name == "CloseBtn"));
                        yield return Settle();
                    }
                    Check(FightBattleCheckpointSerializer.Save(runner.FinalCheckpoint) == snapshot,
                        "Viewing reports advanced the final battle.");
                }
                else Check(!runner && !actions.ReportButton.gameObject.activeSelf, "Direct victory fabricated a report.");
                Click(native.LeaveBtn); yield return Settle();
                Check(!window && !runner && home.HomePlayerTransform.parent == parent
                    && home.HomePlayerTransform.localPosition == position, "Leaving the native result leaked battle state.");
                Check(home.ItemCount(settled.awardItemId) == coins + settled.awardCount, "Report/leave granted twice.");
                yield return Capture("arena-return-" + kind);
            }
        }

        private static void Set(List<FightPropertyValue> values, int id, long amount)
        {
            var value = values.FirstOrDefault(row => row.Id == id);
            if (value == null) values.Add(new FightPropertyValue { Id = id, Value = amount });
            else value.Value = amount;
        }
        private static Transform Root(string name) => FindObjectsOfType<Transform>().FirstOrDefault(value => value.name == name);
        private static Button Button(string root, string name) => Root(root).GetComponentsInChildren<Button>()
            .First(value => value.name == name && value.isActiveAndEnabled && value.interactable);
        private static Button LeaveButton() => FindObjectsOfType<Button>().Single(value => value.isActiveAndEnabled
            && value.name == "CloseBtn_normal" && value.transform.parent.name == "CloseBtn" && value.interactable);
        private static IEnumerator Settle() { yield return new WaitForSecondsRealtime(1); }
        private IEnumerator ScrollTravelNotesToArena()
        {
            Transform root = Root("TravelNotesPanel");
            ScrollRect scroll = root.GetComponentInChildren<ScrollRect>();
            Check(scroll && scroll.viewport && scroll.content, "Travel Notes scroll container is missing.");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null,
                    scroll.viewport.TransformPoint(scroll.viewport.rect.center)),
                scrollDelta = new Vector2(0, -6)
            };
            GameObject arena = Root("ArenaAreaEntry").gameObject;
            for (int attempt = 0; attempt < 36; attempt++)
            {
                Canvas.ForceUpdateCanvases();
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null,
                    ((RectTransform)arena.transform).TransformPoint(((RectTransform)arena.transform).rect.center));
                if (RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, center)) return;
                ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.scrollHandler);
                yield return null;
            }
            throw new InvalidOperationException("Arena entry did not enter the original Travel Notes viewport after simulated wheel input.");
        }
        private static void Click(Selectable control)
        {
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)control.transform;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            bool TargetsControl(Vector2 position)
            {
                if (position.x < 0 || position.x >= Screen.width || position.y < 0 || position.y >= Screen.height)
                    return false;
                pointer.position = position;
                hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0) return false;
                GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                return target != null && (target == control.gameObject || target.transform.IsChildOf(control.transform));
            }
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            bool found = TargetsControl(position);
            if (!found)
            {
                var samples = new List<Vector2> { position };
                foreach (Graphic graphic in control.GetComponentsInChildren<Graphic>())
                {
                    if (graphic == null || !graphic.raycastTarget) continue;
                    RectTransform graphicRect = graphic.rectTransform;
                    samples.Add(RectTransformUtility.WorldToScreenPoint(null,
                        graphicRect.TransformPoint(graphicRect.rect.center)));
                    for (int y = 0; y < 3; y++)
                        for (int x = 0; x < 3; x++)
                        {
                            Vector2 local = new Vector2(Mathf.Lerp(graphicRect.rect.xMin, graphicRect.rect.xMax, (x + .5f) / 3f),
                                Mathf.Lerp(graphicRect.rect.yMin, graphicRect.rect.yMax, (y + .5f) / 3f));
                            samples.Add(RectTransformUtility.WorldToScreenPoint(null, graphicRect.TransformPoint(local)));
                        }
                }
                foreach (Vector2 candidate in samples)
                    if (TargetsControl(candidate)) { position = candidate; found = true; break; }
            }
            Check(found, "Player control has no exposed original hit area: " + control.name
                + "; tested=" + position + "; topHit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            pointer.position = position;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame(); var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); }
            finally { Destroy(frame); }
        }
        private void OnDestroy() => Application.logMessageReceived -= Observe;
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
