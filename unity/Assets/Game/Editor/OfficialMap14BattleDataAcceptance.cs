using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap14BattleDataAcceptance
    {
        private const string Key = "Zjcs.Map14.BattleDataAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            ProjectTools.Validate();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if (SessionState.GetBool(Key, false)) Hook(); }
        private static void Hook()
        {
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 battle data acceptance timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Map14BattleDataAcceptance").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            IEnumerator steps = Steps();
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) break; next = steps.Current; }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 battle data Play Mode passed: 217 official stages, 20 grids, 909 entity stage bindings; current monster properties, passive properties, unit construction and active/passive skill assembly. FightOther 4651/4652 model binding, battle presentation/input/rewards, mainline entry and Player release are not covered by this fixture.");
            }
            EditorApplication.isPlaying = false;
        }
        private static IEnumerator Steps()
        {
            var asset = Resources.Load<TextAsset>("Map14/battle-runtime");
            Require(asset, "Map14 battle Resources payload missing");
            var data = JsonUtility.FromJson<Map10RuntimeBattleData>(asset.text);
            Require(data.manifest == "871_156598" && data.mapClassId == 14, "Map14 battle version mismatch");
            Require(data.stages.Length == 217 && data.monsterRosters.Length == 217 && data.battleScenes.Length == 20,
                "Map14 battle closure counts differ");
            Require(data.entityStageBindings.Length == 909, "Map14 component stage bindings differ");
            var stageIds = new HashSet<int>(data.stages.Select(s => s.Id));
            Require(stageIds.Count == 217 && stageIds.SetEquals(data.monsterRosters.Select(r => r.StageId)), "Stage/roster identity mismatch");
            Require(data.entityStageBindings.All(b => b.StageComponents.All(c => c.StageIds.All(stageIds.Contains))), "Unbound entity stage");
            var source = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = source.Load();
            var formula = source.LoadDamageFormula();
            var skills = source.LoadSkillItems();
            var methods = typeof(OfficialMainlineMapRuntime);
            MethodInfo passive = methods.GetMethod("ApplyMonsterPassiveSkills", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo create = methods.GetMethod("CreateMonsterBattleUnit", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo assemble = methods.GetMethod("AssembleMonsterSkills", BindingFlags.NonPublic | BindingFlags.Static);
            Require(passive != null && create != null && assemble != null, "Actual mainline monster construction methods missing");
            int count = 0, dynamic = 0;
            foreach (var stage in data.stages)
            {
                var scene = data.battleScenes.Single(s => s.GameId == stage.GameId);
                Require(scene.FightGridGroundComponent?.PosInfos?.Length > 0 && scene.FightComponent?.FactionInfoList?.Length > 0,
                    "Original battle grid missing: " + stage.GameId);
                var roster = data.monsterRosters.Single(r => r.StageId == stage.Id);
                foreach (var wave in roster.Waves) foreach (var monster in wave.Units)
                {
                    int level = stage.AwardUsePlayerLevelAndSubRank ? 90 + Mathf.RoundToInt(monster.LevelOffset) : monster.Level;
                    int subRank = stage.AwardUsePlayerLevelAndSubRank ? 3 : monster.SubRank;
                    var properties = new Dictionary<int, long>(catalog.CalculateMonsterProps(monster.MonsterId, monster.MonsterGroupNo, level, subRank));
                    if (!stage.AwardUsePlayerLevelAndSubRank)
                    {
                        var expected = monster.Properties.ToDictionary(p => p.Id, p => p.Value);
                        Require(expected.Count == properties.Count && expected.All(p => properties.TryGetValue(p.Key, out long actual) && actual == p.Value),
                            "Official C# monster formula differs from source roster: stage " + stage.Id + " monster " + monster.MonsterId);
                    }
                    passive.Invoke(null, new object[] { catalog, skills, properties, monster, level, subRank });
                    var unit = (FightBattleUnitState)create.Invoke(null, new object[] { catalog, formula, monster, properties, level, subRank, count + 2, 7 });
                    Require(unit.Level == level && unit.SubRank == subRank && unit.Hp > 0 && unit.Wave == wave.Index,
                        "Computed monster level or properties were lost during actual unit construction");
                    assemble.Invoke(null, new object[] { catalog, skills, unit, monster });
                    Require(unit.Skills != null && (monster.ActiveSkills ?? Array.Empty<Map10RuntimeSkill>()).All(active =>
                        unit.Skills.Any(skill => skill.EntityId == active.EntityId && skill.ItemId == active.ItemId
                            && skill.Level == Math.Max(1, level) && skill.SubRank == subRank && skill.Rank == Math.Max(1, active.Rank))),
                        "Computed monster level was lost during actual skill assembly: " + stage.Id + "/" + monster.MonsterId);
                    count++; if (stage.AwardUsePlayerLevelAndSubRank) dynamic++;
                }
                yield return null;
            }
            Debug.Log("Map14 battle data consumed monster placements=" + count + "; player-level placements=" + dynamic + ".");
        }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidDataException(message); }
        private sealed class Runner : MonoBehaviour { }
    }
}
