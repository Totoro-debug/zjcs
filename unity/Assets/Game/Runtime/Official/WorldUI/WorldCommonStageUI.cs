using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // StageStageUI and WorldCommonStageUI use the same original battle button.
    public sealed class WorldCommonStageUI : MonoBehaviour, IStageUI
    {
        public Button myBattleBtn;
        public Transform myAwardSlot;
        public WorldMultiLanguageText myLevelText, ReWardDescText, mLayerText;
        private WorldStageUiContext stage;
        private void Awake() => myBattleBtn.onClick.AddListener(HandleBattleBtnClicked);
        public void Init(WorldStageUiContext context)
        {
            stage = context;
            if (myLevelText != null) myLevelText.Format(stage.Level);
            stage.ShowRewardPreview(myAwardSlot);
            if (ReWardDescText != null) ReWardDescText.SetKey("ui_drop_preview_text");
        }
        private void HandleBattleBtnClicked()
        {
            stage.StartStage(0);
            stage.Close();
        }
    }
}
