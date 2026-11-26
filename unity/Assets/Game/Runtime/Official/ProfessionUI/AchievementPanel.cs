using System;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    public sealed class AchievementPanel : MonoBehaviour
    {
        public AchievementPanelBase AchievementPanelBase;
        public int AchievementType;
        public string JsonParam;
        private IAchievementPanelSource data;
        private AchievementPanelKey mDataKey;
        private void Awake()
        {
            if (AchievementType != 0) mDataKey = new AchievementPanelKey(AchievementType, json: JsonParam);
        }
        public void Init(IProfessionAchievementData context)
        {
            if (AchievementType != 56 || !string.IsNullOrEmpty(JsonParam))
                throw new InvalidOperationException("This official panel adapter is scoped to ProfessionTotalLevel with no parameters.");
            data = new ProfessionAchievementPanelSource(context);
            mDataKey = new AchievementPanelKey(56, 10);
            Refresh();
        }
        public void Configure(IAchievementPanelSource source) { data = source ?? throw new ArgumentNullException(nameof(source)); Refresh(); }
        public void SetDataKey(AchievementPanelKey key) { mDataKey = key; Refresh(); }
        private void OnEnable() => Refresh();
        private void Refresh()
        {
            if (mDataKey != null && data != null && isActiveAndEnabled) AchievementPanelBase.InitDataKey(mDataKey, data);
        }
    }
}
