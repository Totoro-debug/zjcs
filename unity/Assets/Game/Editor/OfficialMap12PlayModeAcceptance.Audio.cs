using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void RunRuntimeAudio()
        {
            SessionState.SetBool(Key + ".runtimeTerminals", false);
            SessionState.SetBool(Key + ".runtimeSpatial", false);
            SessionState.SetBool(Key + ".runtimeDialogue", false);
            RunCore(false, false, 12, true, false, true);
        }

        [Serializable] private sealed class Map12VoiceBinding { public string eventKey, clip; public float gainDb; }
        [Serializable] private sealed class Map12VoiceCatalog { public Map12VoiceBinding[] events; }

        private static void AudioRequire(bool value, string message)
        {
            if (!value) throw new InvalidDataException(message);
        }

        private static IEnumerator VerifyRuntimeAudioSteps()
        {
            var music = RuntimeField<MonoBehaviour>("mapMusic");
            AudioRequire(music != null, "Map12 original BGM was not mounted by the actual runtime.");
            var bgm = music.GetComponent<AudioSource>();
            bgm.mute = true;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (bgm.clip == null && Time.realtimeSinceStartup < deadline) yield return null;
            AudioRequire(bgm.clip != null && bgm.clip.samples > 0 && bgm.loop && bgm.spatialBlend == 0f,
                "Map12 original BGM does not import as a local looping 2D clip.");
            map12Runtime.MapMusicVolume = () => .35f;
            yield return new WaitForSecondsRealtime(1.1f);
            AudioRequire(bgm.isPlaying && Mathf.Abs(bgm.volume - .35f) < .001f, "Original Play fade/setting differs.");
            music.GetType().GetMethod("Pause").Invoke(music, null);
            yield return new WaitForSecondsRealtime(.4f);
            AudioRequire(!bgm.isPlaying, "Original Pause fade did not pause the BGM.");
            int sample = bgm.timeSamples;
            yield return new WaitForSecondsRealtime(.2f);
            AudioRequire(bgm.timeSamples == sample, "Paused BGM lost its playback position.");
            music.GetType().GetMethod("Resume").Invoke(music, null);
            yield return new WaitForSecondsRealtime(.7f);
            AudioRequire(bgm.isPlaying && bgm.timeSamples > sample && Mathf.Abs(bgm.volume - .35f) < .001f,
                "Original Resume fade did not retain the BGM position/volume.");

            var catalog = JsonUtility.FromJson<Map12VoiceCatalog>(Resources.Load<TextAsset>("Map12/Audio/voices").text);
            var stories = RuntimeField<IDictionary>("stories").Values.Cast<Map10RuntimeStory>()
                .Concat(RuntimeField<IDictionary>("storySubgraphs").Values.Cast<Map10RuntimeStory>()).ToArray();
            string[] expected = stories.SelectMany(s => s.dialogueGroups).SelectMany(g => g.Lines)
                .Where(l => l.DialogType == "Voice" && !string.IsNullOrEmpty(l.VoiceEventKey))
                .Select(l => l.VoiceEventKey).Distinct().OrderBy(k => k).ToArray();
            AudioRequire(expected.Length == 236 && expected.SequenceEqual(catalog.events.Select(e => e.eventKey).OrderBy(k => k)),
                "The complete original Map12 voice closure is not bound.");
            foreach (var entry in catalog.events)
            {
                var request = Resources.LoadAsync<AudioClip>(entry.clip);
                yield return request;
                var clip = request.asset as AudioClip;
                AudioRequire(clip != null && clip.samples > 0 && clip.loadType == AudioClipLoadType.Streaming,
                    "Map12 dialogue voice failed Windows streaming import: " + entry.eventKey);
            }
            var voice = RuntimeField<MonoBehaviour>("mapVoice");
            var source = voice.GetComponent<AudioSource>();
            source.mute = true;
            map12Runtime.MapVoiceVolume = () => .4f;
            var gain = voice.GetComponent<OfficialWorldVoiceGain>();
            AudioRequire(gain != null && catalog.events.Count(e => e.gainDb == 2f) == 1
                && catalog.events.Where(e => e.gainDb != 0f).Single().eventKey == "chapter_3_main_42_03",
                "Original sound MakeUpGain is not preserved.");
            gain.SetDecibels(2f);
            float[] pcm = { .125f, -.25f };
            typeof(OfficialWorldVoiceGain).GetMethod("OnAudioFilterRead", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(gain, new object[] { pcm, 2 });
            float factor = (float)Math.Pow(10d, 2d / 20d);
            AudioRequire(Mathf.Abs(pcm[0] - .125f * factor) < .000001f && Mathf.Abs(pcm[1] + .25f * factor) < .000001f,
                "Positive Wwise gain was clamped instead of applied to both native PCM channels.");

            var story = stories.First(s => s.dialogueGroups.Any(g => g.Lines.Length >= 2
                && g.Lines.Take(2).All(l => l.DialogType == "Voice" && !string.IsNullOrEmpty(l.VoiceEventKey))));
            var group = story.dialogueGroups.First(g => g.Lines.Length >= 2
                && g.Lines.Take(2).All(l => l.DialogType == "Voice" && !string.IsNullOrEmpty(l.VoiceEventKey)));
            RuntimeCall("PlayStory", story.key, group.Id, null, null);
            yield return new WaitForSecondsRealtime(.5f);
            var first = catalog.events.Single(e => e.eventKey == group.Lines[0].VoiceEventKey);
            AudioRequire(source.isPlaying && source.clip.name == Path.GetFileName(first.clip)
                && Mathf.Abs(source.volume - .4f) < .001f, "Actual dialogue did not start its original voice/volume.");
            var ui = RuntimeField<OfficialWorldUiRenderer>("ui");
            string panel = RuntimeField<string>("dialoguePanelKey");
            var next = ui.Find(panel, "/" + panel + "/NextBtn").GetComponent<Button>();
            yield return new WaitForEndOfFrame();
            SimulateUiClick(next, next.GetComponentInParent<Canvas>().worldCamera);
            yield return new WaitForSecondsRealtime(.6f);
            // The first press may finish the typewriter before advancing.
            if (RuntimeField<int>("dialogueLineIndex") == 0)
            {
                SimulateUiClick(next, next.GetComponentInParent<Canvas>().worldCamera);
                yield return new WaitForSecondsRealtime(.6f);
            }
            var second = catalog.events.Single(e => e.eventKey == group.Lines[1].VoiceEventKey);
            AudioRequire(RuntimeField<int>("dialogueLineIndex") == 1 && source.isPlaying
                && source.clip.name == Path.GetFileName(second.clip), "Original next button did not replace the dialogue voice.");
            var textStory = stories.First(s => s.dialogueGroups.Any(g => g.Lines.Length > 0 && g.Lines[0].DialogType == "Text"));
            var textGroup = textStory.dialogueGroups.First(g => g.Lines.Length > 0 && g.Lines[0].DialogType == "Text");
            RuntimeCall("PlayStory", story.key, group.Id, null, null);
            RuntimeCall("PlayStory", textStory.key, textGroup.Id, null, null);
            yield return new WaitForSecondsRealtime(.4f);
            AudioRequire(!source.isPlaying && source.clip == null, "Text-only dialogue did not cancel stale asynchronous voice playback.");
            music.GetType().GetMethod("StopAndRelease").Invoke(music, null);
            yield return new WaitForSecondsRealtime(.4f);
            AudioRequire(!music, "Original Stop fade did not release the music host.");
            Debug.Log("Map12 runtime audio passed: original BGM four fades/loop/settings and pause position; 236 local streaming voice imports, actual dialogue voice, original next-button pointer input, text-only stop/stale-load cancellation and original +2dB PCM gain. Sources muted; audible listening and full mainline routes are not covered.");
        }
    }
}
