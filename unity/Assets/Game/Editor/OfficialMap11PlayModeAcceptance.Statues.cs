using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11PlayModeAcceptance
    {
        private static IEnumerator VerifyStatues(Transform canvas)
        {
            Invoke("AbortStoryGraph");
            OfflineMainlineProgressSession save = Field<OfflineMainlineProgressSession>("progressionSave");
            // Earlier branch probes used synthetic activation records. Reset only those
            // isolated fixture fields before exercising the actual first touch stage.
            save.ApplyInventoryTransaction("background-statue-initial-fixture", value =>
            {
                value.World.ActivatedWitches.Clear();
                value.Player.StatueVisitDate = string.Empty;
                value.Player.PendingStatueBattleEffects.Clear();
            });
            IDictionary entities = Field<IDictionary>("mapEntities");
            OfficialMap11UiData data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            Invoke("Interact", entities[2801]);
            Require(Field<object>("witchDialog") != null && save.ActiveWitchCount == 0,
                "Opening the official witch book does not activate its world stage");
            GameObject book = GameObject.Find("WorldWitchTouchDialog");
            OfficialMap11UiComponentFields fields = data.components.Single(value => value.panel == book.name && value.type == "StoryBookDialog").fields;
            string relative = fields.mStoryReadEndBtn.Substring(book.name.Length + 2);
            book.transform.Find(relative).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.2f);
            Require(GameObject.Find("WorldStageInteractionPreviewDialogWitch") != null && save.ActiveWitchCount == 0,
                "Read-end opens the actual result preview without awarding on preview");
            yield return CaptureStatueFrame("background-witch-first-preview");
            Invoke("RequestWitchDialogClose");
            Require(save.ActiveWitchCount == 1 && save.Save.Player.PendingStatueBattleEffects.Count == 1
                && save.Save.Player.PendingStatueBattleEffects[0].HpPercentChange == -1f,
                "Successful touch stage atomically records one activation and its next-battle effects");
            Require(!save.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd")),
                "Story activation is separate from periodic visits in the official stage graph");
            if (Field<bool>("dialogueWaiting")) yield return AdvanceScriptedDialogue();
            Require(Field<bool>("storyOptionsWaiting"), "Witch story resumes after the actual preview closes");
            Invoke("SelectStoryOption", 0);
            for (int count = 0; count < 24 && Field<bool>("dialogueWaiting"); count++)
            {
                Invoke("RequestDialogueAdvance");
                yield return new WaitForSecondsRealtime(.15f);
            }
            Require(!Field<bool>("storyRunning"), "Original witch post-activation story reaches its end");

            VerifyStatueEntryHp(save.Save.Player);
            UnityEngine.Object.Destroy(runtime.gameObject);
            yield return null;
            OpenMap(canvas);
            yield return new WaitForSecondsRealtime(.5f);
            save = Field<OfflineMainlineProgressSession>("progressionSave");
            entities = Field<IDictionary>("mapEntities");
            Require(save.ActiveWitchCount == 1 && save.Save.Player.PendingStatueBattleEffects.Count == 1,
                "Map teardown and cold profile loading preserve unused statue effects");
            Invoke("Interact", entities[2801]);
            yield return new WaitForSecondsRealtime(.4f);
            Require(GameObject.Find("WorldTouchDialogWitchPeriodicityInteraction") != null,
                "Activated witch opens the original periodic interaction window");
            yield return CaptureStatueFrame("background-witch-daily-ready");
            Invoke("VisitWitch", entities[2801]);
            yield return new WaitForSecondsRealtime(4.5f);
            Require(GameObject.Find("WorldStageInteractionPreviewDialogWitch") != null,
                "Periodic visit waits for the original ActionTime before result preview");
            yield return CaptureStatueFrame("background-witch-daily-result");
            Require(save.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd")), "Daily witch visit is durable");
            int effects = save.Save.Player.PendingStatueBattleEffects.Count;
            Require(save.ApplyStatueVisit(DateTime.Now.ToString("yyyyMMdd"), "hero", 11, 90001,
                "background-hero-same-day", 0, Array.Empty<int>(), Array.Empty<OfflineItemStack>(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Status == ProgressionResultStatus.NotReady,
                "Hero and witch share one daily visit");
            Invoke("RequestWitchDialogClose");
            Invoke("Interact", entities[2801]);
            yield return new WaitForSecondsRealtime(.3f);
            GameObject periodic = GameObject.Find("WorldTouchDialogWitchPeriodicityInteraction");
            fields = data.components.Single(value => value.panel == periodic.name && value.type == "PeriodicityInteractionStageUI").fields;
            Require(!periodic.transform.Find(fields.mInteractionBtn.Substring(periodic.name.Length + 2)).gameObject.activeInHierarchy,
                "Official visited state hides the visit button");
            yield return CaptureStatueFrame("background-witch-daily-used");
            Invoke("RequestWitchDialogClose");

            // Exercise all three dynamic result families using original rows; closing is deliberately
            // omitted here so preview rendering cannot create additional world activations.
            Map11RuntimeWitchInfo[] infos = Field<Map11RuntimeWitchInfo[]>("witchInfos");
            foreach (int index in new[] { 1, 2, 3, 4, 6, 9 })
            {
                Invoke("ShowWitchActivationPreview", entities[6901], infos.Single(value => value.ActiveCount == index));
                yield return new WaitForSecondsRealtime(1.2f);
                GameObject preview = GameObject.Find("WorldStageInteractionPreviewDialogWitch");
                Require(preview != null && preview.GetComponentsInChildren<TMP_Text>().All(text => !text.text.Contains("{0}")),
                    "Official witch preview resolves result text for row " + index);
                yield return CaptureStatueFrame("background-witch-preview-" + index);
                Invoke("CloseWitchWindow");
                yield return null;
            }
            Require(save.ActiveWitchCount == 1 && save.Save.Player.PendingStatueBattleEffects.Count == effects,
                "Opening and abandoning result previews never grants effects");
            Invoke("StartEntityStage", entities[3828], null);
            OfficialBattleRuntimeController battle = Field<OfficialBattleRuntimeController>("battleController");
            Require(battle != null && battle.Checkpoint.Units.First(value => value.Id == 1).Hp == 1,
                "Real world battle begins with pending statue HP and status effects");
            yield return CaptureStatueFrame("background-statue-real-battle");
            float deadline = Time.realtimeSinceStartup + 90;
            while (Field<OfficialBattleRuntimeController>("battleController") != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Require(Field<OfficialBattleRuntimeController>("battleController") == null
                && save.Save.Player.PendingStatueBattleEffects.Count == 0,
                "A real battle end clears pending statue effects");
            Require(save.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd")), "Battle end does not restore daily visits");
            Debug.Log("Map11 statue background validation passed: original book, previews, visit quota, durable effects and entry HP.");
        }

        private static IEnumerator CaptureStatueFrame(string name)
        {
            Capture(name);
            yield return new WaitForEndOfFrame();
            yield return null;
        }

        private static void VerifyStatueEntryHp(OfflinePlayerProgress profile)
        {
            FightOfficialCombatCatalog catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            MethodInfo apply = typeof(OfficialBattleRuntimeController).GetMethod("ApplyPendingStatueEffects", BindingFlags.Static | BindingFlags.NonPublic);
            var unit = new FightBattleUnitState { Id = 1, Hp = 1000, MaxHp = 1000 };
            var checkpoint = new FightBattleCheckpoint();
            checkpoint.Units.Add(unit);
            apply.Invoke(null, new object[] { checkpoint, catalog, profile });
            Require(unit.Hp == 1 && unit.Turn.Hp == 1 && unit.Statuses.Count > 0,
                "Minus 100 percent preserves one HP and binds real official battle statuses");
            profile = new OfflinePlayerProgress();
            profile.PendingStatueBattleEffects.Add(new OfflineStatueBattleEffect
                { EventId = "half-hp", HpPercentChange = -.5f });
            unit.Hp = unit.Turn.Hp = 1000;
            apply.Invoke(null, new object[] { checkpoint, catalog, profile });
            Require(unit.Hp == 500 && unit.Turn.Hp == 500, "Minus 50 percent affects next battle entry");
        }
    }
}
