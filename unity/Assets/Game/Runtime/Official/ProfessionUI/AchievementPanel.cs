using System;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    public sealed class AchievementPanel : MonoBehaviour
    {
        public AchievementPanelBase AchievementPanelBase;
        public int AchievementType;
        public string JsonParam;
        private IProfessionAchievementData data;
        public void Init(IProfessionAchievementData context)
        {
            if (AchievementType != 56 || !string.IsNullOrEmpty(JsonParam))
                throw new InvalidOperationException("This official panel adapter is scoped to ProfessionTotalLevel with no parameters.");
            data = context ?? throw new ArgumentNullException(nameof(context));
            Refresh();
        }
        private void OnEnable() => Refresh();
        private void Refresh()
        {
            if (data != null && isActiveAndEnabled) AchievementPanelBase.InitDataKey(data);
        }
    }
}
