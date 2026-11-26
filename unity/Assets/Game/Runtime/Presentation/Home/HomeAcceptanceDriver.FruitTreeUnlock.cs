using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeFruitTreeWorld;
using Zjcs.Official.HomeFruitTreeNative;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.HomeUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeAcceptanceDriver
    {
        private IEnumerator VerifyNativeFruitTreeUnlock(HomeFeatureRuntime runtime, Action<bool> completed)
        {
            bool passed = Check("fruit-unlock-initial", () =>
            {
                Assert(!runtime.Profile.Save.Player.FruitTree.ProductionRepaired && runtime.StoryStatus("FruitTreeUnlock1") == 0,
                    "isolated city starts with an unrepaired production tree and unstarted original story");
                return "original unlock conditions are present without repaired stock or story completion";
            });
            GameObject tree = runtime.NativeFurnitureInstance(EFurniture.FruitTree);
            var produce = tree.GetComponentInChildren<HomeFruitTreeProduceFruit>(true);
            passed &= Check("fruit-unlock-condition", () =>
            {
                var info = produce.Services.GetFurnitureInfo(produce.Furniture);
                Assert(produce.GetState() == FurnitureBase.EFurnitureState.CanRepair, "original production sub-building reaches CanRepair; actual=" + produce.GetState()
                    + "; type=" + produce.Furniture + "; repair=" + produce.Services.PlayerData.IsMetFurnitureRepairCondition(info)
                    + "; notice=" + produce.Services.PlayerData.IsMetFurnitureNoticeCondition(info)
                    + "; level=" + runtime.Profile.Save.Player.Level + "; rank=" + runtime.Profile.Save.Player.SubRank
                    + "; siblings=" + string.Join(",", tree.GetComponentsInChildren<HomeFruitTreeSubFurniture>(true).Select(value => value.Furniture + ":" + value.GetState())));
                return "native condition evaluation exposes the repair entry";
            });
            if (!passed) { completed(false); yield break; }
            // The official graph owns camera placement after starting. Position
            // only this initial input through the already verified drag seam.
            CenterUnlockTree(runtime, tree);
            yield return ClickUnlockTree(runtime, tree, true);
            float deadline = Time.realtimeSinceStartup + 15;
            while (!runtime.IsFurnitureStoryRunning && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("fruit-unlock-story-start", () =>
            {
                Assert(runtime.IsFurnitureStoryRunning && runtime.StoryStatus("FruitTreeUnlock1") == 1,
                    "native scene repair click starts and persists the original graph");
                return "official graph is running from the native production-side collider";
            });
            bool firstDialog = false;
            yield return AdvanceUnlockDialogue(runtime, "fruit-unlock-before-repair", value => firstDialog = value);
            passed &= firstDialog;
            if (!passed) { completed(false); yield break; }
            deadline = Time.realtimeSinceStartup + 12;
            while (runtime.Hud.Canvas.GetComponentsInChildren<OptionTalkDialogPanel>().Any()
                && Time.realtimeSinceStartup < deadline) yield return null;
            yield return ClickUnlockTree(runtime, tree, true);
            deadline = Time.realtimeSinceStartup + 8;
            FurnitureRepairDialog repair;
            while (!(repair = runtime.Hud.Canvas.GetComponentInChildren<FurnitureRepairDialog>())
                && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("fruit-unlock-repair-window", () =>
            {
                Assert(repair && repair.RepairBtn, "original graph opens the native fruit-tree repair window");
                return "repair mask and original confirmation button are available";
            });
            yield return Capture("fruit-unlock-repair-window.png");
            if (!passed) { completed(false); yield break; }
            bool repairTimeline = false;
            yield return VerifyUnlockRepairTimeline(runtime, repair, tree.GetComponentInChildren<HomeFruitTreeProduceFruit>(true),
                "fruit-unlock", value => repairTimeline = value);
            passed &= repairTimeline;
            if (!passed) { completed(false); yield break; }
            deadline = Time.realtimeSinceStartup + 15;
            while (!runtime.Profile.Save.Player.FruitTree.ProductionRepaired && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("fruit-unlock-repaired", () =>
            {
                runtime.Profile.Reload();
                Assert(runtime.Profile.Save.Player.FruitTree.ProductionRepaired && runtime.Profile.Save.Player.FruitTree.StoredCount == 10,
                    "committed production repair grants the original forty-hour initial fruit stock");
                return "repair and initial output persist before collection";
            });
            if (!passed) { completed(false); yield break; }
            bool notification = false;
            yield return CloseUnlockRepairNotification(runtime, "fruit-unlock", value => notification = value);
            passed &= notification;
            if (!passed) { completed(false); yield break; }
            bool secondDialog = false;
            yield return AdvanceUnlockDialogue(runtime, "fruit-unlock-after-repair", value => secondDialog = value);
            passed &= secondDialog;
            yield return null;
            long rewardBefore = runtime.Profile.InventoryCount(OfflineFruitTreeService.RewardItemId);
            yield return ClickUnlockTree(runtime, tree, false);
            bool lastDialog = false;
            yield return AdvanceUnlockDialogue(runtime, "fruit-unlock-after-collection", value => lastDialog = value);
            passed &= lastDialog;
            deadline = Time.realtimeSinceStartup + 12;
            while (runtime.IsFurnitureStoryRunning && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("fruit-unlock-completed", () =>
            {
                runtime.Profile.Reload();
                var state = runtime.Profile.Save.Player.FruitTree;
                Assert(!runtime.IsFurnitureStoryRunning && runtime.StoryStatus("FruitTreeUnlock1") == 2
                    && runtime.StoryCount("FruitTreeUnlock1") == 1, "original story finishes exactly once after collection and final dialogue");
                Assert(state.StoredCount == 0 && state.TotalCollected == 10 && state.CollectionCount == 1
                    && state.ProductionRepairStorySeen && state.CollectionTutorialSeen
                    && runtime.Profile.InventoryCount(OfflineFruitTreeService.RewardItemId) == rewardBefore + 10,
                    "repair, collection, tutorial completion and rewards restore from the same isolated save");
                return "original production unlock graph, native repair, ten-fruit collection and completion are committed";
            });
            yield return Capture("fruit-unlock-completed.png");
            if (passed)
            {
                long reward = runtime.Profile.InventoryCount(OfflineFruitTreeService.RewardItemId);
                runtime.Close();
                yield return null;
                var slots = new ProfileSlotCatalog(Root);
                var restored = HomeFeatureRuntime.BeginWithNativePointer(slots.GetSlotIds().First(), Root, null,
                    new AcceptanceNativePointer(), null, null);
                restored.TutorialAutoStartEnabled = false;
                for (int frame = 0; frame < 5; frame++) yield return null;
                passed &= Check("fruit-unlock-restored", () =>
                {
                    Assert(restored.NativeFurnitureInstance(EFurniture.FruitTree).GetComponentInChildren<HomeFruitTreeProduceFruit>(true).GetState()
                        == FurnitureBase.EFurnitureState.Functional && !restored.IsFurnitureStoryRunning
                        && restored.StoryStatus("FruitTreeUnlock1") == 2 && restored.StoryCount("FruitTreeUnlock1") == 1,
                        "cold city reopening restores native production ownership and the completed story");
                    Assert(restored.Profile.Save.Player.FruitTree.StoredCount == 0
                        && restored.Profile.Save.Player.FruitTree.TotalCollected == 10
                        && restored.Profile.InventoryCount(OfflineFruitTreeService.RewardItemId) == reward,
                        "cold reopening neither repeats the initial stock nor grants the reward again");
                    return "native production state, one completion and one reward survive a full city reopening";
                });
                restored.Close();
            }
            completed(passed);
        }

        private IEnumerator VerifyUnlockRepairTimeline(HomeFeatureRuntime runtime, FurnitureRepairDialog repair,
            FurnitureBase furniture, string prefix, Action<bool> completed)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var effect = (GameObject)typeof(FurnitureBase).GetField("mRepairEffect", fields).GetValue(furniture);
            var stateField = typeof(FurnitureBase).GetField("mFurnitureState", fields);
            float showTime = (float)typeof(FurnitureBase).GetField("mRepairEffectShowTime", fields).GetValue(furniture);
            float fullTime = (float)typeof(FurnitureBase).GetField("mRepairEffectFullTime", fields).GetValue(furniture);
            var initialState = (FurnitureBase.EFurnitureState)stateField.GetValue(furniture);
            bool setup = Check(prefix + "-repair-timeline-source", () =>
            {
                Assert(effect && !effect.activeSelf && Mathf.Abs(showTime - .6f) < .0001f
                    && Mathf.Abs(fullTime - 1.5f) < .0001f, "original native repair effect and serialized timings are preserved");
                Assert(initialState == FurnitureBase.EFurnitureState.CanRepair,
                    "native visual state is repairable before the actual button press");
                return "original effect reveals at 0.6 seconds and finishes at 1.5 seconds";
            });
            if (!setup) { completed(false); yield break; }
            float started = -1, revealed = -1, finished = -1, largestFrame = 0;
            int repairEvents = 0, homeSwitches = 0, peakParticles = 0;
            bool premature = false, earlyActive = false, revealedUnderEffect = false;
            var frames = new System.Collections.Generic.Dictionary<string, Texture2D>();
            Action<EFurniture> repaired = type =>
            {
                if (type != furniture.Furniture) return;
                repairEvents++;
                started = Time.time;
                premature |= (FurnitureBase.EFurnitureState)stateField.GetValue(furniture) != initialState;
                earlyActive = effect.activeInHierarchy;
            };
            IDisposable switchSubscription = furniture.Services.SubscribePostHomeChange(() => homeSwitches++);
            furniture.Services.Player.OnRepairFurniture += repaired;
            try
            {
                ClickPartnerUi(repair.RepairBtn);
                float deadline = Time.realtimeSinceStartup + 12;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForEndOfFrame();
                    if (started < 0) continue;
                    float elapsed = Time.time - started;
                    largestFrame = Mathf.Max(largestFrame, Time.deltaTime);
                    bool functional = (FurnitureBase.EFurnitureState)stateField.GetValue(furniture)
                        == FurnitureBase.EFurnitureState.Functional;
                    premature |= functional && elapsed + .001f < showTime;
                    if (functional && revealed < 0)
                    {
                        revealed = elapsed;
                        revealedUnderEffect = effect.activeInHierarchy;
                    }
                    peakParticles = Math.Max(peakParticles,
                        effect.GetComponentsInChildren<ParticleSystem>(true).Sum(value => value.particleCount));
                    string phase = elapsed < showTime ? "before-reveal" : effect.activeSelf ? "revealed" : "finished";
                    if (!frames.ContainsKey(phase))
                    {
                        frames.Add(phase, CaptureUnlockRepairScene(runtime.Scene.Camera));
                        log.Add("VIEW " + prefix + "-repair-" + phase + " scaled=" + elapsed.ToString("F3")
                            + " visual=" + stateField.GetValue(furniture) + " active=" + effect.activeInHierarchy);
                    }
                    if (!effect.activeSelf)
                    {
                        finished = elapsed;
                        break;
                    }
                }
            }
            finally
            {
                furniture.Services.Player.OnRepairFurniture -= repaired;
                switchSubscription.Dispose();
                foreach (var frame in frames)
                {
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(Output, prefix + "-repair-" + frame.Key + ".png"),
                        frame.Value.EncodeToPNG());
                    Destroy(frame.Value);
                }
            }
            completed(Check(prefix + "-repair-timeline", () =>
            {
                float tolerance = largestFrame * 2 + .08f;
                Assert(repairEvents == 1 && homeSwitches == 0, "one committed repair does not masquerade as a home switch");
                Assert(earlyActive && !premature && frames.Count == 3, "original effect starts over the unrepaired visual state");
                Assert(revealed >= showTime && revealed <= showTime + tolerance && revealedUnderEffect,
                    "native model changes under the active effect at its original reveal time");
                Assert(finished >= fullTime && finished <= fullTime + tolerance && !effect.activeSelf && peakParticles > 0,
                    "original particles run and the effect root closes at its original full duration");
                return "repair=" + repairEvents + " homeSwitches=" + homeSwitches + " reveal=" + revealed.ToString("F3")
                    + " finish=" + finished.ToString("F3") + " maxFrame=" + largestFrame.ToString("F3")
                    + " peakParticles=" + peakParticles;
            }));
        }

        private static Texture2D CaptureUnlockRepairScene(Camera camera)
        {
            // Render the actual scene camera without the original notification
            // overlay obscuring the building. Preserve the live camera surface.
            RenderTexture original = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                frame.Apply();
                return frame;
            }
            finally
            {
                camera.targetTexture = original;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private IEnumerator CloseUnlockRepairNotification(HomeFeatureRuntime runtime, string prefix, Action<bool> completed)
        {
            bool passed = true;
            // Close the original repaired notification via its actual button.
            float deadline = Time.realtimeSinceStartup + 15;
            FurnitureRepairedNotifyDialog notify;
            while (Time.realtimeSinceStartup < deadline)
            {
                notify = runtime.Hud.Canvas.GetComponentInChildren<FurnitureRepairedNotifyDialog>();
                if (notify && notify.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show) break;
                yield return null;
            }
            notify = runtime.Hud.Canvas.GetComponentInChildren<FurnitureRepairedNotifyDialog>();
            passed &= Check(prefix + "-notification", () =>
            {
                Assert(notify, "original fruit-tree repaired notification is active");
                return "original repaired notification waits for its close interaction";
            });
            if (!passed) { completed(false); yield break; }
            if (notify)
            {
                yield return Capture(prefix + "-repaired-notification.png");
                Button close = notify.GetComponentsInChildren<Button>().First(value => value.name.Contains("Close"));
                ClickPartnerUi(close);
            }
            deadline = Time.realtimeSinceStartup + 10;
            while (notify && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check(prefix + "-notification-closed", () =>
            {
                Assert(!notify, "simulated close input releases the original repaired notification");
                return "native repaired notification closes before the next graph dialogue";
            });
            if (!passed) { completed(false); yield break; }
            completed(passed);
        }

        private static void CenterUnlockTree(HomeFeatureRuntime runtime, GameObject tree)
        {
            Vector3 current = runtime.Scene.Camera.transform.position;
            var input = PrivateField<object>(runtime, "input");
            float units = 2f * runtime.Scene.OrthographicSize / Screen.height * runtime.Scene.DragSensitivity;
            Vector2 start = new Vector2(Screen.width * .5f, Screen.height * .5f);
            Vector2 delta = new Vector2(-(tree.transform.position.x - current.x) / units,
                -(tree.transform.position.y - current.y) / units);
            Type inputType = input.GetType();
            inputType.GetMethod("PointerDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { start, false });
            inputType.GetMethod("PointerMove", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { start + delta });
            inputType.GetMethod("PointerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { start + delta, false });
        }

        private IEnumerator ClickUnlockTree(HomeFeatureRuntime runtime, GameObject tree, bool repairing,
            EFurniture repairType = EFurniture.FruitTree)
        {
            Physics.SyncTransforms();
            Vector2 point = default;
            var candidates = new System.Collections.Generic.List<Vector2>();
            foreach (var collider in tree.GetComponentsInChildren<Collider>().Where(value => value.enabled))
            {
                var sub = collider.GetComponentInParent<HomeFruitTreeSubFurniture>();
                if (repairing && (!sub || sub.Furniture != repairType)) continue;
                if (!repairing && collider.GetComponentInParent<HomeFruitTreePet>()) continue;
                foreach (float x in new[] { .5f, .25f, .75f })
                foreach (float y in new[] { .5f, .25f, .75f })
                {
                    var sample = collider.bounds.min + Vector3.Scale(collider.bounds.size, new Vector3(x, y, .5f));
                    var screen = runtime.Scene.Camera.WorldToScreenPoint(sample);
                    if (!runtime.Scene.Camera.pixelRect.Contains(screen)) continue;
                    if (!Physics.Raycast(runtime.Scene.Camera.ScreenPointToRay(screen), out var hit, 1000f)) continue;
                    if (repairing && hit.collider.GetComponentInParent<HomeFruitTreeSubFurniture>() != sub) continue;
                    candidates.Add(screen);
                }
            }
            Assert(candidates.Count > 0, "native tree has an unobstructed collider for the current story interaction");
            var uiHits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            float deadline = Time.realtimeSinceStartup + 8;
            bool allowed = false;
            do
            {
                foreach (Vector2 candidate in candidates)
                {
                    uiHits.Clear();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(
                        new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = candidate }, uiHits);
                    if (uiHits.Count == 0) { point = candidate; allowed = true; break; }
                }
                if (allowed) break;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            Assert(allowed, "native story mask permits the scene pointer; blockers="
                + string.Join(",", uiHits.Select(value => value.gameObject.name)));
            nativeScenePointer.Press(point); yield return null;
            nativeScenePointer.Hold(point); yield return null;
            nativeScenePointer.Release(point); yield return null;
            nativeScenePointer.Clear();
        }

        private IEnumerator AdvanceUnlockDialogue(HomeFeatureRuntime runtime, string prefix, Action<bool> completed)
        {
            float deadline = Time.realtimeSinceStartup + 18;
            OptionTalkDialogPanel panel = null;
            while (!(panel = runtime.Hud.Canvas.GetComponentInChildren<OptionTalkDialogPanel>())
                && Time.realtimeSinceStartup < deadline) yield return null;
            if (!panel) { completed(Check(prefix, () => { Assert(false, "original story dialogue becomes visible"); return ""; })); yield break; }
            bool captured = false;
            while (panel && panel.gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
            {
                var next = PrivateField<Button>(panel, "mNextBtn");
                var text = PrivateField<WorldMultiLanguageText>(panel, "mTalkContentTxt").GetComponent<TMPro.TMP_Text>();
                if (next.gameObject.activeInHierarchy && text.canvasRenderer.GetInheritedAlpha() >= .95f)
                {
                    if (!captured) { yield return Capture(prefix + ".png"); captured = true; }
                    ClickPartnerUi(next);
                    yield return new WaitForSecondsRealtime(.25f);
                }
                yield return null;
            }
            completed(Check(prefix, () => { Assert(captured && !panel, "native dialogue closes after simulated pointer progression"); return "original portrait, text and next button exercised"; }));
        }
    }
}
