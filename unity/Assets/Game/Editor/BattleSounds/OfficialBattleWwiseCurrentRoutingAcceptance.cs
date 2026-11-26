using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;

namespace Zjcs.Editor
{
    public static class OfficialBattleWwiseCurrentRoutingAcceptance
    {
        [Serializable] private sealed class EventSource { public uint id; public string name; }
        [Serializable] private sealed class Source { public EventSource[] events; }
        [Serializable] private sealed class PlayingEvent { public uint id, playingId; public string name; }
        [Serializable] private sealed class Result { public bool passed; public PlayingEvent[] events; }

        public static void RunAndDeliver()
        {
            Run();
            OfficialBattleWwisePackageBuild.DeliverConfiguredPlayer();
        }

        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(workspace)
                || !File.Exists(Path.Combine(workspace, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use an isolated validation workspace.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.Combine(root,
                "assets/reference/battle-animation/windows-sound-routing-source-manifest.json")));
            if (source.events == null || source.events.Length == 0)
                throw new InvalidOperationException("Current original routing has no verified events.");
            OfficialBattleWwisePackageBuild.Validate();
            var listener = new GameObject("Muted current sound routing listener");
            var emitter = new GameObject("Muted current sound routing emitter");
            var result = new Result();
            var played = new List<PlayingEvent>();
            try
            {
                using (var host = new OfflineBattleWwiseSoundHost(OfflineBattleWwisePackages.DirectoryPath, listener, true))
                {
                    foreach (EventSource entry in source.events)
                    {
                        if (AkSoundEngine.GetIDFromString(entry.name) != entry.id)
                            throw new InvalidOperationException("Current native sound event identity differs.");
                        uint playing = host.PostEvent(entry.id, emitter);
                        host.RenderFrame();
                        if (playing == AkSoundEngine.AK_INVALID_PLAYING_ID
                            || AkSoundEngine.GetEventIDFromPlayingID(playing) != entry.id
                            || AkSoundEngine.GetGameObjectFromPlayingID(playing) != AkSoundEngine.GetAkGameObjectID(emitter))
                            throw new InvalidOperationException("Current original sound event did not reach its native emitter: " + entry.name);
                        played.Add(new PlayingEvent { id = entry.id, name = entry.name, playingId = playing });
                    }
                    host.RenderFrame();
                }
                if (AkSoundEngine.IsInitialized())
                    throw new InvalidOperationException("Current routing verification retained the native engine.");
                result.passed = true;
                Debug.Log("CURRENT_WWISE_ROUTING_NATIVE_PASSED events=" + played.Count);
            }
            finally
            {
                result.events = played.ToArray();
                File.WriteAllText(Path.Combine(workspace, "current-sound-routing-result.json"), JsonUtility.ToJson(result, true));
                UnityEngine.Object.DestroyImmediate(emitter);
                UnityEngine.Object.DestroyImmediate(listener);
            }
        }
    }
}
