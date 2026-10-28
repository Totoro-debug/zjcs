using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // StageDialog.Init, with the original ECS queries supplied by its map context.
    public sealed class StageDialog : MonoBehaviour
    {
        public WorldMultiLanguageText myStageName, myStageDesc;
        public Image myStageIcon, myBgIcon;
        public WorldDialogEntityContent mWorldDialogEntityContent;

        public void Init(WorldStageUiContext stage)
        {
            if (myStageName != null) myStageName.SetKey(stage.NameKey);
            if (myStageIcon != null)
            {
                myStageIcon.gameObject.SetActive(stage.TitleIcon != null);
                if (stage.TitleIcon != null) myStageIcon.sprite = stage.TitleIcon;
            }
            if (myBgIcon != null && stage.Background != null) myBgIcon.sprite = stage.Background;
            if (mWorldDialogEntityContent != null) mWorldDialogEntityContent.Init(stage);
            if (myStageDesc != null) myStageDesc.SetKey(stage.DescriptionKey, stage.DescriptionArgs);
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                if (component is IStageUI ui) ui.Init(stage);
        }
    }

    public interface IStageUI { void Init(WorldStageUiContext stage); }

    public sealed class WorldStageUiContext
    {
        public string NameKey, DescriptionKey, TouchDescriptionKey, TouchButtonKey;
        public object[] DescriptionArgs;
        public Sprite TitleIcon, Background;
        public GameObject DialogIconPrefab;
        public int Level;
        public WorldBattleSteleContext BattleStele;
        public Func<bool> IsActive;
        public Action<int> StartStage;
        public Action<Transform> ShowRewardPreview;
        public Action Close;
    }
}
