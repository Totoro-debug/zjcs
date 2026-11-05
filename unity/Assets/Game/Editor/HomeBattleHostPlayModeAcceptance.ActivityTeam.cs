using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ActivityBattleUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private sealed class ActivityTeamFixture : IActivityBattleTeam
        {
            private Action changed;
            public event Action Changed { add { changed += value; } remove { changed -= value; } }
            public int MemberCount { get; set; } = 1;
            public int MaximumMemberCount { get; set; } = 4;
            public int Invitations;
            public int Subscribers => changed == null ? 0 : changed.GetInvocationList().Length;
            public void OpenInvitation() => Invitations++;
            public void Refresh() => changed?.Invoke();
        }

        private static IEnumerator ExerciseActivityTeam(string output, Action<Button> click)
        {
            var previousFont = WorldMultiLanguageText.LanguageFont;
            WorldMultiLanguageText.LanguageFont = null;
            var fixture = new GameObject("ActivityTeamFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            fixture.GetComponent<Canvas>().sortingOrder = 30000;
            var scaler = fixture.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            var host = new GameObject("OriginalButtonGoSize", typeof(RectTransform));
            host.transform.SetParent(fixture.transform, false);
            ((RectTransform)host.transform).sizeDelta = new Vector2(2041, 314.75537f);
            var prefab = Resources.Load<GameObject>("DailyChallenges/Team/ActivityDomainBossTeamButton");
            Require(prefab != null && !prefab.activeSelf, "Original activity invitation prefab/state is absent.");
            var instance = UnityEngine.Object.Instantiate(prefab, host.transform, false);
            try
            {
                var texts = JsonUtility.FromJson<SelectorTexts>(Resources.Load<TextAsset>("DailyChallenges/Team/texts").text);
                foreach (var language in instance.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    language.Localizer = key => texts.texts.FirstOrDefault(row => row.key == key)?.text ?? key;
                Require(instance.GetComponentsInChildren<MonoBehaviour>(true).All(value => value != null),
                    "An original activity invitation script remains unbound.");
                foreach (Image graphic in instance.GetComponentsInChildren<Image>(true))
                    Require(graphic.sprite != null, "An original invitation image is unbound: " + graphic.name);
                var control = instance.GetComponent<ActivityDomainBossTeamButton>();
                var first = new ActivityTeamFixture();
                control.Init(first);
                Require(first.Subscribers == 0, "An inactive activity invitation subscribed to team updates.");
                instance.SetActive(true);
                yield return new WaitForSecondsRealtime(.3f);
                var fields = new SerializedObject(control);
                var languageText = (WorldMultiLanguageText)fields.FindProperty("mTeamInviteText").objectReferenceValue;
                var text = languageText.GetComponent<TMP_Text>();
                var button = (Button)fields.FindProperty("mTeamInviteBtn").objectReferenceValue;
                var sound = button.GetComponent<WorldButtonSound>();
                Require(sound != null && sound.ClickEvent != null && sound.ClickEvent.samples > 0,
                    "The original invitation audio is unbound.");
                int sounds = 0;
                sound.Post = (clip, owner) => {
                    Require(clip == sound.ClickEvent && owner == button.gameObject,
                        "The original invitation sound event binding differs.");
                    sounds++;
                };
                string format = texts.texts.Single(row => row.key == languageText.myKey).text;
                Require(first.Subscribers == 1 && text.text == string.Format(format, 1, 4),
                    "The original invitation did not show the local team count.");
                first.MemberCount = 3; first.Refresh();
                Require(text.text == string.Format(format, 3, 4), "Team changes did not refresh the original count.");
                text.ForceMeshUpdate(true, true);
                Require(text.font.sourceFontFile != null && text.font.HasCharacters(text.text, out _, false, true),
                    "The original invitation font cannot render its Chinese text.");
                click(button);
                Require(first.Invitations == 1, "A simulated pointer did not open the activity invitation.");
                instance.SetActive(false);
                Require(first.Subscribers == 0, "A disabled invitation retained its team subscription.");
                first.MemberCount = 2; first.Refresh(); instance.SetActive(true);
                Require(first.Subscribers == 1 && text.text == string.Format(format, 2, 4),
                    "Re-enabling the invitation did not refresh the current team.");
                click(button);
                Require(first.Invitations == 2, "Re-enabling duplicated the invitation callback.");
                var second = new ActivityTeamFixture { MaximumMemberCount = 3, MemberCount = 2 };
                control.Init(second);
                Require(first.Subscribers == 0 && second.Subscribers == 1
                    && text.text == string.Format(format, 2, 3), "Reinitialization retained the previous team adapter.");
                click(button);
                Require(first.Invitations == 2 && second.Invitations == 1,
                    "Reinitialization invoked the wrong team or duplicated a listener.");
                Require(sounds == 3, "Invitation lifecycle duplicated the original click sound event.");
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(output, "activity-team.png"), image.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                UnityEngine.Object.DestroyImmediate(instance);
                Require(second.Subscribers == 0, "Destroying the invitation retained its team subscription.");
                Debug.Log("[Battle hosts] Original activity invitation pointer, Chinese text, local team count and adapter/enable/destroy lifecycle passed.");
            }
            finally { WorldMultiLanguageText.LanguageFont = previousFont; UnityEngine.Object.Destroy(fixture); }
        }
    }
}
