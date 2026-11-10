using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.BattleStageStatusUI
{
    // Official FightStageStatusDialog: ClearCells, data source, count, RefillCells
    // keep their original order. UIBase's InitWrapper is scoped to this window.
    public sealed class FightStageStatusDialog : MonoBehaviour, LoopScrollDataSource
    {
        [SerializeField] private LoopVerticalScrollRect mScrollStatus;
        private List<int> mStatusEntityIds;
        private FightStageStatusPresentation presentation;
        private bool initialized;

        public void Init(List<int> statusEntityIds, FightStageStatusPresentation source)
        {
            mStatusEntityIds = statusEntityIds;
            presentation = source;
            initialized = true;
            if (isActiveAndEnabled) Refresh();
        }

        private void OnEnable() { if (initialized) Refresh(); }

        private void Refresh()
        {
            mScrollStatus.ClearCells();
            mScrollStatus.dataSource = this;
            mScrollStatus.totalCount = mStatusEntityIds.Count;
            mScrollStatus.RefillCells();
        }

        public void ProvideData(Transform transform, int index)
        {
            if (index >= 0 && index < mStatusEntityIds.Count)
            {
                var item = transform.GetComponent<FightStageStatusDialogItemUI>();
                if (item != null)
                {
                    presentation.Bind(transform.gameObject);
                    item.Init(mStatusEntityIds[index], presentation);
                }
            }
        }
    }
}
