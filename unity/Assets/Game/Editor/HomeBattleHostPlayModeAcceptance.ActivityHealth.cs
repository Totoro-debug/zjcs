using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static IEnumerator ExerciseActivityHealth(string output)
        {
            var previousFont = WorldMultiLanguageText.LanguageFont;
            bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            WorldMultiLanguageText.LanguageFont = null;
            var fixture = new GameObject("Original activity HP fixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            fixture.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var prefab = Resources.Load<GameObject>("DailyChallenges/Health/ActivityDomainBossHpView");
            Require(prefab, "The original activity HP subtree is missing.");
            var instance = UnityEngine.Object.Instantiate(prefab, fixture.transform, false);
            try
            {
                var texts = JsonUtility.FromJson<SelectorTexts>(Resources.Load<TextAsset>("DailyChallenges/Health/texts").text);
                foreach (var language in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    language.Localizer = key => texts.texts.Single(row => row.key == key).text;
                Require(instance.GetComponentsInChildren<MonoBehaviour>(true).All(value => value != null),
                    "An original HP script is missing.");
                foreach (Image image in instance.GetComponentsInChildren<Image>(true))
                    Require(image.sprite && image.material.shader.isSupported, "An original HP image/shader is unbound.");
                var control = instance.GetComponent<ActivityDomainBossHpView>();
                var fields = new SerializedObject(control);
                var slider = (Slider)fields.FindProperty("mHpSlider").objectReferenceValue;
                var label = (WorldMultiLanguageText)fields.FindProperty("mHpText").objectReferenceValue;
                Require(slider && slider.fillRect && label && label.myKey == "ui_legionboss_remain_hp",
                    "The original HP field references differ.");
                var catalog = OfficialDailyChallengeCatalog.LoadDefault();
                var stages = OfflineDailyChallengeBattleCatalog.LoadDefault();
                var now = new DateTime(2026, 11, 6, 10, 0, 0, DateTimeKind.Local);
                foreach (int activity in new[] { 209, 212, 226 })
                {
                    string path = Path.Combine(output, "activity-health-" + activity + "-" + Guid.NewGuid().ToString("N") + ".json");
                    var progress = new OfflineDailyChallengeService(path);
                    progress.EnterFeature(now);
                    int duration = catalog.DurationHours(activity), limit = catalog.DailyLimit(activity);
                    Require(OfflineDomainBossBattleHealth.Read(progress, catalog, activity, 0) == 1f,
                        "A new independent encounter did not start with full HP.");
                    int monster = stages.FindStage(catalog.StageId(activity)).waveMonsterIdsFlat[0];
                    var checkpoint = new FightBattleCheckpoint { StageId = catalog.StageId(activity),
                        StageType = catalog.StageType(activity), RandomSeed = 1 };
                    checkpoint.Units.Add(HealthUnit(1, 1, 0, 0, 100, 100));
                    checkpoint.Units.Add(HealthUnit(2, 2, 0, monster, 250, 1000));
                    // Enemy summons with the same MonsterId must not dilute the boss's ratio.
                    checkpoint.Units.Add(HealthUnit(3, 2, 2, monster, 9000, 9000));
                    checkpoint.Units.Add(HealthUnit(4, 2, 0, monster + 1, 9000, 9000));
                    Require(progress.RecordParticipation(activity, now, duration, limit, 0,
                        FightBattleCheckpointSerializer.Save(checkpoint)), "Could not persist the HP fixture.");
                    progress = new OfflineDailyChallengeService(path);
                    Require(OfflineDomainBossBattleHealth.Read(progress, catalog, activity, 0) == .25f,
                        "Cold recovery did not read the configured boss's checkpoint HP.");
                    control.Refresh(OfflineDomainBossBattleHealth.Read(progress, catalog, activity, 0));
                    Require(slider.value == .25f && label.Text.Contains("25%"),
                        "The original HP slider/text did not consume the common checkpoint ratio.");
                    checkpoint.StageId++;
                    progress.SaveBattleCheckpoint(activity, FightBattleCheckpointSerializer.Save(checkpoint));
                    bool rejected = false;
                    try { OfflineDomainBossBattleHealth.Read(progress, catalog, activity, 0); }
                    catch (InvalidDataException) { rejected = true; }
                    Require(rejected, "A different battle stage was silently displayed as this boss's HP.");
                    progress.SaveBattleCheckpoint(activity, string.Empty);
                    progress = new OfflineDailyChallengeService(path);
                    Require(OfflineDomainBossBattleHealth.Read(progress, catalog, activity, 0) == 1f,
                        "A finished failed encounter leaked its damage into the next encounter.");
                    Require(!progress.CanParticipate(activity, now, duration, limit, out _)
                        && progress.CanParticipate(activity, now.AddDays(1), duration, limit, out _),
                        "The independent HP rule changed the daily participation receipt.");
                    progress.RecordDifficulty(activity, 1);
                    Require(OfflineDomainBossBattleHealth.Read(progress, catalog, activity, 0) == 0f,
                        "A completed tier lost the original defeated display.");
                    checkpoint.StageId--;
                    Require(progress.RecordParticipation(activity, now.AddDays(1), duration, limit, 0,
                        FightBattleCheckpointSerializer.Save(checkpoint)), "Could not persist the tier replay.");
                    Require(OfflineDomainBossBattleHealth.Read(new OfflineDailyChallengeService(path), catalog, activity, 0) == .25f,
                        "A replay's active checkpoint was hidden behind its completed-tier display.");
                }
                control.Refresh(.25f);
                yield return new WaitForSecondsRealtime(.3f);
                var text = label.GetComponent<TMP_Text>();
                text.ForceMeshUpdate(true, true);
                Require(text.font.sourceFontFile && text.font.HasCharacters(text.text, out _, false, true)
                    && text.textInfo.characterInfo.Any(character => character.isVisible),
                    "The original HP font cannot render its Chinese text.");
                Require(slider.fillRect.anchorMax.x > .249f && slider.fillRect.anchorMax.x < .251f,
                    "The original fill transform did not follow the displayed HP.");
                instance.SetActive(false); instance.SetActive(true); control.Refresh(1f);
                Require(slider.value == 1f && label.Text.Contains("100%"), "Reopening the HP view retained a stale value.");
                control.Refresh(.25f);
                yield return new WaitForEndOfFrame();
                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Vector3[] corners = new Vector3[4];
                    label.RectTransform.GetWorldCorners(corners);
                    Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                    Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                    int white = 0, cyan = 0;
                    for (int y = Mathf.Max(0, Mathf.FloorToInt(min.y)); y < Mathf.Min(frame.height, Mathf.CeilToInt(max.y)); y++)
                        for (int x = Mathf.Max(0, Mathf.FloorToInt(min.x)); x < Mathf.Min(frame.width, Mathf.CeilToInt(max.x)); x++)
                        {
                            Color pixel = frame.GetPixel(x, y);
                            if (pixel.r > .6f && pixel.g > .6f && pixel.b > .6f) white++;
                            if (pixel.r < .1f && pixel.g > .8f && pixel.b > .8f) cyan++;
                        }
                    Require(white > 50 && cyan == 0, "The original HP text has no raster glyphs or still shows a compiling-shader placeholder.");
                    Debug.Log("[Activity HP] Actual text raster: " + white + " light pixels, " + cyan + " cyan placeholder pixels.");
                }
                finally { UnityEngine.Object.Destroy(frame); }
                yield return Capture(output, "activity-original-health");
                Debug.Log("[Activity HP] Three activity checkpoint identities, summons exclusion, cold restore, failed-encounter clearing, daily limit, completed tiers/replays and original slider/text/font lifecycle passed. Component fixture; full page interaction remains pending.");
            }
            finally
            {
                UnityEngine.Object.Destroy(fixture);
                WorldMultiLanguageText.LanguageFont = previousFont;
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
            }
        }

        private static FightBattleUnitState HealthUnit(int id, int side, int master, int monster, long hp, long maxHp)
        {
            return new FightBattleUnitState { Id = id, Side = side, MasterId = master, MonsterId = monster,
                Hp = hp, MaxHp = maxHp, Turn = new FightTurnUnit { Id = id, MasterId = master } };
        }
    }
}
