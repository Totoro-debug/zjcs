using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public sealed class StageTouchUI : MonoBehaviour, IStageUI
    {
        public Button myTouchBtn;
        public WorldMultiLanguageText myBtnText, myFuncDescText;
        private WorldStageUiContext stage;
        private void Awake() { if (myTouchBtn != null) myTouchBtn.onClick.AddListener(HandleTouchBtnClicked); }
        public void Init(WorldStageUiContext context)
        {
            stage = context;
            if (myFuncDescText != null) myFuncDescText.SetKey(stage.TouchDescriptionKey);
            if (myBtnText != null && !string.IsNullOrEmpty(stage.TouchButtonKey)) myBtnText.SetKey(stage.TouchButtonKey);
        }
        private void HandleTouchBtnClicked()
        {
            if (stage.IsActive()) stage.StartStage(0);
            stage.Close();
        }
    }
}
