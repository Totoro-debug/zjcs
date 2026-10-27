using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void BindStagePrefab(string key, OfficialMap11UiData data, Func<string, Transform> require)
        {
            T Component<T>(string path) where T : Component => string.IsNullOrEmpty(path) ? null : require(path).GetComponent<T>();
            WorldMultiLanguageText Text(string path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                var node = require(path).gameObject;
                var text = node.GetComponent<WorldMultiLanguageText>() ?? node.AddComponent<WorldMultiLanguageText>();
                text.myKey = data.components.Single(v => v.panel == key && v.path == path && v.type == "MultiLanguageText").fields.myKey;
                return text;
            }
            StageDialog dialog = null;
            foreach (var source in data.components.Where(v => v.panel == key))
            {
                GameObject node = require(source.path).gameObject;
                var fields = source.fields;
                switch (source.type)
                {
                    case "StageDialog":
                        dialog = node.AddComponent<StageDialog>();
                        dialog.myStageName = Text(fields.myStageName);
                        dialog.myStageDesc = Text(fields.myStageDesc);
                        dialog.myStageIcon = Component<Image>(fields.myStageIcon);
                        dialog.myBgIcon = Component<Image>(fields.myBgIcon);
                        break;
                    case "StageTouchUI":
                        var touch = node.AddComponent<StageTouchUI>();
                        touch.myTouchBtn = Component<Button>(fields.myTouchBtn);
                        touch.myBtnText = Text(fields.myBtnText);
                        touch.myFuncDescText = Text(fields.myFuncDescText);
                        break;
                    case "WorldDialogEntityContent":
                        node.AddComponent<WorldDialogEntityContent>().mPreviewSlot = require(fields.mIconSlot.mPreviewSlot);
                        break;
                    case "WorldCommonStageUI":
                        var battle = node.AddComponent<WorldCommonStageUI>();
                        battle.myBattleBtn = Component<Button>(fields.myBattleBtn);
                        battle.myAwardSlot = require(fields.myAwardSlot);
                        battle.myLevelText = Text(fields.myLevelText);
                        battle.ReWardDescText = Text(fields.ReWardDescText);
                        battle.mLayerText = Text(fields.mLayerText);
                        break;
                    case "WorldStageOperationWindowClickUi":
                        node.AddComponent<WorldStageOperationWindowClickUi>().mDatas = fields.mDatas.Select(v =>
                            new WorldStageOperationWindowClickUi.ClickData { ClickType = v.ClickType, Button = Component<Button>(v.Button) }).ToArray();
                        break;
                }
            }
            var content = data.components.Single(v => v.panel == key && v.type == "StageDialog").fields.mWorldDialogEntityContent;
            dialog.mWorldDialogEntityContent = Component<WorldDialogEntityContent>(content);
        }
    }
}
