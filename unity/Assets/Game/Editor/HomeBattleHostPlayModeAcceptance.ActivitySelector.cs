using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        [Serializable] private sealed class SelectorTexts { public SelectorText[] texts; }
        [Serializable] private sealed class SelectorText { public string key, text; }
        private static IEnumerator ExerciseActivitySelector(string output, Action<Button> click)
        {
            var canvas = new GameObject("ActivitySelectorFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            var parent = new GameObject("OriginalSelectorHostWidth", typeof(RectTransform));
            parent.SetActive(false);
            var rect = (RectTransform)parent.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(0, .5f);
            rect.sizeDelta = new Vector2(1080, 100);
            rect.anchoredPosition = new Vector2(-540, 0);
            var prefab = Resources.Load<GameObject>("DailyChallenges/Selector/ActivityDomainBossNumCell");
            Require(prefab != null, "Original activity difficulty prefab is absent.");
            var instance = UnityEngine.Object.Instantiate(prefab, rect, false);
            var texts = JsonUtility.FromJson<SelectorTexts>(Resources.Load<TextAsset>("DailyChallenges/Selector/texts").text);
            foreach (var value in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                value.Localizer = key => texts.texts.Single(row => row.key == key).text;
            Require(instance.GetComponentsInChildren<MonoBehaviour>(true).All(value => value != null),
                "The activity difficulty cell has an unbound original script.");
            var cell = instance.GetComponent<ActivityFestivalBossNumCell>();
            int clicked = -1, preselected = 0;
            bool unlocked = true;
            var context = new ActivityFestivalBossNumContext {
                SelectedIndex = -1, PreSelectedIndex = -1, DataUpdateCompleted = false,
                IsUnlockedDelegate = _ => unlocked, ShowRedDotDelegate = _ => true,
                OnCellClicked = index => clicked = index, OnCellPreSelected = () => preselected++
            };
            cell.Index = 2;
            cell.SetContext(context);
            cell.Initialize();
            parent.SetActive(true);
            cell.UpdateContent(2);
            cell.UpdatePosition(.5f);
            yield return new WaitForSecondsRealtime(.5f);
            var ports = new SerializedObject(cell);
            var lockImage = (Image)ports.FindProperty("mLockImg").objectReferenceValue;
            var effect = (TMPEffect)ports.FindProperty("mTMPEffect").objectReferenceValue;
            var red = (GameObject)ports.FindProperty("mRedDot").objectReferenceValue;
            var text = instance.GetComponentInChildren<TMP_Text>(true);
            var button = (Button)ports.FindProperty("mButton").objectReferenceValue;
            var animator = (Animator)ports.FindProperty("mAnimator").objectReferenceValue;
            text.ForceMeshUpdate(true, true);
            Require(text.font.sourceFontFile != null && text.font.HasCharacters(text.text, out _, false, true)
                && Enumerable.Range(0, text.textInfo.characterCount).All(index =>
                    text.textInfo.characterInfo[index].textElement.unicode == text.text[index]),
                "Original activity difficulty font cannot render its localized glyphs.");
            Require(text.text == string.Format(texts.texts.Single().text, 3)
                && red.activeSelf && !lockImage.gameObject.activeInHierarchy,
                "Original difficulty text or visibility differs: text=" + text.text
                + " reward=" + red.activeSelf + " locked=" + lockImage.gameObject.activeInHierarchy);
            Require(preselected == 0 && animator.runtimeAnimatorController != null && animator.speed == 0,
                "The loading phase emitted selection sound or lost the original sampled animator.");
            context.DataUpdateCompleted = true;
            cell.UpdatePosition(.5f);
            cell.UpdatePosition(.5f);
            Require(preselected == 1, "Preselection was not emitted exactly once for the newly centered cell.");
            click(button);
            Require(clicked == 2, "Pointer input did not select the original difficulty index.");
            foreach (bool selected in new[] { true, false })
            foreach (bool isUnlocked in new[] { true, false })
            {
                unlocked = isUnlocked;
                cell.UpdateContent(2);
                cell.UpdatePosition(selected ? .5f : .35f);
                yield return new WaitForSecondsRealtime(.1f);
                int status = selected ? (isUnlocked ? 0 : 1) : (isUnlocked ? 2 : 3);
                var value = ports.FindProperty("mStatusDataDict").FindPropertyRelative("mSerializeBuffer")
                    .GetArrayElementAtIndex(status).FindPropertyRelative("Value");
                Require(text.color == value.FindPropertyRelative("TextColor").colorValue
                    && effect.outlineColor == value.FindPropertyRelative("EffectColor").colorValue
                    && effect.enabled == value.FindPropertyRelative("ShowEffect").boolValue
                    && lockImage.gameObject.activeInHierarchy == !isUnlocked,
                    "Native activity difficulty visuals differ for status " + status);
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(output, "activity-selector-" + status + ".png"), image.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            context.SelectedIndex = 2;
            cell.UpdateContent(2);
            Require(!red.activeSelf, "A selected difficulty still shows its reward marker.");
            int before = preselected;
            parent.SetActive(false); parent.SetActive(true);
            Require(preselected == before, "Re-enabling the cell duplicated its preselection callback.");
            UnityEngine.Object.Destroy(canvas);
            yield return null;
            yield return ExerciseActivityScrollView(output, click);
            Debug.Log("[Battle hosts] Original activity difficulty click, four visual states, reward marker, sampled animator and re-enable passed.");
        }

        private static IEnumerator ExerciseActivityScrollView(string output, Action<Button> click)
        {
            var previousLocalize = WorldMultiLanguageText.Localize;
            var texts = JsonUtility.FromJson<SelectorTexts>(Resources.Load<TextAsset>("DailyChallenges/Selector/texts").text);
            WorldMultiLanguageText.Localize = key => texts.texts.FirstOrDefault(row => row.key == key)?.text
                ?? previousLocalize?.Invoke(key) ?? key;
            try
            {
            var canvas = new GameObject("ActivityScrollViewFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            var host = new GameObject("OriginalDetailHostSize", typeof(RectTransform));
            host.SetActive(false);
            var parent = (RectTransform)host.transform;
            parent.SetParent(canvas.transform, false);
            parent.sizeDelta = new Vector2(1080, 1920);
            var prefab = Resources.Load<GameObject>("DailyChallenges/Selector/ActivityDomainBossNumScrollView");
            Require(prefab != null, "Original activity scroll-view subtree is absent.");
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            var scroll = instance.GetComponent<ActivityFestivalBossNumScrollView>();
            var ports = new SerializedObject(scroll);
            var audio = (AudioClip)ports.FindProperty("mPreSelectedEvent").objectReferenceValue;
            Require(audio != null && audio.samples > 0 && instance.GetComponentsInChildren<MonoBehaviour>(true).All(x => x != null),
                "The original scroll-view audio or scripts are unbound.");
            int selected = -1, callbacks = 0, sounds = 0;
            scroll.Post = (clip, owner) => { Require(clip == audio && owner == instance, "Preselection audio binding differs."); sounds++; };
            scroll.SetSelectCallback(index => { selected = index; callbacks++; });
            scroll.SetIsUnlockedDelegate(index => index < 4);
            scroll.SetShowRedDotRewardDelegate(index => index == 3);
            host.SetActive(true);
            yield return null;
            scroll.UpdateData(Enumerable.Range(0, 8).ToArray(), 2);
            yield return new WaitForSecondsRealtime(.5f);
            Require(scroll.GetSelectionIndex() == 2 && selected == 2 && callbacks == 1 && sounds == 0,
                "Original scroll-view initialization selected the wrong difficulty or emitted audio.");
            var next = instance.GetComponentsInChildren<ActivityFestivalBossNumCell>().Single(x => x.Index == 3);
            click(next.GetComponentInChildren<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            Require(scroll.GetSelectionIndex() == 3 && selected == 3 && sounds > 0,
                "Original difficulty cell pointer did not scroll and emit its original selection audio.");
            scroll.SelectPrevCell();
            yield return new WaitForSecondsRealtime(.6f);
            Require(scroll.GetSelectionIndex() == 2, "Original previous-difficulty control did not return to index 2.");
            var viewport = (RectTransform)ports.FindProperty("cellContainer").objectReferenceValue;
            var start = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(viewport.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = start, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            var hit = hits.FirstOrDefault();
            Require(hit.gameObject != null && ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) == instance,
                "Original difficulty scroll view cannot receive a pointer drag.");
            pointer.pointerPressRaycast = hit;
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(instance, pointer, ExecuteEvents.beginDragHandler);
            for (int step = 1; step <= 12; step++)
            {
                pointer.position = start + Vector2.left * (viewport.rect.width / 6 * canvas.GetComponent<Canvas>().scaleFactor * step / 12);
                ExecuteEvents.Execute(instance, pointer, ExecuteEvents.dragHandler);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.25f);
            ExecuteEvents.Execute(instance, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(.6f);
            Require(scroll.GetSelectionIndex() == 3 && selected == 3, "Original difficulty pointer drag did not snap to the next cell.");
            scroll.SelectCell(7, true);
            yield return null;
            int before = callbacks;
            scroll.SelectNextCell();
            scroll.SelectCell(-1);
            yield return new WaitForSecondsRealtime(.4f);
            Require(scroll.GetSelectionIndex() == 7 && callbacks == before, "Out-of-range activity difficulty changed selection.");
            scroll.UpdateData(Enumerable.Range(0, 8).ToArray(), 2);
            yield return new WaitForSecondsRealtime(.3f);
            foreach (var text in instance.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(true, true);
                Require(text.font.sourceFontFile != null, "Pooled difficulty text lost its official source font.");
            }
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(output, "activity-selector-scroll.png"), image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            UnityEngine.Object.Destroy(canvas);
            yield return null;
            Debug.Log("[Battle hosts] Original activity scroll view pointer, drag/snap, bounds, refresh and audio event passed.");
            }
            finally { WorldMultiLanguageText.Localize = previousLocalize; }
        }
    }
}
