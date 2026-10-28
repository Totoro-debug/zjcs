using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void BindSteleConfirmPrefab(GameObject root, OfficialMap11UiData data,
            Func<string, Transform> require)
        {
            const string key = "ConfirmDialog";
            var components = data.components.Where(c => c.panel == key).ToArray();
            WorldMultiLanguageText Text(string path)
            {
                var node = require(path).gameObject;
                var result = node.GetComponent<WorldMultiLanguageText>() ?? node.AddComponent<WorldMultiLanguageText>();
                result.myKey = components.Single(c => c.path == path && c.type == "MultiLanguageText").fields.myKey;
                return result;
            }
            BindSteleAuxiliary(key, data, require);
            var alignSource = components.Single(c => c.type == "AutoAlignForSingleMultiLineText");
            var align = require(alignSource.path).gameObject.AddComponent<WorldAutoAlignForSingleMultiLineText>();
            align.mTxt = Text(alignSource.fields.mTxt);
            align.mTmp = require(alignSource.fields.mTmp).GetComponent<TextMeshProUGUI>();
            align.mSingleLineAlignment = (TextAlignmentOptions)alignSource.fields.mSingleLineAlignment;
            align.mMultiLineAlignment = (TextAlignmentOptions)alignSource.fields.mMultiLineAlignment;
            var timerSource = components.Single(c => c.type == "ShowRemainTimeInfo");
            var timer = require(timerSource.path).gameObject.AddComponent<WorldShowRemainTimeInfo>();
            timer.mRemainTimeText = Text(timerSource.fields.mRemainTimeText);
            var lockSource = components.Single(c => c.type == "LockMaskWithTime");
            var lockFields = lockSource.fields;
            var locked = require(lockSource.path).gameObject.AddComponent<WorldLockMaskWithTime>();
            locked.mLockTimeDefault = lockFields.mLockTimeDefault;
            locked.mShowRemainTimeInfo = require(lockFields.mShowRemainTimeInfo).GetComponent<WorldShowRemainTimeInfo>();
            var stateRefs = lockFields.mLockedStateSwitcher.mStateConfigs;
            if (stateRefs.Length != 1) throw new InvalidDataException("Original confirmation lock state count changed.");
            var state = lockFields.references.RefIds.Single(r => r.rid == stateRefs[0].rid);
            if (state.type.@class != "StateGameObjectConfig")
                throw new InvalidDataException("Original confirmation lock state type changed.");
            locked.mLockedStateSwitcher = WorldStateSwitcher<bool>.FromBindings(
                require(lockFields.mLockedStateSwitcher.mOwner).gameObject,
                new IWorldStateConfig[] { WorldStateGameObjectConfig.FromBindings(new WorldStateGameObjectData
                {
                    GoConfig = state.data.mStateGameObjectData.GoConfig.Select(row =>
                        new WorldStateGameObjectData.StateGameObjectKV
                        {
                            State = row.State.mValue,
                            GameObjects = row.GameObjects.Select(path => require(path).gameObject).ToList()
                        }).ToList()
                }) });
            var f = components.Single(c => c.type == key).fields;
            var dialog = root.AddComponent<WorldConfirmDialog>();
            dialog.TitleText = Text(f.TitleText);
            dialog.ContentText = require(f.ContentText).GetComponent<WorldAutoAlignForSingleMultiLineText>();
            dialog.HintText = Text(f.HintText);
            dialog.HintGo = require(f.HintGo).gameObject;
            dialog.ConfirmBtn = require(f.ConfirmBtn).GetComponent<Button>();
            dialog.CancelBtn = require(f.CancelBtn).GetComponent<Button>();
            dialog.ConfirmText = Text(f.ConfirmText);
            dialog.CancelText = Text(f.CancelText);
            dialog.PopupWindow = root.GetComponent<WorldPopupWindow>();
            dialog.LockMaskWithTime = require(f.LockMaskWithTime).GetComponent<WorldLockMaskWithTime>();
            dialog.LockTime = f.LockTime;
            dialog.CancelBtnCustomWidth = f.CancelBtnCustomWidth;
            dialog.CancelBtnLayoutElement = require(f.CancelBtnLayoutElement).GetComponent<LayoutElement>();
        }
    }
}
