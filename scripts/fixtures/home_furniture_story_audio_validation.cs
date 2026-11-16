using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HomeFurnitureStoryAudioValidation
{
    private const string Active = "Zjcs.HomeFurnitureStoryAudioValidation.Active";
    static HomeFurnitureStoryAudioValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("FurnitureStoryAudioValidation").AddComponent<HomeFurnitureStoryAudioRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup >
                SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Furniture story voice validation timed out."));
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned validation project required.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 300);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("HOME_FURNITURE_STORY_AUDIO_PASSED: original local voices loaded and played silently.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeFurnitureStoryAudioRunner : MonoBehaviour
{
    [Serializable] private sealed class ClipRecord
    {
        public string eventKey, clip;
        public int channels, sampleRate, frames;
    }
    [Serializable] private sealed class ClipRecords { public ClipRecord[] clips; }
    private int errors;

    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += (message, stack, type) => {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        };
        var checks = Check();
        while (true)
        {
            object current;
            try { if (!checks.MoveNext()) break; current = checks.Current; }
            catch (Exception error) { HomeFurnitureStoryAudioValidation.Finish(error); yield break; }
            yield return current;
        }
        HomeFurnitureStoryAudioValidation.Finish(errors == 0 ? null : new Exception("Unexpected audio errors: " + errors));
    }

    private IEnumerator Check()
    {
        var asset = Resources.Load<TextAsset>("Home/FurnitureStoryAudioValidation");
        Require(asset, "Original audio verification data");
        var records = JsonUtility.FromJson<ClipRecords>(asset.text);
        Require(records?.clips != null && records.clips.Length == 13, "Thirteen original dialogue voices");
        gameObject.AddComponent<AudioListener>();
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        foreach (var record in records.clips)
        {
            var clip = Resources.Load<AudioClip>(record.clip);
            Require(clip && clip.channels == record.channels && clip.frequency == record.sampleRate
                && clip.samples == record.frames, "Original PCM data imported for " + record.eventKey);
            source.clip = clip;
            source.Play();
            yield return null;
            Require(source.isPlaying, "Original voice is playing silently: " + record.eventKey);
            source.Stop();
            Require(!source.isPlaying, "Voice stopped: " + record.eventKey);
            Resources.UnloadAsset(clip);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
