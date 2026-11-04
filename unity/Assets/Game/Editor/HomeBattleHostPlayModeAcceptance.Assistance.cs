using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        public static void RunConfiguredPartnerAssistAcceptance()
        {
            string directory = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (!Application.isBatchMode || string.IsNullOrEmpty(directory)
                || !File.Exists(Path.Combine(directory, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use a dedicated graphical batch Editor and an owned validation directory.");
            ProjectTools.Validate();
            // Keep this process's compiled snapshot stable while other sessions
            // write the shared project. The dedicated Editor exits after the run.
            UnityEditor.AssetDatabase.DisallowAutoRefresh();
            Skills.SkillPlayModeAcceptance.Run();
        }

        // An isolated seam fixture, not the festival activity's entry or reward adapter.
        private static IEnumerator RunConfiguredAssistHost(HomeFeatureRuntime home, int roomId,
            string output, Action<Selectable> click)
        {
            const int playType = (int)NpcAssistPlayType.FestivalBoss;
            const string entryId = "battle-host-assistance-fixture";
            int stageId = home.DailyDungeons.FindRoom(roomId).stageId;
            var invite = home.NpcAssistanceCatalog.InviteRule(playType);
            string selectionText = Environment.GetEnvironmentVariable("ZJCS_BATTLE_HOSTS_ASSIST_PARTNERS");
            if (string.IsNullOrWhiteSpace(selectionText)
                && Skills.SkillPlayModeAcceptance.PartnerBattleModelAcceptanceRequested)
                selectionText = "8402";
            int[] selectedNpcIds = string.IsNullOrWhiteSpace(selectionText) ? Array.Empty<int>()
                : selectionText.Split(',').Select(value => int.Parse(value.Trim())).ToArray();
            Require(home.NpcAssistanceProgress.SetSelection(playType, entryId, selectedNpcIds,
                invite.MaxPlayer), "Could not prepare the isolated assistance selection.");
            var snapshot = home.NpcAssistanceProgress.CommitBattle(playType, entryId, stageId,
                invite.MaxPlayer, invite.OneNpcMaxInviteCount, invite.MaxInviteCount,
                (NpcAssistRefreshCycle)invite.RefreshCycle, DateTimeOffset.Now.ToUnixTimeSeconds());
            string committed = JsonUtility.ToJson(snapshot);
            Action verifyHome = CaptureHomeRestoration(home);
            var options = new OfficialBattleAssistOptions { PlayType = playType, EntryId = entryId };
            var first = new GameObject("ConfiguredAssistBeforeReload");
            first.transform.SetParent(home.transform, false);
            first.AddComponent<DailyDungeonBattleRunner>().Begin(home, roomId, "legacy-default-entry",
                _ => throw new InvalidOperationException("Preparing a battle settled it."), options);
            first.GetComponent<OfficialBattleRuntimeController>().SetPaused(true);
            Require(!string.IsNullOrEmpty(home.DailyDungeons.ActiveRun.activeBattleCheckpoint),
                "The configured battle did not persist its checkpoint.");
            FightBattleCheckpoint savedBattle = FightBattleCheckpointSerializer.Load(
                home.DailyDungeons.ActiveRun.activeBattleCheckpoint);
            File.WriteAllText(Path.Combine(output, "battle-assistance-checkpoint.json"),
                home.DailyDungeons.ActiveRun.activeBattleCheckpoint);
            Require(selectedNpcIds.All(npcId => savedBattle.Units.Any(unit => unit.NpcClassId == npcId)),
                "The selected partner was not included in the persisted battle checkpoint.");
            // Later edits by the caller must not change the identity of the active battle.
            options.PlayType = (int)NpcAssistPlayType.All;
            options.EntryId = "changed-after-begin";
            UnityEngine.Object.Destroy(first);
            yield return null;
            yield return SettleUi();
            verifyHome();
            Require(JsonUtility.ToJson(home.NpcAssistanceProgress.ActiveBattle) == committed,
                "Pausing/disposal modified or consumed the committed assistance transaction.");

            var wrong = new GameObject("WrongAssistRestore");
            bool rejected = false;
            try
            {
                wrong.AddComponent<DailyDungeonBattleRunner>().Begin(home, roomId, entryId, _ => { });
            }
            catch (InvalidOperationException) { rejected = true; }
            finally { UnityEngine.Object.Destroy(wrong); }
            Require(rejected && JsonUtility.ToJson(home.NpcAssistanceProgress.ActiveBattle) == committed,
                "Restoring with another play type did not preserve and reject the active snapshot.");
            yield return null;

            bool callback = false;
            var resumed = new GameObject("DailyDungeonBattleRunner");
            resumed.transform.SetParent(home.transform, false);
            resumed.AddComponent<DailyDungeonBattleRunner>().Begin(home, roomId, "legacy-default-entry",
                _ => callback = true, new OfficialBattleAssistOptions { PlayType = playType, EntryId = entryId });
            Require(JsonUtility.ToJson(home.NpcAssistanceProgress.ActiveBattle) == committed,
                "Restoring the configured battle recommitted its assistance transaction.");
            yield return new WaitForEndOfFrame();
            foreach (var unit in savedBattle.Units.Where(value => selectedNpcIds.Contains(value.NpcClassId)))
                VerifyPartnerModel(unit.Id, unit.NpcClassId);
            foreach (var unit in savedBattle.Units.Where(value => value.MonsterId == 3206 || value.MonsterId == 3207))
            {
                var monster = GameObject.Find("BattleUnit_" + unit.Id)?.GetComponentInChildren<Zjcs.Offline.ProfessionUI.BattleNativeMonsterRenderer>();
                Require(monster != null && monster.Attachments?.Agent != null,
                    "The actual dungeon monster has no original native attachment registry.");
                Require(monster.Attachments.Agent.myBoneNameList.Count == (unit.MonsterId == 3206 ? 25 : 28),
                    "The actual monster lost original attachment entries.");
                if (unit.MonsterId == 3207)
                    Require(monster.Attachments.Agent.GetBone("rotate_01") != null,
                        "The actual 16010 caster has no original rotation attachment.");
            }
            yield return RunBattle<DailyDungeonBattleRunner>("daily-assist", output, click);
            verifyHome();
            Require(callback && home.NpcAssistanceProgress.ActiveBattle == null,
                "The shared host failed to finish the configured assistance identity.");
            File.WriteAllText(Path.Combine(output, "battle-assistance-result.txt"),
                "PlayType 5 custom entry, selected NPCs " + string.Join(",", selectedNpcIds)
                + ": checkpoint restore, default-type rejection, unchanged committed snapshot, "
                + "actual battle completion, assistance cleanup and host restoration passed.\n");
        }

        [Serializable] private sealed class PartnerFighterPayload { public PartnerFighter[] fighters; }
        [Serializable] private sealed class PartnerFighter
        {
            public int classId;
            public float scale;
            public OfficialBattleModelTransform[] modelTransforms;
        }

        [Serializable] private sealed class NativePartnerFighterPayload { public NativePartnerFighter[] fighters; }
        [Serializable] private sealed class NativePartnerFighter
        {
            public int npcId, entityId;
            public OfficialBattleModelTransform[] transforms;
        }

        private static void VerifyPartnerModel(int unitId, int npcId)
        {
            if (OfficialBattlePartnerNativeModelView.HasBinding(npcId))
            {
                var nativePayload = JsonUtility.FromJson<NativePartnerFighterPayload>(File.ReadAllText(
                    "Assets/Official/Combat/NativePartners/catalog.json"));
                NativePartnerFighter nativeSource = nativePayload.fighters.Single(value => value.npcId == npcId);
                Transform nativeEntity = GameObject.Find("BattleUnit_" + unitId)?.transform;
                Require(nativeEntity != null && Mathf.Approximately(Mathf.Abs(nativeEntity.localScale.x), 1f)
                    && Mathf.Approximately(nativeEntity.localScale.y, 1f), "Native partner entity scale differs.");
                Transform nativeModel = nativeEntity;
                foreach (OfficialBattleModelTransform expected in nativeSource.transforms)
                {
                    nativeModel = nativeModel.Find(expected.name);
                    bool entityRoot = nativeModel != null && nativeModel.parent == nativeEntity;
                    // The role entity's root is positioned/faced by the battle;
                    // descendants retain the original authored local transforms.
                    Require(nativeModel != null && (entityRoot || Vector3.Distance(nativeModel.localPosition, expected.position) < .00001f)
                        && Quaternion.Angle(nativeModel.localRotation, expected.rotation) < .001f
                        && (entityRoot
                            ? Vector3.Distance(new Vector3(Mathf.Abs(nativeModel.localScale.x), nativeModel.localScale.y,
                                nativeModel.localScale.z), expected.scale) < .00001f
                            : Vector3.Distance(nativeModel.localScale, expected.scale) < .00001f),
                        "Native partner differs from the original prefab transform: " + expected.name);
                }
                var nativeAnimator = nativeModel.GetComponent<Animator>();
                Require(nativeAnimator != null && nativeAnimator.runtimeAnimatorController != null,
                    "Native partner original Animator is unbound.");
                var nativeRenderer = nativeModel.GetComponent<MeshRenderer>();
                Require(nativeRenderer != null && nativeRenderer.enabled && nativeRenderer.bounds.size.sqrMagnitude > 0f,
                    "Native partner model did not produce a rendered mesh.");
                Require(nativeEntity.GetComponentsInChildren<SpriteRenderer>().Any(value => value.sprite != null),
                    "Native partner original shadow is unbound.");
                return;
            }
            var payload = JsonUtility.FromJson<PartnerFighterPayload>(File.ReadAllText(
                "Assets/Official/Combat/NpcFighters/fighters.json"));
            PartnerFighter source = payload.fighters.Single(value => value.classId == npcId);
            Transform entity = GameObject.Find("BattleUnit_" + unitId)?.transform;
            Require(entity != null && Mathf.Approximately(Mathf.Abs(entity.localScale.x), 1f)
                && Mathf.Approximately(entity.localScale.y, 1f),
                "Partner skeleton unit conversion was reapplied to its entity transform.");
            Transform model = entity;
            foreach (OfficialBattleModelTransform expected in source.modelTransforms)
            {
                model = model.Find(expected.name);
                Require(model != null && Vector3.Distance(model.localPosition, expected.position) < .00001f
                    && Quaternion.Angle(model.localRotation, expected.rotation) < .001f
                    && Vector3.Distance(model.localScale, expected.scale) < .00001f,
                    "Partner model differs from the original prefab transform: " + expected.name);
            }
            var skeleton = model.GetComponent<Spine.Unity.SkeletonAnimation>();
            Require(skeleton != null && Mathf.Approximately(skeleton.skeletonDataAsset.scale, source.scale),
                "Partner skeleton lost its original data scale.");
            var renderer = model.GetComponent<MeshRenderer>();
            Require(renderer != null && renderer.enabled && renderer.bounds.size.sqrMagnitude > 0f,
                "Partner model did not produce a rendered mesh.");
        }
    }
}
