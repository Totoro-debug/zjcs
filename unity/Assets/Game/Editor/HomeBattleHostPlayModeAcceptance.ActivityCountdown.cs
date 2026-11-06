using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityCountdown(string output)
        {
            var previousFont = WorldMultiLanguageText.LanguageFont;
            float previousScale = Time.timeScale;
            WorldMultiLanguageText.LanguageFont = null;
            var fixture = new GameObject("ActivityCountdownFixture", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            var canvas = fixture.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = fixture.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            GameObject instance = null;
            try
            {
                var prefab = Resources.Load<GameObject>("DailyChallenges/Countdown/ActivityDomainBossCountdown");
                Require(prefab != null, "The original activity countdown is absent.");
                instance = UnityEngine.Object.Instantiate(prefab, fixture.transform, false);
                var texts = JsonUtility.FromJson<SelectorTexts>(Resources.Load<TextAsset>("DailyChallenges/Countdown/texts").text);
                foreach (var text in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = key => texts.texts.FirstOrDefault(row => row.key == key)?.text ?? key;
                Require(instance.GetComponentsInChildren<MonoBehaviour>(true).All(value => value != null),
                    "An original activity countdown script is unbound.");
                var countdown = instance.GetComponent<CountdownText>();
                var view = instance.GetComponent<CountdownView>();
                Require(countdown != null && view != null, "The original countdown/view reference is absent.");
                var fields = new SerializedObject(view);
                var language = (WorldMultiLanguageText)fields.FindProperty("mCountdownText").objectReferenceValue;
                var label = language.GetComponent<TMP_Text>();
                string format = texts.texts.Single(row => row.key == language.myKey).text;
                Require(new SerializedObject(countdown).FindProperty("mUpdateIntervalS").floatValue == .5f,
                    "The original countdown interval differs.");
                Require(fields.FindProperty("mFormat").enumValueIndex == 0, "The original time format differs.");

                int completed = 0;
                Time.timeScale = 0;
                countdown.Show(TimeSpan.FromDays(2) + TimeSpan.FromHours(3), () => completed++);
                string days = string.Format(texts.texts.Single(row => row.key == "TwoDayCountdown2").text, 2, 3);
                Require(label.text == string.Format(format, days),
                    "The original multi-day countdown language dependency is missing.");
                label.ForceMeshUpdate(true, true);
                Require(label.font.sourceFontFile != null && label.font.HasCharacters(label.text, out _, false, true),
                    "The original font cannot render the Chinese multi-day countdown.");
                countdown.Show(10, 5, () => completed++);
                yield return new WaitForSecondsRealtime(.7f);
                Require(label.text == string.Format(format, "00:05") && completed == 0,
                    "The original countdown did not pause with scaled time or display its initial remainder.");
                label.ForceMeshUpdate(true, true);
                Require(label.font.sourceFontFile != null && label.font.HasCharacters(label.text, out _, false, true),
                    "The activity countdown font cannot render the original time text.");
                Time.timeScale = 1;
                yield return new WaitForSeconds(.65f);
                Require(label.text == string.Format(format, "00:04") && completed == 0,
                    "The original countdown did not subtract its configured interval.");

                int replaced = 0;
                countdown.Show(.5, () => replaced++);
                countdown.Show(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(.5), () => completed++);
                yield return new WaitForSeconds(.8f);
                Require(replaced == 0 && completed == 1, "Replacing a countdown retained its old coroutine/callback.");
                yield return new WaitForSeconds(.6f);
                Require(completed == 1, "The activity countdown repeated its timeout callback.");
                countdown.Show(TimeSpan.FromSeconds(.5), () => completed++);
                countdown.Hide();
                yield return new WaitForSeconds(.8f);
                Require(!instance.activeSelf && completed == 1, "Hiding the original countdown did not cancel it.");
                countdown.Show(2, () => completed++);
                fixture.SetActive(false);
                yield return new WaitForSeconds(.7f);
                fixture.SetActive(true);
                yield return new WaitForSeconds(.7f);
                Require(completed == 1, "An ancestor disable retained or restarted the countdown callback.");
                countdown.Show(65, 65, () => completed++);
                Require(label.text == string.Format(format, "01:05"), "Re-showing did not refresh the original time text.");
                countdown.Show(TimeSpan.FromDays(2) + TimeSpan.FromHours(3), () => completed++);
                Time.timeScale = 0;
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(output, "activity-countdown.png"), image.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                Time.timeScale = 1;
                UnityEngine.Object.DestroyImmediate(instance);
                yield return new WaitForSeconds(.7f);
                Require(completed == 1, "Destroying the original countdown retained its callback.");
                Debug.Log("[Battle hosts] Original activity countdown native binding, Chinese multi-day time, scaled interval, replacement and hide/ancestor/destroy cancellation passed.");
            }
            finally
            {
                Time.timeScale = previousScale;
                WorldMultiLanguageText.LanguageFont = previousFont;
                UnityEngine.Object.Destroy(fixture);
            }
        }
    }
}
