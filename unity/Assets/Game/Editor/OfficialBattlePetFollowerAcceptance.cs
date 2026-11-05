using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattlePetFollowerAcceptance
    {
        [Serializable] private sealed class Data { public OfficialBattlePetFollowerCatalog.Binding[] models; }

        public static void Verify()
        {
            var catalog = OfficialBattlePetFollowerCatalog.Load();
            var rows = JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("Combat/pet-followers").text).models;
            foreach (var row in rows)
            {
                var root = new GameObject("Pet follower validation " + row.entityId);
                root.transform.position = new Vector3(1000, 1000, 0);
                var owner = new GameObject("Owner"); owner.transform.SetParent(root.transform, false);
                owner.transform.localScale = new Vector3(2, 3, 1);
                var state = new FightBattleUnitState { Id = 1, X = 0, Y = 0 };
                using (var scope = new OfflineBattleNativePlayableScope(root.transform, null))
                using (var view = new OfficialBattlePetFollowerView(catalog.Find(row.petClassId), state,
                    owner.transform, owner.transform, root.transform,
                    point => root.transform.TransformPoint(new Vector3(point.X, point.Y, 5)), scope))
                {
                    var renderer = view.Transform.GetComponentInChildren<BattleNativeMonsterRenderer>(true);
                    Require(renderer && renderer.Animator.runtimeAnimatorController.name.Contains(row.entityId.ToString()), "Wrong native model");
                    var start = view.Transform.localPosition;
                    Require(Mathf.Abs(start.x + .3f) < .0001f && start.y == 0 && start.z == 0, "Original standing offset or Z changed");
                    state.X = 1; view.Refresh(state);
                    state.X = 3; state.Y = 1; view.Refresh(state);
                    view.Tick(.05f); scope.Tick(.05f);
                    Require(view.Transform.localPosition == start, "Follower ignored original start delay");
                    Require(renderer.Animator.GetBool("run"), "Original movement graph did not set run");
                    view.Transform.gameObject.SetActive(false);
                    view.Tick(1f); scope.Tick(1f);
                    Require(view.Transform.localPosition == start, "Hidden childhood entity advanced its component clock");
                    view.Transform.gameObject.SetActive(true);
                    renderer.gameObject.SetActive(false);
                    view.Tick(.01f); scope.Tick(.01f);
                    renderer.gameObject.SetActive(true);
                    for (int i = 0; i < 120; i++) { view.Tick(.02f); scope.Tick(.02f); scope.LateTick(); }
                    Require(Vector3.Distance(view.Transform.localPosition, new Vector3(2.7f, 1, 0)) < .0001f, "Latest queued owner position was not reached");
                    Require(!renderer.Animator.GetBool("run"), "Original movement graph did not restore run");
                    owner.transform.localScale = new Vector3(-2, 3, 1); view.Refresh(state);
                    Require(view.Transform.localScale.x == -1 && owner.transform.localScale == new Vector3(-2, 3, 1), "Pet facing changed owner scale");
                    state.X = -5; state.Y = -2; view.Refresh(state);
                    Require(Vector3.Distance(view.Transform.localPosition, new Vector3(-4.7f, -2, 0)) > 1, "Disabled teleport threshold teleported the pet");
                    for (int i = 0; i < 160; i++) { view.Tick(.02f); scope.Tick(.02f); scope.LateTick(); }
                    Require(Vector3.Distance(view.Transform.localPosition, new Vector3(-4.7f, -2, 0)) < .0001f, "Long follow movement did not settle");
                    Require(owner.transform.localScale == new Vector3(-2, 3, 1), "Following changed owner scale");
                    var transferBinding = OfficialBattlePetTransferCatalog.Load().Find(row.entityId);
                    var groups = transferBinding.graphGroups.Select(path => GraphGroup(view.Transform, path)).ToArray();
                    view.Refresh(state, true);
                    view.Tick(transferBinding.transferTime + .001f); scope.Tick(.01f); scope.LateTick();
                    Require(groups.All(group => !group.activeSelf) && view.Transform.gameObject.activeSelf,
                        "Entityized pet did not hide precisely its original graph groups");
                    view.Refresh(state, false);
                    view.Tick(transferBinding.transferTime + .001f); scope.Tick(.01f); scope.LateTick();
                    Require(groups.All(group => group.activeSelf), "Ending entityization did not restore the original childhood graph groups");
                    view.Refresh(state, true); view.Tick(.01f);
                    view.Refresh(state, false); view.Tick(.01f);
                    view.Refresh(state, true); view.Tick(transferBinding.transferTime + .001f);
                    Require(groups.All(group => !group.activeSelf), "Interrupted transfer left a stale visibility timer");
                    view.Refresh(state, false); view.Tick(transferBinding.transferTime + .001f);
                    Require(groups.All(group => group.activeSelf), "Repeated transfer failed to restore childhood rendering");
                }
                using (var scope = new OfflineBattleNativePlayableScope(root.transform, null))
                using (var restored = new OfficialBattlePetFollowerView(catalog.Find(row.petClassId), state,
                    owner.transform, owner.transform, root.transform,
                    point => root.transform.TransformPoint(new Vector3(point.X, point.Y, 5)), scope, initialTransfer: true))
                {
                    var groups = OfficialBattlePetTransferCatalog.Load().Find(row.entityId).graphGroups;
                    Require(groups.All(path => !GraphGroup(restored.Transform, path).activeSelf),
                        "Restored entityization briefly exposed childhood graph groups");
                }
                UnityEngine.Object.Destroy(root);
            }
            Require(rows.Length == 21 && rows.Select(row => row.entityId).Distinct().Count() == 21, "Incomplete native follower fixture");
            Debug.Log("BATTLE_PET_FOLLOWERS_PASSED models=21 delay/queue/run/facing/nonteleport/Z/hidden/disposal");
            Debug.Log("BATTLE_PET_TRANSFERS_PASSED models=21 hide/show/interruption/restore/disposal");
        }

        private static GameObject GraphGroup(Transform root, string path)
            => path.EndsWith("/ShadowBoneFollower", StringComparison.Ordinal)
                ? root.GetComponentInChildren<OfficialBattleShadowBoneFollower>(true).gameObject : root.Find(path).gameObject;

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
