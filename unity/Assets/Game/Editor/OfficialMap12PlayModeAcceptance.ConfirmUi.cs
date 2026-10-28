using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private static IEnumerator VerifySteleConfirmUi(GameObject canvas, Camera camera,
            OfficialMap11UiData data, string output, int width)
        {
            var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map12/UI/ConfirmDialog"), canvas.transform, false);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
            foreach (Canvas child in root.GetComponentsInChildren<Canvas>(true)) child.worldCamera = camera;
            foreach (WorldMultiLanguageText text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                text.Localizer = key => key == "{0}" ? key : Text(data, key);
            var view = root.GetComponent<WorldConfirmDialog>();
            SteleRequire(view != null && view.LockMaskWithTime != null, "Original confirmation component bindings are missing.");
            var events = new List<string>();
            void Init(string content)
            {
                view.Init("{0}", new object[] { content }, () => events.Add("confirm"), () => events.Add("cancel"));
                view.CloseWindow = () => { events.Add("close"); root.SetActive(false); };
                root.SetActive(true);
                view.PopupWindow.Show();
            }
            Init("确认");
            yield return new WaitForSecondsRealtime(.6f);
            yield return new WaitForEndOfFrame();
            SteleRequire(view.TitleText.myKey == "confirmation" && view.ConfirmText.myKey == "confirm"
                && view.CancelText.myKey == "cancel" && !view.HintGo.activeSelf,
                "Confirmation initialization changed its original keys or hint visibility.");
            SteleRequire(view.ContentText.IsSingleLine
                && view.ContentText.mTmp.alignment == view.ContentText.mSingleLineAlignment,
                "Single-line confirmation alignment changed.");
            view.SetCustomWidthBtn();
            SteleRequire(view.CancelBtnLayoutElement.preferredWidth == view.CancelBtnCustomWidth,
                "Original confirmation custom cancel width was lost.");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, width + "-stele-confirm-single.png"));
            SimulateUiClick(view.ConfirmBtn, camera);
            SteleRequire(events.SequenceEqual(new[] { "confirm", "close" }), "Confirm invoked cancel or changed callback/close order.");
            events.Clear();
            Init("确认\n第二行");
            view.SetHintTextKey("{0}", "提示");
            yield return new WaitForSecondsRealtime(.6f);
            yield return new WaitForEndOfFrame();
            SteleRequire(!view.ContentText.IsSingleLine
                && view.ContentText.mTmp.alignment == view.ContentText.mMultiLineAlignment
                && view.HintText.GetComponent<TextMeshProUGUI>().alignment == view.ContentText.mTmp.alignment,
                "Multi-line confirmation or visible hint alignment changed.");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, width + "-stele-confirm-multi.png"));
            SimulateUiClick(view.CancelBtn, camera);
            SteleRequire(events.SequenceEqual(new[] { "cancel", "close" }), "Cancel callback ran twice on disable.");
            events.Clear();
            Init("确认");
            view.SetCloseOnConfirm(false);
            yield return new WaitForSecondsRealtime(.6f);
            SimulateUiClick(view.ConfirmBtn, camera);
            SteleRequire(events.SequenceEqual(new[] { "confirm" }) && root.activeSelf,
                "Non-closing confirmation unexpectedly closed or consumed the callback state.");
            root.SetActive(false);
            SteleRequire(events.SequenceEqual(new[] { "confirm", "cancel" }), "Original non-closing confirmation disable behavior changed.");
            events.Clear();
            Init("确认");
            view.SetCancelCallbackAutoExecute(false);
            root.SetActive(false);
            SteleRequire(events.Count == 0, "Disabled cancellation still auto-executed.");
            view.SetCancelCallbackAutoExecute(true);
            Init("确认");
            yield return new WaitForSecondsRealtime(.6f);
            view.PopupWindow.Hide(null);
            yield return new WaitForSecondsRealtime(.3f);
            SteleRequire(events.SequenceEqual(new[] { "cancel" }) && !root.activeSelf,
                "Popup hide did not cancel on disable.");
            events.Clear();
            Init("确认");
            yield return new WaitForSecondsRealtime(.6f);
            var locked = view.LockMaskWithTime;
            var mask = root.transform.Find("BgDialog1/Content/DialogButtonGroup/ButtonsGo/LockMask/Mask").gameObject;
            var timerText = locked.mShowRemainTimeInfo.mRemainTimeText;
            SteleRequire(locked.mLockTimeDefault == 10, "The original prefab lock default changed.");
            view.SetConfirmBtnLock(.3f);
            SteleRequire(mask.activeSelf && timerText.gameObject.activeSelf, "Timed confirmation lock was not shown.");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)view.ConfirmBtn.targetGraphic.transform;
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            var hit = hits.FirstOrDefault().gameObject;
            SteleRequire(hit != null && (hit == mask || hit.transform.IsChildOf(mask.transform)),
                "Original lock mask did not intercept the simulated pointer.");
            pointer.pointerCurrentRaycast = hits[0];
            var pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
            SteleRequire(events.Count == 0 && root.activeSelf, "A pointer blocked by the lock mask confirmed the dialog.");
            yield return new WaitForSecondsRealtime(.5f);
            SteleRequire(!mask.activeSelf, "Timed confirmation lock did not unlock.");
            view.SetConfirmBtnLock(.3f);
            root.SetActive(false);
            SteleRequire(!mask.activeSelf && !timerText.gameObject.activeSelf && events.SequenceEqual(new[] { "cancel" }),
                "Disabling a timed confirmation did not stop/reset its original lock and cancellation state.");
            UnityEngine.Object.Destroy(root);
            yield return null;
        }
    }
}
