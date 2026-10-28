using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        // Presentation fixture: activates the original matching subtree, without starting a matchmaking request.
        private static IEnumerator VerifyPrivateMatchingAnimation(GameObject dialog, string output, string profilePath)
        {
            var receiver = dialog.GetComponentsInChildren<WorldAnimationSoundPlayer>(true).Single();
            var player = receiver.GetComponent<Animation>();
            if (player == null || player.clip == null || player.clip.name != "DailyDungeonDetailContent_GameMatch_Show"
                || !player.clip.legacy || player.clip.frameRate != 60f || player.clip.length != 56f / 60f
                || !player.enabled || !player.playAutomatically || player.animatePhysics
                || player.wrapMode != WrapMode.Default || player.cullingType != AnimationCullingType.AlwaysAnimate
                || !receiver.enabled || receiver.mEvents.Count != 1 || receiver.mEvents[0].name != "TeamMatch_Waiting_1")
                throw new InvalidDataException("Original private matching Animation or sound-player settings were not restored.");
            AnimationEvent expected = player.clip.events.Single();
            if (expected.functionName != "PostEvent" || expected.intParameter != 0 || expected.time != 5f / 60f)
                throw new InvalidDataException("Original private matching fifth-frame event was not restored.");
            GameObject matching = receiver.transform.parent.gameObject;
            if (matching.name != "GameMatch" || matching.activeSelf)
                throw new InvalidDataException("Original idle matching subtree was unexpectedly active.");
            byte[] profileBefore = File.ReadAllBytes(profilePath);
            int posts = 0;
            Action<AudioClip, GameObject> post = receiver.Post;
            if (post == null) throw new InvalidDataException("Original matching event has no local playback service.");
            receiver.Post = (audio, emitter) =>
            {
                if (audio != receiver.mEvents[0] || emitter != receiver.gameObject)
                    throw new InvalidDataException("Original matching animation posted a different event or emitter.");
                posts++; post(audio, emitter);
            };
            try
            {
                matching.SetActive(true);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (posts == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Transform circle = receiver.transform.Find("ImgMatchW_Circle1");
                Transform glow = receiver.transform.Find("ImgMatchW_Glow");
                if (receiver.GetComponentsInChildren<WorldRotateAnimation>(true).Length != 2
                    || circle == null || glow == null || !circle.GetComponent<WorldRotateAnimation>().enabled
                    || !glow.GetComponent<WorldRotateAnimation>().enabled)
                    throw new InvalidDataException("Original matching rotation components were not restored.");
                // Observe real Update frames; do not invoke the rotation component's callback.
                for (int frame = 0; frame < 3; frame++)
                {
                    Quaternion circleBefore = circle.localRotation, glowBefore = glow.localRotation;
                    yield return null;
                    Quaternion circleExpected = circleBefore * Quaternion.Euler(0f, 0f, 50f * Time.deltaTime);
                    Quaternion glowExpected = glowBefore * Quaternion.Euler(0f, 0f, 80f * Time.deltaTime);
                    if (Quaternion.Angle(circle.localRotation, circleExpected) > .1f
                        || Quaternion.Angle(glow.localRotation, glowExpected) > .1f)
                        throw new InvalidDataException("Original matching local rotation speed or delta-time update changed.");
                }
                var source = UnityEngine.Object.FindObjectsOfType<AudioSource>()
                    .SingleOrDefault(value => value.gameObject.name == "OfficialDungeonUiSound:TeamMatch_Waiting_1");
                if (posts != 1 || source == null || source.clip != receiver.mEvents[0] || source.loop
                    || source.spatialBlend != 0f || source.volume != 0f || source.transform.IsChildOf(dialog.transform)
                    || source.clip.channels != 2 || source.clip.frequency != 44100 || source.clip.samples != 96776)
                    throw new InvalidDataException("Original Legacy event did not post once to the independent muted PCM player.");
                yield return new WaitForSeconds(.5f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-matching-animation.png"));
                yield return new WaitForSeconds(.6f);
                if (posts != 1) throw new InvalidDataException("Original finite matching animation repeated its sound event.");
                matching.SetActive(false);
                deadline = Time.realtimeSinceStartup + 5f;
                while (source != null && Time.realtimeSinceStartup < deadline) yield return null;
                if (source != null || !File.ReadAllBytes(profilePath).SequenceEqual(profileBefore))
                    throw new InvalidDataException("Matching presentation changed the profile or leaked its local sound player.");
            }
            finally
            {
                if (receiver != null) receiver.Post = post;
                if (matching != null) matching.SetActive(false);
            }
        }
    }
}
