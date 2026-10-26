using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Spine.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11PlayModeAcceptance
    {
        // Scripted callbacks verify state and actual rendered frames only.
        // This mode never invokes the native pointer helper or claims input acceptance.
        public static void RunBackground()
        {
            SessionState.SetBool(Key + ".ui", false);
            try { SessionState.SetBool(Key + ".background", true); RunCore(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static IEnumerator VerifyBackground(Transform canvas)
        {
            yield return new WaitForSecondsRealtime(1);
            yield return VerifyMapMusic();
            yield return VerifyDialogueAudio();
            var save = Field<OfflineMainlineProgressSession>("progressionSave");
            foreach (var region in save.Save.World.Maps.Single(value => value.MapClassId == 11).Regions.ToArray())
                save.SetMapRegionProgress(11, region.Region, 10000);
            save.ApplyInventoryTransaction("background-quickbuy-fixture", progress =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                OfflineInventoryOperations.Grant(progress.Player, 4013, 1, now);
                OfflineInventoryOperations.Grant(progress.Player, 2, 100, now);
            });
            Require(OfficialMainlineMapRuntime.ResolveHomeReturnMap(save.Save) == 11, "Home return follows active normal map order");
            VerifyMainPlayerStoryPositions();
            VerifyStorySpatialBindings();
            VerifyWorldMonsterViews();
            Capture("background-map-world-monsters");
            yield return new WaitForEndOfFrame();
            yield return CaptureWorldMonsterView(3828);
            OfficialMap11StoryBranchValidation.Validate(runtime);
            var story = ((IDictionary)Field<object>("stories"))["WorldTreeGuideToNPC"] as Map10RuntimeStory;
            OfficialMainlineMapInspection.Set((OfficialMainlineMapRuntime)runtime, "activeStory", story);
            OfficialMainlineMapInspection.Set((OfficialMainlineMapRuntime)runtime, "activeStoryRootKey", story.key);
            OfficialMainlineMapInspection.Set((OfficialMainlineMapRuntime)runtime, "storyRunning", true);
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
            var music = Field<MonoBehaviour>("mapMusic");
            music.GetType().GetMethod("StopAndRelease").Invoke(music, null);
            yield return new WaitForSecondsRealtime(.4f);
            Require(music == null, "Original Stop event releases the local audio object after its fade");
            Debug.Log("Map11 scripted Play Mode passed: original option layout and sprites; gift/free reset/paid solve; quick-buy cancel/success; fixed currency shortage and confirmed return. No native mouse or keyboard input was used. Output: " + Root);
            yield return VerifyStatues(canvas);
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

        private static IEnumerator VerifyMapMusic()
        {
            var music = Field<MonoBehaviour>("mapMusic");
            Require(music != null, "Map11 default BGM binding");
            AudioSource source = music.GetComponent<AudioSource>();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (source.clip == null && Time.realtimeSinceStartup < deadline) yield return null;
            Require(source.clip != null && source.clip.samples > 0 && source.loop && source.spatialBlend == 0f,
                "Original BGM imports as a native looping 2D clip");
            // Keep desktop audio silent while testing the actual Windows mixer.
            source.mute = true;
            runtime.MapMusicVolume = () => .35f;
            yield return new WaitForSecondsRealtime(1.1f);
            Require(source.isPlaying && Mathf.Abs(source.volume - .35f) < .001f,
                "Original Play fade reaches the current music setting");
            music.GetType().GetMethod("Pause").Invoke(music, null);
            yield return new WaitForSecondsRealtime(.4f);
            Require(!source.isPlaying, "Original Pause event pauses after the 250ms fade");
            int pausedSample = source.timeSamples;
            yield return new WaitForSecondsRealtime(.2f);
            Require(source.timeSamples == pausedSample, "Paused music retains its playback position");
            music.GetType().GetMethod("Resume").Invoke(music, null);
            yield return new WaitForSecondsRealtime(.7f);
            Require(source.isPlaying && source.timeSamples > pausedSample
                && Mathf.Abs(source.volume - .35f) < .001f, "Original Resume advances from the paused position");
            runtime.MapMusicVolume = () => 0f;
            Debug.Log("Map11 native audio validated: original looping clip, 1000ms Play, 250ms Pause/Stop, 500ms Resume, retained sample position and current music setting. AudioSource was muted; audible listening was not performed.");
        }

        [Serializable] private sealed class VoiceCatalog { public VoiceBinding[] events; }
        [Serializable] private sealed class VoiceBinding { public string eventKey, clip; }

        private static IEnumerator VerifyDialogueAudio()
        {
            var catalog = JsonUtility.FromJson<VoiceCatalog>(Resources.Load<TextAsset>("Map11/Audio/voices").text);
            var stories = Field<IDictionary>("stories").Values.Cast<Map10RuntimeStory>()
                .Concat(Field<IDictionary>("storySubgraphs").Values.Cast<Map10RuntimeStory>()).ToArray();
            string[] expected = stories.SelectMany(value => value.dialogueGroups).SelectMany(value => value.Lines)
                .Where(value => value.DialogType == "Voice" && !string.IsNullOrEmpty(value.VoiceEventKey))
                .Select(value => value.VoiceEventKey).Distinct().OrderBy(value => value).ToArray();
            Require(expected.Length == 207 && expected.SequenceEqual(catalog.events.Select(value => value.eventKey).OrderBy(value => value)),
                "All original main-graph and subgraph dialogue voice keys have explicit bindings");
            foreach (VoiceBinding binding in catalog.events)
            {
                ResourceRequest request = Resources.LoadAsync<AudioClip>(binding.clip);
                yield return request;
                var clip = request.asset as AudioClip;
                Require(clip != null && clip.samples > 0 && clip.loadType == AudioClipLoadType.Streaming,
                    "Original dialogue clip imports for native streaming: " + binding.eventKey);
            }
            var voice = Field<MonoBehaviour>("mapVoice");
            AudioSource source = voice.GetComponent<AudioSource>();
            source.mute = true;
            runtime.MapVoiceVolume = () => .4f;
            Invoke("PlayStory", "FirstMeetSenSen", 0, null, null);
            yield return new WaitForSecondsRealtime(.4f);
            var story = stories.Single(value => value.key == "FirstMeetSenSen");
            string first = catalog.events.Single(value => value.eventKey == story.dialogueGroups[0].Lines[0].VoiceEventKey).clip;
            Require(source.isPlaying && source.clip.name == first.Substring(first.LastIndexOf('/') + 1)
                && Mathf.Abs(source.volume - .4f) < .001f, "Actual dialogue starts its original voice at the current voice volume");
            Invoke("AdvanceDialogue");
            // TalkDialog switches content and voice at the portrait animation's
            // original half-way event. A cached clip can also load synchronously.
            string second = catalog.events.Single(value => value.eventKey == story.dialogueGroups[0].Lines[1].VoiceEventKey).clip;
            Require(Field<bool>("dialogueContentSwitchWaiting") || source.clip == null
                || source.clip.name == second.Substring(second.LastIndexOf('/') + 1),
                "Voice replacement waits only for the original content-switch event");
            yield return new WaitForSecondsRealtime(.4f);
            Require(source.isPlaying && source.clip.name == second.Substring(second.LastIndexOf('/') + 1),
                "Next dialogue plays the next original voice");

            var textStory = stories.First(value => value.dialogueGroups.Any(group => group.Lines.Length > 0 && group.Lines[0].DialogType == "Text"));
            var textGroup = textStory.dialogueGroups.First(value => value.Lines.Length > 0 && value.Lines[0].DialogType == "Text");
            Invoke("PlayStory", "FirstMeetSenSen", 0, null, null);
            Invoke("PlayStory", textStory.key, textGroup.Id, null, null);
            yield return new WaitForSecondsRealtime(.4f);
            Require(!source.isPlaying && source.clip == null, "Text-only dialogue stops voice and rejects late asynchronous voice loads");
            Invoke("PlayStory", "CombatAssistance", 0, null, null);
            yield return new WaitForSecondsRealtime(.4f);
            Require(source.isPlaying, "Actual public-subgraph dialogue resolves and plays its original voice");
            Invoke("AdvanceDialogue");
            Require(!Field<bool>("dialogueWaiting") && source.clip == null && !source.isPlaying,
                "Closing the original talk panel stops its dialogue voice");
            runtime.MapVoiceVolume = () => 0f;
            Debug.Log("Map11 dialogue audio validated: all 207 original clips import for Windows streaming; actual main/subgraph dialogue playback, next-line replacement, text-only stop, stale async-load cancellation, panel close and current voice volume. Sources were muted; audible listening and native input remain unverified.");
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
                    var point = (Vector2)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ResolveStoryCameraPoint", new object[] { story, camera });
                    MapGridPoint player = Field<MapGridPoint>("currentGrid");
                    Require(point == new Vector2(player.X + source.offsetX, player.Y + source.offsetY),
                        key + " resolves MainPlayer and ignores unrelated serialized LoEntityId");
                    checkedNodes++;
                }
                Require(checkedNodes > 0, key + " includes an actual MainPlayer camera query");
            }
            Debug.Log("Map11 camera query validated: official MainPlayer enum 0 in rank door, combat door, monument and holy monster graphs; original offsets retained.");
        }

        private static void VerifyStorySpatialBindings()
        {
            IDictionary entities = Field<IDictionary>("mapEntities");
            var worldTree = entities.Values.Cast<object>().Single(value =>
                (int)value.GetType().GetField("EntityClassId", BindingFlags.Instance | BindingFlags.Public).GetValue(value) == 6101);
            const BindingFlags EntityFields = BindingFlags.Instance | BindingFlags.Public;
            int placementId = (int)worldTree.GetType().GetField("PlacementId", EntityFields).GetValue(worldTree);
            MapGridPoint worldTreeGrid = (MapGridPoint)worldTree.GetType().GetField("Grid", EntityFields).GetValue(worldTree);
            OfficialMap10EntityView view = Field<OfficialMap10EntityView>("entityView");
            GameObject instance = view.FindEntityObject(placementId);
            Require(instance != null, "World Tree registered entity instance exists");
            bool active = instance.activeSelf;
            instance.SetActive(true);
            Vector2 target = (Vector2)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ResolveRegisteredStoryPoint", new object[] { "WorldTree" });
            Require(target == new Vector2(worldTreeGrid.X, worldTreeGrid.Y),
                "WorldTree camera target resolves the original 6101 StoryNodeRegister root");
            instance.SetActive(active);

            MapGridPoint grid = Field<MapGridPoint>("currentGrid");
            GameObject avatar = Field<GameObject>("avatar");
            Vector3 position = avatar.transform.position;
            OfficialWorldMapSession session = Field<OfficialWorldMapSession>("session");
            OfficialMapVector3 origin = session.Definition.Navigation.Origin;
            try
            {
                OfficialMainlineMapInspection.Set((OfficialMainlineMapRuntime)runtime, "currentGrid", new MapGridPoint(-1000, -1000));
                avatar.transform.position = new Vector3(origin.X - 1000, origin.Y - 1000, position.z);
                Vector2 fallback = (Vector2)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ResolveStoryTalkCenter", null);
                Require(fallback == new Vector2(-1000, -1001),
                    "No nearby original StoryTalk tags fall back to player position and StorySettings offset");
            }
            finally
            {
                OfficialMainlineMapInspection.Set((OfficialMainlineMapRuntime)runtime, "currentGrid", grid);
                avatar.transform.position = position;
            }
            Debug.Log("Map11 spatial bindings validated: official 6101 WorldTree register, player fallback, and StorySettings offset.");
        }

        private static void VerifyWorldMonsterViews()
        {
            IDictionary views = Field<IDictionary>("worldMonsterViews");
            IDictionary followers = Field<IDictionary>("worldMonsterFollowers");
            IDictionary entities = Field<IDictionary>("mapEntities");
            IDictionary rosters = Field<IDictionary>("rosters");
            OfficialBattleModelCatalog catalog = OfficialBattleModelCatalog.LoadDefault();
            Require(views.Count == 6, "All six official Map11 dynamic monster placements have model views");

            foreach (DictionaryEntry entry in views)
            {
                int placementId = (int)entry.Key;
                var skeleton = entry.Value as SkeletonAnimation;
                Require(skeleton != null && skeleton.Skeleton != null && skeleton.AnimationState != null,
                    "World monster skeleton initializes for placement " + placementId);
                Require(skeleton.transform.localPosition == Vector3.zero
                    && skeleton.transform.localScale == new Vector3(0.3f, 0.3f, 1f),
                    "World monster retains the official ModelSlot origin and prefab root scale");
                Transform modelRoot = skeleton.transform.parent.parent;
                Require(skeleton.transform.parent.name == "ModelSlot" && modelRoot.name == "Model"
                    && modelRoot.localPosition == new Vector3(0f, -0.5f, -0.5f)
                    && modelRoot.parent.name == "5011_view",
                    "World monster retains the official 5110 model mount hierarchy and offset");
                MeshRenderer meshRenderer = skeleton.GetComponent<MeshRenderer>();
                Require(meshRenderer != null && meshRenderer.enabled,
                    "World monster skeleton renderer is active");
                Material[] materials = meshRenderer.sharedMaterials;
                Require(materials.Length > 0 && materials.All(value => value != null
                    && value.shader != null && value.shader.isSupported && value.mainTexture != null),
                    "World monster atlas materials resolve to supported textured shaders");

                bool hasShadowBone = skeleton.Skeleton.FindBone("shadow") != null;
                Require(followers.Contains(placementId) == hasShadowBone,
                    "World monster shadow follows the original loader bone condition");
                if (hasShadowBone)
                {
                    var follower = (BoneFollower)followers[placementId];
                    SpriteRenderer renderer = follower.GetComponentInChildren<SpriteRenderer>(true);
                    Require(follower.boneName == "shadow" && follower.followBoneRotation
                        && follower.followLocalScale && !follower.followSkeletonFlip
                        && follower.transform.parent.name == "Shadow",
                        "World monster shadow retains the official follower settings and mount");
                    Require(renderer != null && renderer.enabled && renderer.sprite != null
                        && renderer.sortingOrder == -10
                        && renderer.transform.localScale == new Vector3(0.3f, 0.3f, 1f),
                        "World monster shadow uses the original sprite, sort order and prefab scale");
                }

                object entity = entities[placementId];
                int stageId = (int)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ResolveEntityStageId", new[] { entity });
                var roster = (Map10RuntimeRoster)rosters[stageId];
                int monsterId = roster.Waves.SelectMany(value => value.Units).First().MonsterId;
                OfficialBattleModelBinding binding = catalog.Find(catalog.ResolveModelKey(
                    new FightBattleUnitState { MonsterId = monsterId }));
                string idle = binding.animations.Single(value => value.name == "idle_01").spineName;
                var current = skeleton.AnimationState.GetCurrent(0);
                Require(current != null && current.Animation.Name == idle && current.Loop,
                    "World monster plays its official idle animation for placement " + placementId);
            }
            Debug.Log("Map11 world monsters validated: six stage-bound instances, five official skeleton identities, idle animation and original shadow bindings.");
        }

        private static IEnumerator CaptureWorldMonsterView(int placementId)
        {
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            GameObject monster = view.FindEntityObject(placementId);
            Require(monster != null, "World monster render target exists");
            Camera camera = Field<Camera>("worldCamera");
            Vector3 previousCameraPosition = camera.transform.position;
            bool runtimeWasEnabled = runtime.enabled;
            runtime.enabled = false;
            camera.transform.position = new Vector3(monster.transform.position.x,
                monster.transform.position.y, previousCameraPosition.z);
            yield return new WaitForEndOfFrame();
            Capture("background-world-monster-" + placementId);
            yield return new WaitForSecondsRealtime(0.3f);
            camera.transform.position = previousCameraPosition;
            runtime.enabled = runtimeWasEnabled;
        }

        private static IEnumerator CheckRenderedOptions(string capture)
        {
            var hud = Field<OfficialWorldUiRenderer>("ui");
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
