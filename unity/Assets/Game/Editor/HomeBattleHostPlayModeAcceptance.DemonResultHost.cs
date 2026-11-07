using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.ItemDetailUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseDemonResultPanel(HomeFeatureRuntime home,
            FightBattleCheckpoint checkpoint, FightBattleReport report, string output,
            OfflineWeeklyChallengeSession.ChallengeAward award)
        {
            var view = UnityEngine.Object.FindObjectOfType<DemonResultWindow>();
            Require(view, "The actual weekly completion did not open the native demon result.");
            float deadline = Time.realtimeSinceStartup + 25;
            while (!view.IsReady)
            {
                Require(Time.realtimeSinceStartup < deadline, "The actual native result did not become ready.");
                yield return null;
            }
            yield return new WaitForSeconds(2);
            var content = view.Panel;
            var runner = UnityEngine.Object.FindObjectOfType<WeeklyChallengeBattleRunner>();
            Require(runner && runner.Controller && runner.Controller.HasBattleEnvironment,
                "The native result released its battle before native leave.");
            string settledState = FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint);
            var panel = content.GetComponent<GameEndPanelDemonKing>();
            var actions = content.GetComponent<GameEndPanelBase>();
            var assets = OfficialDemonResultAssets.Load();
            Require(actions.ResultMsg.Report == report
                && (actions.ResultMsg.DamageValue > 0) == report.Units.Any(value => value.Side == 1 && value.DamageDealt > 0),
                "The native damage result does not correspond to the actual completed battle.");
            var row = content.GetComponentsInChildren<GameEndPlayerSettleItem>().Single();
            Require(row.PlayerId == home.Profile.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId && !row.IsNpc,
                "The actual native result lost the persisted local actor identity.");
            foreach (var info in row.GetComponentsInChildren<PlayerInfoUI>())
                if (info.ProfessionIcon && info.ProfessionIcon.gameObject.activeInHierarchy)
                    Require(info.ProfessionIcon.sprite, "The actual settlement profession sprite is unbound.");
            foreach (var text in content.GetComponentsInChildren<TMP_Text>())
                Require(text.font && text.font.HasCharacters(text.text, out _, false, true),
                    "The actual result is missing displayed glyphs: " + text.name);
            var rt = content.GetComponentInChildren<Zjcs.Official.ActivityBattleUI.TempRTUI>();
            Require(rt && rt.RawImage.texture && home.Scene.Camera.CompareTag("MainCamera"),
                "The actual home camera did not render the original result model.");
            yield return Capture(output, award == null ? "demon-result-host-repeat" : "demon-result-host-first");
            long inventory = home.ItemCount(41021);
            var common = content.GetComponent<OfficialBattleResultReport>();
            Require(common && common.FinalReport != null, "The real result did not capture its final report.");
            string snapshot = JsonUtility.ToJson(common.FinalReport);
            Require(snapshot == JsonUtility.ToJson(report), "The result replaced the runner's final statistics.");
            for (int open = 0; open < 2; open++)
            {
                yield return Combat.BattleGameViewInput.Click(actions.ReportButton);
                deadline = Time.realtimeSinceStartup + 20;
                Transform popup;
                while (!(popup = Root("FightEndReportPanel") ?? Root("FightEndAllPlayerReportPanel"))
                    || popup.GetComponent<OfficialBattlePopupMask>()?.IsReady != true)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The real demon report popup did not become ready.");
                    yield return null;
                }
                Require(common.IsOpen && content.activeInHierarchy
                    && popup.GetComponentsInChildren<WorldBattleReportPlayerRow>().Length == 1,
                    "The actual demon report lost its single player or original result.");
                foreach (var text in popup.GetComponentsInChildren<TMP_Text>())
                {
                    Require(text.font && text.font.HasCharacters(text.text, out _, false, true),
                        "The actual report is missing displayed glyphs: " + text.name);
                }
                yield return Capture(output, "demon-result-host-report-" + open + (award == null ? "-repeat" : "-first"));
                // The default skill tab shows this player's skills. Damage
                // caused only by the opposing boss does not create a row here.
                if (open == 0 && report.Skills.Any(value => value.UnitId == 1 && value.Damage > 0))
                {
                    var tabs = popup.GetComponentsInChildren<WorldTabGroup>(true)
                        .Single(value => value.GetAllTabs().Length == 2);
                    yield return Combat.BattleGameViewInput.Click(tabs.GetAllTabs()[1]);
                    yield return new WaitForSeconds(1);
                    var skill = popup.GetComponentsInChildren<WorldBattleReportSkillRow>().First();
                    yield return Combat.BattleGameViewInput.Click(skill.GetComponentsInChildren<UnityEngine.UI.Button>()
                        .First(value => value.interactable));
                    yield return new WaitForSeconds(1);
                    Require(Root("skill_plain_detail"), "The actual demon report did not open its original read-only skill detail.");
                    yield return Capture(output, "demon-result-host-skill-detail");
                    var close = Active<UnityEngine.UI.Button>().Where(value => value.name == "BlurMask")
                        .OrderByDescending(value => value.GetComponentInParent<Canvas>().sortingOrder).First();
                    yield return Combat.BattleGameViewInput.Click(close);
                    yield return new WaitForSeconds(1);
                }
                yield return Combat.BattleGameViewInput.Click(popup.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Single(value => value.name == "CloseBtn"));
                yield return new WaitForSeconds(1);
                Require(!common.IsOpen && common.FinalReport != null && JsonUtility.ToJson(common.FinalReport) == snapshot
                    && !Root("BattleResultReportLifetime") && home.ItemCount(41021) == inventory,
                    "Closing/reopening the report changed final statistics, leaked its lifetime or granted inventory.");
            }
            if (award != null)
            {
                var icon = content.GetComponentInChildren<ItemAwardIcon>().ItemIcon;
                Require(icon.IconSprite == assets.RewardIcon && icon.IconGameObject.activeInHierarchy
                    && icon.myText.Text.Contains("300"), "The real result reward icon is unbound.");
                yield return Combat.BattleGameViewInput.Click(icon.Button);
                deadline = Time.realtimeSinceStartup + 20;
                while (!view.ItemDetail || !view.ItemDetail.activeInHierarchy
                    || view.ItemDetail.GetComponent<WorldPopupWindow>().State != WorldPopupWindow.EState.Show)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The actual result item detail did not become ready.");
                    yield return null;
                }
                var detail = view.ItemDetail.GetComponent<ItemDetailDialog>();
                Require(detail.ItemInfo.ClassId == 41021 && detail.ItemImage.sprite == assets.RewardIcon
                    && detail.QualityBg.sprite == assets.DetailQualityBackground
                    && detail.ItemCountText.Text.Contains(inventory.ToString()) && !detail.ItemSourceObj.activeSelf,
                    "The actual original item detail differs from the committed inventory/configuration.");
                yield return Capture(output, "demon-result-host-item-detail");
                // PopupWindow rejects mask clicks for the first original .5 seconds.
                yield return new WaitForSeconds(.6f);
                yield return Combat.BattleGameViewInput.Click(view.ItemMask.Button);
                deadline = Time.realtimeSinceStartup + 20;
                while (view.ItemDetail || view.ItemMask)
                {
                    Require(Time.realtimeSinceStartup < deadline, "The actual item detail did not finish closing.");
                    yield return null;
                }
                Require(!view.ItemDetail && !view.ItemMask && content.activeInHierarchy,
                    "Closing the actual item detail did not restore the same result.");
            }
            Require(runner && runner.Controller.HasBattleEnvironment
                && FightBattleCheckpointSerializer.Save(runner.Controller.Checkpoint) == settledState,
                "Viewing the native result advanced or released the settled battle.");
            yield return Combat.BattleGameViewInput.Click(panel.LeaveGameBtn);
            yield return new WaitForSeconds(1);
            Require(!view && !content && !runner && !home.Scene.Camera.CompareTag("MainCamera")
                && home.ItemCount(41021) == inventory && !Root("BattleResultReportLifetime"),
                "Closing the actual result leaked its window/camera ownership or changed rewards.");
            yield return Capture(output, "demon-result-host-return" + (award == null ? "-repeat" : "-first"));
            Debug.Log("[Demon result host] Actual weekly completion, retained battle mask/model/statistics, twice-opened report, immutable final report, item detail, simulated leave and unchanged inventory passed.");
        }
    }
}
