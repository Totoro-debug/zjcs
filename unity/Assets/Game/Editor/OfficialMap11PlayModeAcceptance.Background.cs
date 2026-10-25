using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11PlayModeAcceptance
    {
        // Scripted callbacks verify state and actual rendered frames only.
        // This mode never invokes the native pointer helper or claims input acceptance.
        public static void RunBackground()
        {
            try { SessionState.SetBool(Key + ".background", true); RunCore(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static IEnumerator VerifyBackground(Transform canvas)
        {
            yield return new WaitForSecondsRealtime(1);
            var save = Field<OfflineMainlineProgressSession>("progressionSave");
            foreach (var region in save.Save.World.Maps.Single(value => value.MapClassId == 11).Regions.ToArray())
                save.SetMapRegionProgress(11, region.Region, 10000);
            save.ApplyInventoryTransaction("background-quickbuy-fixture", progress =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                OfflineInventoryOperations.Grant(progress.Player, 4013, 1, now);
                OfflineInventoryOperations.Grant(progress.Player, 2, 100, now);
            });
            Require(OfficialMap10GameRuntime.ResolveHomeReturnMap(save.Save) == 11, "Home return follows active normal map order");
            VerifyMainPlayerStoryPositions();
            OfficialMap11StoryBranchValidation.Validate(runtime);
            var story = ((IDictionary)Field<object>("stories"))["WorldTreeGuideToNPC"] as Map10RuntimeStory;
            typeof(OfficialMap10GameRuntime).GetField("activeStory", Private).SetValue(runtime, story);
            typeof(OfficialMap10GameRuntime).GetField("activeStoryRootKey", Private).SetValue(runtime, story.key);
            typeof(OfficialMap10GameRuntime).GetField("storyRunning", Private).SetValue(runtime, true);
            Invoke("RunStoryGraph", 14);
            Require(!Field<bool>("storyRunning") && save.StoryCount(story.key) == 1 && save.StoryStatus(story.key) == 2,
                "Unconnected selector Exit ends normally and records exactly one completion");
            IDictionary entities = Field<IDictionary>("mapEntities");
            Invoke("ActivateGoddess", entities[202]);
            Invoke("ActivateGoddess", entities[202]);
            Require(save.Save.World.ActivatedGoddessRegions.Count == 1, "Repeated goddess activation retains one receipt");
            VerifyFurnitureProps(save);
            var furniture = Field<OfflineCharacterProgressionService>("progression")
                .PropertyCalculator.Evaluate(save.Save.Player, DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                .Chunks[CharacterPropHandleType.LostGoddess];
            Require(furniture.Count == 2 && furniture[1001] == 100 && furniture[1005] == 100,
                "Registered goddess property source reads the current durable world state");
            Invoke("Interact", entities[2307]);
            yield return AdvanceScriptedDialogue();
            Require(ItemCount(325) == 1, "First terminal fixed gift");
            yield return CheckRenderedOptions("background-terminal-options");
            Invoke("SelectStoryOption", 1);
            yield return new WaitForSecondsRealtime(.3f);
            Require(ItemCount(325) == 1 && !Field<bool>("storyRunning"), "Free terminal reset");
            Invoke("Interact", entities[2307]);
            yield return AdvanceScriptedDialogue();
            Invoke("SelectStoryOption", 0);
            yield return AdvanceScriptedDialogue();
            yield return CheckRenderedOptions("background-terminal-submit");
            Invoke("SelectStoryOption", 0);
            yield return new WaitForSecondsRealtime(.3f);
            Require(ItemCount(325) == 0 && Field<OfflineMapPuzzleSession>("puzzles").IsUnlocked(2306),
                "Ticket consumed by successful terminal solve");

            // The second terminal has no gift remaining. Follow the original buy branch.
            Invoke("Interact", entities[3220]);
            yield return AdvanceScriptedDialogue();
            Invoke("SelectStoryOption", 0);
            yield return AdvanceScriptedDialogue();
            Invoke("SelectStoryOption", 0);
            yield return new WaitForSecondsRealtime(.7f);
            WorldCostItemConfirmDialog buy = Field<WorldCostItemConfirmDialog>("quickBuyDialog");
            Require(buy != null && buy.ContentText.text.Contains("50"), "Original quick-buy quote");
            Require(buy.Slot.GetComponentInChildren<WorldItemIcon>()?.Icon.sprite != null, "Explicit ticket icon binding");
            Capture("background-quickbuy-confirm");
            yield return new WaitForSecondsRealtime(.2f);
            buy.CancelBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.7f);
            yield return AdvanceScriptedDialogue();
            Require(ItemCount(2) == 100 && ItemCount(325) == 0, "Cancel keeps currency and ticket");
            Invoke("SelectStoryOption", 0);
            yield return new WaitForSecondsRealtime(.7f);
            buy = Field<WorldCostItemConfirmDialog>("quickBuyDialog");
            Require(buy != null, "Retry original purchase branch");
            buy.ConfirmBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            Require(ItemCount(2) == 50 && ItemCount(325) == 0 && !Field<bool>("storyRunning"),
                "Purchase grants one ticket, following solve consumes it, and currency decreases by exactly 50");

            // A third terminal exercises the confirmed insufficient-currency return.
            save = Field<OfflineMainlineProgressSession>("progressionSave");
            save.ApplyInventoryTransaction("background-empty-currency", progress =>
                OfflineInventoryOperations.Consume(progress.Player, 2, 50, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            Invoke("Interact", entities[4207]);
            yield return AdvanceScriptedDialogue();
            Invoke("SelectStoryOption", 0);
            yield return AdvanceScriptedDialogue();
            Invoke("SelectStoryOption", 0);
            yield return new WaitForSecondsRealtime(.7f);
            buy = Field<WorldCostItemConfirmDialog>("quickBuyDialog");
            Require(buy != null, "Insufficient-currency purchase quote");
            buy.ConfirmBtn.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            CommerceScreenController exchange = Field<CommerceScreenController>("commerceScreen");
            Require(exchange != null && exchange.MainWindow == null, "Standalone currency exchange");
            WorldPayMoneyChangeDialog nativeExchange = exchange.GetComponentInChildren<WorldPayMoneyChangeDialog>();
            Require(nativeExchange != null && nativeExchange.FlareStarItemIcon.myTextGo.activeInHierarchy
                && nativeExchange.DiamondItemIcon.myTextGo.activeInHierarchy,
                "Original exchange item quantities are visible");
            Transform fixedLabel = exchange.transform.GetComponentsInChildren<TMP_Text>(true)
                .Single(value => value.name == "TipExchangeTxt").transform;
            Require(fixedLabel.gameObject.activeInHierarchy && fixedLabel.GetComponent<TMP_Text>().text.Contains("50"),
                "Fixed shortage is retained with zero balance");
            Capture("background-currency-exchange");
            yield return new WaitForSecondsRealtime(.2f);
            exchange.CloseCurrentDialog();
            yield return new WaitForSecondsRealtime(.8f);
            yield return AdvanceScriptedDialogue();
            Require(Field<CommerceScreenController>("commerceScreen") == null && Field<bool>("storyOptionsWaiting"),
                "Closing exchange returns to original failure options");
            Require(ItemCount(2) == 0 && ItemCount(325) == 0, "Insufficient purchase does not mutate inventory");
            Debug.Log("Map11 scripted Play Mode passed: original option layout and sprites; gift/free reset/paid solve; quick-buy cancel/success; fixed currency shortage and confirmed return. No native mouse or keyboard input was used. Output: " + Root);
            SessionState.SetInt(Key + ".result", 0);
            EditorApplication.isPlaying = false;
        }

        private static IEnumerator AdvanceScriptedDialogue()
        {
            for (int count = 0; count < 24 && !Field<bool>("storyOptionsWaiting"); count++)
            {
                Require(Field<bool>("dialogueWaiting"), "Original dialogue must precede story options");
                Invoke("RequestDialogueAdvance");
                yield return new WaitForSecondsRealtime(.15f);
            }
            Require(Field<bool>("storyOptionsWaiting"), "Original story options appeared");
            yield return new WaitForSecondsRealtime(.2f);
        }

        private static void VerifyMainPlayerStoryPositions()
        {
            var stories = Field<IDictionary>("stories");
            var subgraphs = Field<IDictionary>("storySubgraphs");
            foreach (string key in new[] { "RankDoorActive", "CombatDoorActive", "FangjianbeiActive", "HolyMonsterActive" })
            {
                var story = (Map10RuntimeStory)(stories[key] ?? subgraphs[key]);
                Require(story != null, key + " is loaded from the original graph or subgraph collection");
                int checkedNodes = 0;
                foreach (Map10RuntimeStoryNode camera in story.nodes.Where(value => value.type == "WorldCameraNode"))
                {
                    int sourceId = camera.ports.FirstOrDefault(value => value.name == "MoveToTargetPos")
                        ?.connections.FirstOrDefault()?.nodeId ?? 0;
                    Map10RuntimeStoryNode source = story.nodes.FirstOrDefault(value => value.id == sourceId);
                    if (source == null || source.type != "GetEntityPosNode" || source.selectGetType != 0) continue;
                    var point = (Vector2)typeof(OfficialMap10GameRuntime).GetMethod("ResolveStoryCameraPoint", Private)
                        .Invoke(runtime, new object[] { story, camera });
                    MapGridPoint player = Field<MapGridPoint>("currentGrid");
                    Require(point == new Vector2(player.X + source.offsetX, player.Y + source.offsetY),
                        key + " resolves MainPlayer and ignores unrelated serialized LoEntityId");
                    checkedNodes++;
                }
                Require(checkedNodes > 0, key + " includes an actual MainPlayer camera query");
            }
            Debug.Log("Map11 camera query validated: official MainPlayer enum 0 in rank door, combat door, monument and holy monster graphs; original offsets retained.");
        }

        private static IEnumerator CheckRenderedOptions(string capture)
        {
            var hud = Field<OfficialMap10UiRenderer>("ui");
            GameObject option = hud.Find("OptionTalkDialog", StoryOptionPath(0));
            TMP_SpriteAsset common = Resources.Load<TMP_SpriteAsset>("Map11/UI/Common");
            Require(common != null && common.spriteSheet != null && common.material.mainTexture == common.spriteSheet,
                "Common atlas and sprite material retain their imported texture binding");
            foreach (TMP_Text text in option.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Require(!text.text.Contains("{0}") && text.text != "MinorText", "No untranslated option placeholders");
                if (text.text.Contains("<sprite="))
                    Require(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(value =>
                            value.elementType == TMP_TextElementType.Sprite && value.isVisible
                            && value.topRight.x > value.bottomLeft.x),
                        "Original Common sprite is rendered by TMP: " + text.text);
            }
            Capture(capture);
            yield return new WaitForSecondsRealtime(.2f);
        }
    }
}
