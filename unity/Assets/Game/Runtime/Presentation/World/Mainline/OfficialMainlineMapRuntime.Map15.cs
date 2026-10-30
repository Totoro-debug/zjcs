using System;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private GameObject map15PreparationDialogRoot;
        private WorldCostItemConfirmDialog map15PreparationDialog;

        public int RequestedMapClassId { get; private set; }

        private void ShowMap15PreparationDialog()
        {
            if (map15PreparationDialog != null) return;
            OfflineMap15PreparationState state = progressionSave.GetMap15PreparationState();
            if (state.Unlocked)
            {
                GameObject unlockedPrefab = Resources.Load<GameObject>("Map14/UI/CostItemConfirmDialog");
                if (unlockedPrefab == null) throw new InvalidOperationException("Official Map15 preparation dialog prefab is missing.");
                map15PreparationDialogRoot = Instantiate(unlockedPrefab, uiParent, false);
                map15PreparationDialog = map15PreparationDialogRoot.GetComponent<WorldCostItemConfirmDialog>();
                if (map15PreparationDialog == null)
                    throw new InvalidOperationException("Official Map15 preparation dialog component is missing.");
                map15PreparationDialog.Init("通天塔入口", "通天塔入口已开放，是否现在进入？", "进入通天塔", "暂不", "",
                    null, true, EnterMap15, null, CloseMap15PreparationDialog);
                return;
            }
            GameObject prefab = Resources.Load<GameObject>("Map14/UI/CostItemConfirmDialog");
            if (prefab == null) throw new InvalidOperationException("Official Map15 preparation dialog prefab is missing.");
            map15PreparationDialogRoot = Instantiate(prefab, uiParent, false);
            map15PreparationDialog = map15PreparationDialogRoot.GetComponent<WorldCostItemConfirmDialog>();
            if (map15PreparationDialog == null)
                throw new InvalidOperationException("Official Map15 preparation dialog component is missing.");
            string content;
            bool canStart = state.RankReached && !state.CountdownStarted;
            if (!state.RankReached)
                content = "达到位阶 " + state.RequiredRank + " 后可开启通天塔入口准备。";
            else if (state.CountdownStarted)
                content = "通天塔入口准备中，剩余 " + FormatPreparationDays(state.RemainingSeconds) + "。";
            else
                content = "通天塔入口尚未开放，开启后需要准备 14 天。是否现在开始？";
            map15PreparationDialog.Init("通天塔入口", content, "开始准备", "暂不", "",
                null, canStart, canStart ? StartMap15Preparation : null, null, CloseMap15PreparationDialog);
            if (!canStart) map15PreparationDialog.ConfirmBtn.interactable = false;
        }

        private void StartMap15Preparation()
        {
            ProgressionResult result = progressionSave.StartMap15Preparation();
            if (result.Status == ProgressionResultStatus.InvalidState)
                throw new InvalidOperationException(result.Message);
            if (result.Status == ProgressionResultStatus.NotReady)
            {
                hud.ShowOfficialNotice("位阶未达到要求");
                return;
            }
            hud.ShowOfficialNotice("已开始通天塔入口准备，14 天后开放");
        }

        private void EnterMap15()
        {
            RequestedMapClassId = 15;
            CloseMap();
        }

        private void CloseMap15PreparationDialog()
        {
            map15PreparationDialog = null;
            if (map15PreparationDialogRoot != null) Destroy(map15PreparationDialogRoot);
            map15PreparationDialogRoot = null;
        }

        private static string FormatPreparationDays(long seconds)
        {
            long days = (seconds + 86400L - 1L) / 86400L;
            return Math.Max(1L, days) + " 天";
        }
    }
}
