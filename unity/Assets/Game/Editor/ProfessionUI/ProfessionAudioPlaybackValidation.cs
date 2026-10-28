using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Editor.ProfessionUI
{
    [InitializeOnLoad]
    public static class ProfessionAudioPlaybackValidation
    {
        private const string Active = "Zjcs.ProfessionAudioProbe.Active";
        [Serializable] private sealed class Proof { public Expected[] events; }
        [Serializable] private sealed class Pcm { public int channels, rate, frames, sample_width; }
        [Serializable] private sealed class Expected { public string name, clip; public uint event_id; public Pcm pcm; }
        private static Proof proof;
        private static OfflineProfessionAudioCatalog catalog;
        private static OfflineProfessionSoundHost host;
        private static GameObject emitter;
        private static AudioSource voice;
        private static int current = -1;
        private static double postedAt, wallPostedAt;
        private static int warmupFrame;
        private static bool ready;
        private static bool advancing, audible;
        private static uint lastId;
        private static bool initialized;
        static ProfessionAudioPlaybackValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ready = true;
            };
        }
        private static string Project => Directory.GetParent(Application.dataPath).FullName;
        private static string Repository => Path.GetFullPath(Path.Combine(Project, "../../.."));
        private static string AssetPath(Expected expected) => expected.clip.Substring("unity/".Length);
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Profession native audio: " + message);
        }
        public static void Run()
        {
            Require(Project.Replace('\\', '/').Contains("/.tools/profession-ui-validation/unity"), "isolated validation project");
            try
            {
                proof = LoadProof();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Require(proof.events.Length == 5, "complete five-event source scope");
                foreach (Expected expected in proof.events) ValidatePcm(expected);
                catalog = AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset");
                Require(catalog != null && catalog.Events.Length == 5, "explicit native catalog import");
                foreach (Expected expected in proof.events)
                    Require(catalog.Events.Single(entry => entry.EventId == expected.event_id).Clip ==
                        AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath(expected)), "source event to native clip binding");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                SessionState.SetBool(Active, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Fail(exception); }
        }
        private static Proof LoadProof() => JsonUtility.FromJson<Proof>(File.ReadAllText(Path.Combine(Repository,
            "assets/reference/profession-ui/windows-audio-source-manifest.json")));
        private static void ValidatePcm(Expected expected)
        {
            string path = AssetPath(expected);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            Require(importer != null && !importer.forceToMono &&
                importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM &&
                importer.defaultSampleSettings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate,
                expected.name + " original channels/rate and no second compression/normalization");
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Require(clip != null && clip.channels == expected.pcm.channels && clip.frequency == expected.pcm.rate &&
                clip.samples == expected.pcm.frames, expected.name + " PCM dimensions");
            byte[] raw = File.ReadAllBytes(Path.Combine(Project, path));
            int cursor = 12, start = -1, length = 0;
            while (cursor + 8 <= raw.Length)
            {
                int size = BitConverter.ToInt32(raw, cursor + 4);
                if (System.Text.Encoding.ASCII.GetString(raw, cursor, 4) == "data")
                { start = cursor + 8; length = size; break; }
                cursor += 8 + size + (size & 1);
            }
            Require(start >= 0 && length == clip.samples * clip.channels * 2, "original PCM data chunk");
            var values = new float[clip.samples * clip.channels];
            Require(clip.LoadAudioData() && clip.GetData(values, 0), "native PCM loading");
            for (int i = 0; i < values.Length; i++)
                Require(Math.Abs(values[i] - BitConverter.ToInt16(raw, start + i * 2) / 32768f) < 0.0000001f,
                    expected.name + " exact decoded sample " + i);
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || !ready) return;
            try
            {
                if (!initialized)
                {
                    initialized = true; current = -1; lastId = 0;
                    Application.runInBackground = true;
                    proof = LoadProof();
                    catalog = AssetDatabase.LoadAssetAtPath<OfflineProfessionAudioCatalog>("Assets/Official/ProfessionAudio/ProfessionAudio.asset");
                    emitter = new GameObject("Profession audio original emitter probe");
                    emitter.AddComponent<AudioListener>();
                    host = new GameObject("Profession audio Windows platform host probe").AddComponent<OfflineProfessionSoundHost>();
                    host.Bind(catalog);
                    warmupFrame = Time.frameCount;
                    Debug.Log("Profession audio probe ready: DSP=" + AudioSettings.dspTime +
                        " sampleRate=" + AudioSettings.outputSampleRate + " listenerPause=" + AudioListener.pause +
                        " listenerVolume=" + AudioListener.volume + " editorPaused=" + EditorApplication.isPaused);
                    return;
                }
                if (current < 0)
                {
                    if (Time.frameCount > warmupFrame + 2) Next();
                    return;
                }
                Require(EditorApplication.timeSinceStartup - wallPostedAt < 8,
                    "audio device DSP timeout: " + AudioSettings.dspTime + " source=" +
                    (voice == null ? "released" : voice.isPlaying + "/" + voice.timeSamples));
                var values = new float[1024]; AudioListener.GetOutputData(values, 0);
                audible |= values.Any(value => Math.Abs(value) > 0.000001f);
                advancing |= voice != null && voice.isPlaying && voice.timeSamples > 0;
                if (AudioSettings.dspTime - postedAt < catalog.Events.Single(entry =>
                    entry.EventId == proof.events[current].event_id).Clip.length + 0.15) return;
                Require(advancing, proof.events[current].name + " real AudioSource playback/sample advancement");
                Require(audible, proof.events[current].name + " nonzero actual listener PCM output");
                Debug.Log("Profession event played through actual Windows audio: " + proof.events[current].name);
                Next();
            }
            catch (Exception exception) { Fail(exception); }
        }
        private static void Next()
        {
            current++;
            if (current == proof.events.Length)
            {
                SessionState.SetBool(Active, false);
                Debug.Log("Profession native audio validation passed: 5 exact imported PCM clips, 5 explicit event bindings and 5 Play Mode AudioSource/listener output checks. Full profession UI mouse acceptance is not covered.");
                EditorApplication.Exit(0); return;
            }
            Expected expected = proof.events[current];
            var reference = ScriptableObject.CreateInstance<WwiseEventReference>(); reference.id = expected.event_id;
            var data = new OfficialWwiseEvent { WwiseObjectReference = reference };
            uint id = data.Post(emitter);
            Require(id != 0 && id != lastId && data.PlayingId == id, "valid unique playing ID returned and retained");
            lastId = id;
            voice = UnityEngine.Object.FindObjectsOfType<AudioSource>().Single(source =>
                source.name == "ProfessionSound:" + expected.event_id);
            Require(!voice.loop && voice.spatialBlend == 0 && voice.clip == catalog.Events.Single(entry =>
                entry.EventId == expected.event_id).Clip, "source one-shot, 2D and native clip identity");
            UnityEngine.Object.Destroy(reference);
            advancing = false; audible = false; postedAt = AudioSettings.dspTime;
            wallPostedAt = EditorApplication.timeSinceStartup;
            Debug.Log("Profession original event posted: " + expected.name + " DSP=" + postedAt +
                " playing=" + voice.isPlaying + " samples=" + voice.timeSamples + " frame=" + Time.frameCount);
        }
        private static void Fail(Exception exception)
        {
            SessionState.SetBool(Active, false);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
