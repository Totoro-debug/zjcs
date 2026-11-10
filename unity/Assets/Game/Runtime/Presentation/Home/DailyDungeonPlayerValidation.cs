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
    public sealed class DailyDungeonPlayerValidation : MonoBehaviour
    {
        private string output, saves, slot;
        private Exception failure;
        private float deadline;
        [Serializable] private sealed class Result { public bool passed; public string error; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-daily-dungeon");
            if (index < 0 || index + 1 >= args.Length) return;
            var root = new GameObject("DailyDungeonPlayerValidation");
            DontDestroyOnLoad(root);
            var runner = root.AddComponent<DailyDungeonPlayerValidation>();
            runner.output = Path.GetFullPath(args[index + 1]);
            runner.saves = Path.Combine(runner.output, "save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runner.output);
            Application.runInBackground = true;
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            runner.deadline = Time.realtimeSinceStartup + 1800;
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
                    Check(Time.realtimeSinceStartup < deadline, "Daily dungeon Player validation timed out.");
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
            Debug.Log("[Daily dungeon Player] " + (result.passed ? "passed" : result.error));
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(result.passed ? 0 : 1);
#else
            Application.Quit(result.passed ? 0 : 1);
#endif
        }

        private IEnumerator Run()
        {
            bool partialExit = Array.IndexOf(Environment.GetCommandLineArgs(),
                "--zjcs-daily-dungeon-partial-exit") >= 0;
            var args = Environment.GetCommandLineArgs();
            int dungeonArgument = Array.IndexOf(args, "--zjcs-daily-dungeon-id");
            int dungeonId = dungeonArgument < 0 ? 11 : int.Parse(args[dungeonArgument + 1]);
            while (!GameObject.Find("OfficialSaveSelectionPanel")) yield return null;
            var slots = new ProfileSlotCatalog(saves); slot = slots.CreateSlot();
            var character = OfficialCharacterAssetCatalog.LoadDefault().Load();
            var save = new OfflineMainlineSave(); var player = save.Player;
            player.CharacterCreated = true; player.CharacterName = "副本验证";
            player.Level = 40; player.Experience = character.CumulativeLevelExperience[40];
            player.Rank = 1; player.Wardrobe.Sex = 1; player.Wardrobe.Profession = 1; player.InitialWeaponType = 200;
            player.SubRank = character.LevelLimits.Where(row => row.PlayerLevel >= 40 && row.PlayerBlessLevel == 0)
                .Min(row => row.SubRank);
            player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 1 });
            int purchaseCurrency = DailyDungeonModule.LoadCatalog().settings.DailyDungeonBuyCostItemId;
            player.Inventory.Add(new OfflineItemStack { ItemId = purchaseCurrency, Count = 1000000 });
            // Supply the isolated profile before battle construction: the official
            // opening statuses can resolve damage inside BeginBattle itself.
            int fixturePropId = Convert.ToInt32(character.Rows("game_settings").First()["PlayerLevelBattlePropId"]);
            var fixtureProps = character.GetLevelProps(fixturePropId, player.Level);
            foreach (var fixture in new[] { ("BaseMaxHp", 1000000000L), ("BaseAttack", 1000000L), ("BaseSpeed", 1000L) })
            {
                int propertyId = character.PropertyIds[fixture.Item1];
                Check(fixtureProps.TryGetValue(propertyId, out long value) && value > 0,
                    "The official level property fixture is missing " + fixture.Item1);
                player.CharacterProperties.ExtraProperties.Add(new OfflineExtraProperty
                {
                    SourceId = "daily-dungeon-validation", PropId = fixturePropId, Level = player.Level,
                    PropType = propertyId, Count = Math.Max(1L, fixture.Item2 / value)
                });
            }
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
            foreach (var camera in FindObjectsOfType<Camera>()) camera.enabled = false;
            var home = HomeFeatureRuntime.Begin(slot, saves, null); home.TutorialAutoStartEnabled = false;
            Check(home.DailyDungeonProgression.InitializeWardrobe("daily-dungeon-validation-wardrobe").IsApplied,
                "Fixture wardrobe was not initialized.");
            Check(home.Profile.Save.Player.Wardrobe.BaseSkins.Count > 0, "Fixture default appearance is empty.");
            home.Profile.EnsureLocalActorIdentities();
            Check(home.DailyDungeonProgression.EquipSkill(10014, 0).IsApplied, "Fixture skill was not equipped.");
            foreach (var group in home.DailyDungeons.Catalog.dungeons.Where(row => row.entryMapClassId > 0)
                .GroupBy(row => row.entryMapClassId))
            {
                home.Profile.InitializeMap(group.Key, group.Select(row => row.entryRegionId).Distinct().ToArray());
                foreach (var row in group) home.Profile.SetMapRegionProgress(group.Key, row.entryRegionId, 10000);
            }
            yield return Settle();
            Click(home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>().First(value => value.name == "UnlockObj"
                && value.transform.parent.name == "TravelNote"));
            yield return Settle();
            Click(Button("DailyDungeonEntry", "EntryBtn")); yield return Settle();
            var appearances = JsonUtility.FromJson<DungeonOfficialBindingsDocument>(File.ReadAllText(
                Path.Combine(Application.streamingAssetsPath, "Dungeons/UI/884_156692/official-bindings.json"))).artwork;
            foreach (Transform card in Root("DailyDungeonEntryPanel").GetComponentsInChildren<Transform>(true)
                .Where(value => value.name.StartsWith("Dungeon_", StringComparison.Ordinal)))
            {
                int id = int.Parse(card.name.Substring("Dungeon_".Length));
                var expected = appearances.Single(value => value.dungeonId == id);
                Check(card.Find("BgImg").GetComponent<Image>().sprite.name == expected.entry.name,
                    "The directory cover does not match the official settings: " + id);
            }
            yield return Capture("daily-dungeon-directory");
            var entry = Root("Dungeon_" + dungeonId);
            Check(entry, "The selected dungeon is absent from the shared directory: " + dungeonId);
            Click(entry.GetComponentsInChildren<Button>().First(button => button.interactable));
            yield return Settle(); yield return Capture("daily-dungeon-detail");
            Click(Button("DailyDungeonDialog", "GoToBtn")); yield return Settle();
            Check(home.DailyDungeons.ActiveRun != null, "Entering did not persist the route.");
            int claims = home.DailyDungeons.RemainEntries;
            long lifetime = home.DailyDungeons.DailyDungeonGetRewardCount;
            string runId = home.DailyDungeons.ActiveRun.id;
            int battles = 0, rooms = 0;
            while (home.DailyDungeons.ActiveRun != null)
            {
                var run = home.DailyDungeons.ActiveRun;
                int room = run.currentRoomId;
                if (run.clearedRoomIds.Contains(room))
                    room = home.DailyDungeons.FindRoom(room).nextRoomIds.First();
                Transform cell = Root("Room_" + room);
                Check(cell, "The next original route cell is not visible: " + room);
                yield return Capture("daily-dungeon-route-" + rooms);
                Click(cell.GetComponentsInChildren<Button>().First(button => button.interactable));
                yield return Settle();
                Transform invitation = Root("BattleAssistSelection");
                if (invitation)
                {
                    Toggle partner = invitation.GetComponentsInChildren<Toggle>()
                        .FirstOrDefault(value => value.interactable && !value.isOn);
                    // Reserve the fixture partner's official invitation allowance for
                    // the final battle, so the completed route has a nonempty roster.
                    if (partner && (partialExit || IsLastBattle(home.DailyDungeons, room))) Click(partner);
                    yield return Settle(); yield return Capture("daily-dungeon-invitation-" + battles);
                    Click(Button("BattleAssistSelection", "CommonBtn_12")); yield return Settle();
                    var runner = FindObjectOfType<DailyDungeonBattleRunner>();
                    Check(runner, "The invitation did not enter an actual dungeon battle.");
                    var controller = runner.GetComponent<OfficialBattleRuntimeController>();
                    Check(controller, "The dungeon runner did not mount its battle controller.");
                    bool exitThisBattle = partialExit && battles > 0 && home.DailyDungeons.CurrentGrade() > 0;
                    if (exitThisBattle)
                    {
                        var savedRoute = DailyDungeonModule.Open(slots, slot).ActiveRun;
                        Check(savedRoute != null && savedRoute.clearedRoomIds.Count
                            == home.DailyDungeons.ActiveRun.clearedRoomIds.Count,
                            "The cleared partial route did not survive disk reload.");
                    }
                    while (controller && controller.Phase != FightBattlePhase.Play)
                    {
                        var start = FindObjectsOfType<Button>().FirstOrDefault(value => value.isActiveAndEnabled
                            && value.name == "Btn" && value.transform.parent.name == "StartBtn" && value.interactable);
                        if (start) Click(start);
                        yield return null;
                    }
                    var playedCheckpoint = controller.Checkpoint;
                    yield return Capture("daily-dungeon-battle-" + battles++);
                    if (exitThisBattle)
                    {
                        Click(LeaveButton()); yield return Settle();
                        var confirmation = FindObjectsOfType<WorldConfirmDialog>().Single();
                        while (confirmation.PopupWindow.State != WorldPopupWindow.EState.Show) yield return null;
                        yield return Capture("daily-dungeon-partial-exit-confirm");
                        Click(confirmation.ConfirmBtn);
                    }
                    while (runner) yield return null;
                    File.WriteAllText(Path.Combine(output, "battle-" + battles + ".json"),
                        FightBattleCheckpointSerializer.Save(playedCheckpoint));
                    yield return Settle();
                    if (exitThisBattle)
                    {
                        Check(home.DailyDungeons.ActiveRun == null,
                            "The original battle exit confirmation did not settle the partial route.");
                        break;
                    }
                    Check(home.DailyDungeons.ActiveRun == null
                        || home.DailyDungeons.ActiveRun.clearedRoomIds.Contains(room),
                        "The actual battle did not clear room " + room + "; inspect its isolated checkpoint.");
                }
                rooms++;
                Check(rooms <= 50, "The dungeon route did not progress.");
            }
            Check(battles > 0 && home.DailyDungeons.PendingClaims.Any(value => value.id == runId),
                "The actual route did not create a pending settlement.");
            int grade = home.DailyDungeons.PendingClaims.Single(value => value.id == runId).run.grade;
            Check(home.DailyDungeons.RemainEntries == claims, "Entering or completing consumed a claim before collecting.");
            var result = FindObjectOfType<DailyDungeonResultPanelAdapter>();
            Check(result, "The original daily dungeon result window did not open.");
            while (result.GetComponent<WorldPopupWindow>().State != WorldPopupWindow.EState.Show) yield return null;
            yield return Settle();
            yield return Capture("daily-dungeon-result");
            CheckVisibleRewards(result.transform, home.PreviewDailyDungeonRewards(runId, 1, out _).Count);
            int purchasedBefore = home.DailyDungeons.Save.purchasedEntries;
            long balanceBefore = home.Profile.InventoryCount(purchaseCurrency);
            long price = home.DailyDungeons.Catalog.settings.DailyDungeonBuyCostCountList[purchasedBefore];
            Click(result.GetComponentsInChildren<Button>().Single(value => value.name == "CommonBtn_Add"));
            yield return Settle();
            yield return Capture("daily-dungeon-purchase-open");
            var purchaseRoot = Root("DailyDungeonBuyCountDialog");
            Check(purchaseRoot, "The exposed count button did not open the purchase dialog; capacity="
                + home.DailyDungeons.ClaimCapacity + "; purchaseRemaining="
                + home.DailyDungeons.EntryAllowance().PurchaseRemaining);
            var slider = purchaseRoot.GetComponentInChildren<Slider>();
            if (slider.maxValue > slider.value)
            {
                Click(Button("DailyDungeonBuyCountDialog", "CommonBtn_Add")); yield return Settle();
            }
            while (slider.value > 1)
            {
                Click(Button("DailyDungeonBuyCountDialog", "CommonBtn_Reduce")); yield return Settle();
            }
            yield return Capture("daily-dungeon-purchase");
            Click(Button("DailyDungeonBuyCountDialog", "CommonBtn_12")); yield return Settle();
            Check(home.DailyDungeons.RemainEntries == claims + 1
                && home.DailyDungeons.Save.purchasedEntries == purchasedBefore + 1
                && home.Profile.InventoryCount(purchaseCurrency) == balanceBefore - price,
                "The original count purchase did not exchange the configured currency and one claim.");
            var purchased = DailyDungeonModule.Open(slots, slot);
            var purchaseSave = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(slots.GetProgressPath(slot)));
            Check(purchased.RemainEntries == claims + 1 && purchased.Save.purchasedEntries == purchasedBefore + 1
                && purchaseSave.Player.Inventory.Single(value => value.ItemId == purchaseCurrency).Count == balanceBefore - price,
                "The count purchase did not survive disk reload.");
            claims++;
            result = FindObjectOfType<DailyDungeonResultPanelAdapter>();
            Check(result, "The pending result did not reopen after buying a claim.");
            while (result.GetComponent<WorldPopupWindow>().State != WorldPopupWindow.EState.Show) yield return null;
            Click(result.GetComponentsInChildren<Button>().Single(value => value.name == "CommonBtn_CostItem"));
            yield return Settle();
            float chestDeadline = Time.realtimeSinceStartup + 10;
            var resultRow = result.GetComponentInChildren<DailyDungeonResultPlayerRowAdapter>();
            while (!resultRow.ChestOpened && Time.realtimeSinceStartup < chestDeadline) yield return null;
            Check(resultRow.ChestOpened, "The original chest sequence did not open after claiming.");
            yield return new WaitForSeconds(1);
            Check(home.DailyDungeons.RemainEntries == claims - 1
                && home.DailyDungeons.DailyDungeonGetRewardCount == lifetime + 1
                && home.DailyDungeons.PendingClaims.All(value => value.id != runId), "Claim did not settle exactly once.");
            var restored = DailyDungeonModule.Open(slots, slot);
            Check(restored.RemainEntries == claims - 1 && restored.DailyDungeonGetRewardCount == lifetime + 1,
                "Claim count did not survive disk reload.");
            yield return Capture("daily-dungeon-result-claimed");
            Click(result.GetComponentsInChildren<Button>().Single(value => value.name == "CloseBtn"));
            yield return Settle();
            Check(home.DailyDungeons.RemainEntries == claims - 1, "Closing granted or consumed rewards again.");
            yield return Capture("daily-dungeon-return");
            if (partialExit)
            {
                Check(!restored.Leaderboard(dungeonId, 0, grade, int.MaxValue).Any(value => value.IsSelf),
                    "A partial exit incorrectly created a full-clear leaderboard record.");
                Debug.Log("[Daily dungeon Player] Partial exit settled " + rooms + " original rooms and " + battles + " actual battles.");
                yield break;
            }
            var record = restored.Leaderboard(dungeonId, 0, grade, int.MaxValue).Single(value => value.IsSelf);
            Check(record.TeamMembers.Count > 0, "The actual invited first-clear team was not saved.");
            Click(Root("Dungeon_" + dungeonId).GetComponentsInChildren<Button>().First(button => button.interactable));
            yield return Settle();
            Click(Button("DailyDungeonDialog", "DailyDungeonTopGrade"));
            yield return Settle();
            yield return Capture("daily-dungeon-first-clear-team");
            var npcIcon = Root("DailyDungeonTopPanel").GetComponentsInChildren<Zjcs.Official.TeamUI.TeamNpcIconUI>()
                .FirstOrDefault();
            Check(npcIcon && npcIcon.NpcIcon.sprite && !npcIcon.NpcIcon.canvasRenderer.cull,
                "The saved first-clear NPC portrait is not rendered.");
            Check(npcIcon.NameText && !string.IsNullOrEmpty(npcIcon.NameText.Text)
                && !npcIcon.NameText.Text.StartsWith("ui_", StringComparison.Ordinal),
                "The first-clear partner name is not localized.");
            Check(!npcIcon.transform.parent.Find("PublicPlayerIcon").gameObject.activeSelf,
                "The player icon branch covers the first-clear NPC portrait.");
            Debug.Log("[Daily dungeon Player] Completed " + rooms + " original route rooms and " + battles + " actual battles.");
        }

        private static void CheckVisibleRewards(Transform result, int count)
        {
            if (count == 0) return;
            Transform viewport = result.Find("Content/WinObj/Content/GameEndRewardContent/ScrollRect/ViewPort");
            var icons = viewport.GetComponentsInChildren<Image>().Where(value => value.name == "icon"
                && value.sprite && value.isActiveAndEnabled && !value.canvasRenderer.cull).ToArray();
            Check(icons.Any(value => RectTransformUtility.RectangleContainsScreenPoint(viewport as RectTransform,
                RectTransformUtility.WorldToScreenPoint(null, value.rectTransform.TransformPoint(value.rectTransform.rect.center)))),
                "Granted reward icons are not visible within the original result viewport; viewport="
                + (viewport as RectTransform).rect + "; icons=" + icons.Length);
        }

        private static bool IsLastBattle(DailyDungeonModule module, int room)
        {
            var pending = new Stack<int>(module.FindRoom(room).nextRoomIds);
            var visited = new HashSet<int>();
            while (pending.Count > 0)
            {
                int next = pending.Pop();
                if (!visited.Add(next)) continue;
                var cell = module.FindRoom(next);
                var stage = module.FindStage(cell.stageId);
                if (stage != null && (stage.waveArrayIds?.Length > 0
                    || stage.waveMonsterIds?.Any(wave => wave != null && wave.Length > 0) == true)) return false;
                foreach (int id in cell.nextRoomIds) pending.Push(id);
            }
            return true;
        }
        private static Transform Root(string name) => FindObjectsOfType<Transform>().FirstOrDefault(value => value.name == name);
        private static Button Button(string root, string name) => Root(root).GetComponentsInChildren<Button>()
            .First(value => value.name == name && value.isActiveAndEnabled && value.interactable);
        private static Button LeaveButton() => FindObjectsOfType<Button>().Single(value => value.isActiveAndEnabled
            && value.name == "CloseBtn_normal" && value.transform.parent.name == "CloseBtn" && value.interactable);
        private static IEnumerator Settle() { yield return new WaitForSecondsRealtime(1); }
        private static void Click(Selectable control)
        {
            Check(control && control.IsActive() && control.IsInteractable(),
                "Player control is unavailable: " + (control ? control.name : "null"));
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
                + "; tested=" + position + "; topHit=" + (hits.Count == 0 ? "none" : string.Join("/", hits[0].gameObject.GetComponentsInParent<Transform>()
                    .Reverse().Select(value => value.name)) + "; layer=" + hits[0].sortingLayer + "; order=" + hits[0].sortingOrder));
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
