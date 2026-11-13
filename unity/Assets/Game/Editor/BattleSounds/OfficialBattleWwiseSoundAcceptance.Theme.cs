using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.BattleBgm;
using Zjcs.Offline.BattleNativePlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleWwiseSoundAcceptance
    {
        [Serializable] private sealed class ThemeTestCondition
        {
            public int ClassId;
            public DomainTypeCondition Condition;
        }
        [Serializable] private sealed class ThemeTestBindings
        {
            public FightThemeInfo[] theme_selection;
            public ThemeTestCondition[] theme_conditions;
        }

        private static IEnumerator ThemeSteps()
        {
            var data = JsonUtility.FromJson<ThemeTestBindings>(Resources.Load<TextAsset>("Combat/Wwise/Bgm/catalog").text);
            var theme = data.theme_selection.Single();
            var condition = data.theme_conditions.Single();
            Require(theme.FightThemeId == 1 && theme.ConditionId == 30110 && theme.TriggerPercent == 4000
                && theme.StageTypes.SequenceEqual(new[] { 11, 2 }) && condition.ClassId == 30110
                && theme.UIPrefabPath == "UI/Fight/FightMainPanel/ThemeNpc/FightThemeNpcLuoTianYi"
                && condition.Condition.DomainType == 2 && !condition.Condition.Not,
                "original theme and condition configuration");
            Require(!condition.Condition.IsMet(0) && !condition.Condition.IsMet(1) && condition.Condition.IsMet(2),
                "original Normal/Special/LuoTianYi condition");
            var inverted = new DomainTypeCondition { DomainType = 2, Not = true };
            Require(inverted.IsMet(0) && inverted.IsMet(1) && !inverted.IsMet(2), "original Not condition");

            int domain = 0, roll = 3999, evaluated = 0, randomCalls = 0;
            bool missing = false;
            var rows = new Dictionary<int, FightThemeInfo> { { theme.FightThemeId, theme } };
            var parser = new FightThemeInfoParser(rows, id => {
                evaluated++;
                Require(id == condition.ClassId, "selector keeps the condition identity");
                return missing ? (bool?)null : condition.Condition.IsMet(domain);
            }, maximum => {
                randomCalls++;
                Require(maximum == 10000, "original probability domain");
                return roll;
            });
            foreach (int stage in new[] { 0, 1, 43 })
                Require(parser.GetThemeIdByStageInfo(stage) == null, "unlisted stage does not select a theme");
            Require(evaluated == 0 && randomCalls == 0, "stage filter precedes conditions and random");
            foreach (int stage in new[] { 11, 2 })
                Require(parser.GetThemeIdByStageInfo(stage) == null, "approved ordinary offline domain keeps normal music");
            Require(evaluated == 2 && randomCalls == 0, "false domain condition does not consume random");
            domain = 2; missing = true;
            Require(parser.GetThemeIdByStageInfo(11) == null && randomCalls == 0,
                "missing condition does not consume random");
            missing = false;
            Require(ReferenceEquals(parser.GetThemeIdByStageInfo(11), theme) && randomCalls == 1,
                "3999 is inside the original forty-percent threshold");
            roll = 4000;
            Require(parser.GetThemeIdByStageInfo(2) == null && randomCalls == 2,
                "4000 is outside the strict original threshold");
            var later = new FightThemeInfo { FightThemeId = 2, ConditionId = 30110,
                StageTypes = new List<int> { 11, 2 }, TriggerPercent = 10000 };
            rows.Add(later.FightThemeId, later);
            Require(ReferenceEquals(parser.GetThemeIdByStageInfo(11), later) && randomCalls == 4,
                "a failed earlier roll permits the next original configuration");
            roll = 0;
            Require(ReferenceEquals(parser.GetThemeIdByStageInfo(11), theme) && randomCalls == 5,
                "the first matching configuration stops later random rolls");
            Debug.Log("ORIGINAL_FIGHT_THEME_RULES_PASSED original table, domain/Not, stage filter, missing condition, random short-circuit, strict boundary and ordered selection");

            var camera = new GameObject("Ordinary domain original theme verification");
            try
            {
                foreach (int stage in new[] { 11, 2 })
                {
                    using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                    {
                        var selected = OfflineBattleThemeSelection.Resolve(stage);
                        Require(selected == null, "approved Normal domain keeps the shared music/UI theme snapshot empty");
                        lease.PlayMusic(themeId: selected?.FightThemeId ?? 0);
                        yield return new WaitForSecondsRealtime(.2f);
                        var provider = UnityEngine.Object.FindObjectOfType<BgmGameObject>().gameObject;
                        NativeMusicSwitch(provider, "BattleMusic", "Map_11_1_Map");
                        NativeMusicPlaying(provider, 3912018356u);
                    }
                    yield return null;
                }
                Debug.Log("NATIVE_WWISE_ORDINARY_THEME_PASSED original daily dungeon and secret area conditions retain scene music in the approved Normal domain");
            }
            finally { UnityEngine.Object.Destroy(camera); }
        }
    }
}
