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
            if (repair) ClickPartnerUi(repair.RepairBtn);
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
            // Close the original repaired notification via its actual button.
            deadline = Time.realtimeSinceStartup + 15;
            FurnitureRepairedNotifyDialog notify;
            while (Time.realtimeSinceStartup < deadline)
            {
                notify = runtime.Hud.Canvas.GetComponentInChildren<FurnitureRepairedNotifyDialog>();
                if (notify && notify.GetComponent<WorldPopupWindow>().State == WorldPopupWindow.EState.Show) break;
                yield return null;
            }
            notify = runtime.Hud.Canvas.GetComponentInChildren<FurnitureRepairedNotifyDialog>();
            passed &= Check("fruit-unlock-notification", () =>
            {
                Assert(notify, "original fruit-tree repaired notification is active");
                return "original repaired notification waits for its close interaction";
            });
            if (!passed) { completed(false); yield break; }
            if (notify)
            {
                yield return Capture("fruit-unlock-repaired-notification.png");
                Button close = notify.GetComponentsInChildren<Button>().First(value => value.name.Contains("Close"));
                ClickPartnerUi(close);
            }
            deadline = Time.realtimeSinceStartup + 10;
            while (notify && Time.realtimeSinceStartup < deadline) yield return null;
            passed &= Check("fruit-unlock-notification-closed", () =>
            {
                Assert(!notify, "simulated close input releases the original repaired notification");
                return "native repaired notification closes before the next graph dialogue";
            });
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

        private IEnumerator ClickUnlockTree(HomeFeatureRuntime runtime, GameObject tree, bool repairing)
        {
            Physics.SyncTransforms();
            Vector2 point = default;
            var candidates = new System.Collections.Generic.List<Vector2>();
            foreach (var collider in tree.GetComponentsInChildren<Collider>().Where(value => value.enabled))
            {
                var sub = collider.GetComponentInParent<HomeFruitTreeSubFurniture>();
                if (repairing && (!sub || sub.Furniture != EFurniture.FruitTree)) continue;
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
