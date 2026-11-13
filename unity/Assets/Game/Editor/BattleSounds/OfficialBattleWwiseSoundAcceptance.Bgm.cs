using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.BattleBgm;
using Zjcs.Offline.BattleNativePlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleWwiseSoundAcceptance
    {
        private sealed class BgmRecorder : IBgmPlayback
        {
            public readonly List<string> Calls = new List<string>();
            public GameObject LastEmitter;
            public uint PostEvent(string name, GameObject emitter)
            { LastEmitter = emitter; Calls.Add(name); return 1; }
            public void SetSwitch(string group, string value, GameObject emitter)
            { LastEmitter = emitter; Calls.Add(group + "=" + value); }
            public void Expect(params string[] expected)
            {
                Require(string.Join(",", Calls) == string.Join(",", expected),
                    "original BGM operations: " + string.Join(",", Calls));
                Calls.Clear();
            }
        }
        private static BgmEventKeyConfig BgmConfig(string prefix) => new BgmEventKeyConfig
        {
            PlayEventKey = prefix + "_Play", StopEventKey = prefix + "_Stop",
            PauseEventKey = prefix + "_Pause", ResumeEventKey = prefix + "_Resume"
        };
        private static void BgmRules(GameObject emitter)
        {
            var provider = new BgmGameObjectProvider { GameObject = emitter };
            var playback = new BgmRecorder();
            var manager = new BgmManager(provider, playback);
            var a = manager.Play("scene A", BgmConfig("A"));
            var b = manager.Play("scene B", BgmConfig("B"));
            b.Dispose(); a.Dispose(); a.Dispose();
            playback.Expect("A_Play", "A_Pause", "B_Play", "B_Stop", "A_Resume", "A_Stop");

            a = manager.Play("same", BgmConfig("A"));
            b = manager.Play("same", BgmConfig("A"));
            a.Dispose(); playback.Expect("A_Play", "A_Pause", "A_Resume");
            b.Dispose(); playback.Expect("A_Stop");

            a = manager.Play("lower", BgmConfig("A"));
            b = manager.Play("upper", BgmConfig("B"));
            playback.Expect("A_Play", "A_Pause", "B_Play");
            a.Dispose(); playback.Expect("A_Stop");
            manager.Stop("unknown"); playback.Expect();
            b.Dispose(); playback.Expect("B_Stop");

            manager.EnterDeferStatus(); manager.EnterDeferStatus();
            a = manager.Play("deferred A", BgmConfig("A"));
            b = manager.Play("deferred B", BgmConfig("B"));
            b.Dispose(); playback.Expect();
            // Switches are immediate even when play/stop operations are deferred.
            manager.SetSwitch("BattleMusic", "Map_11_1_Map");
            playback.Expect("BattleMusic=Map_11_1_Map");
            Require(playback.LastEmitter == emitter, "BGM uses the original provider object");
            manager.ExitDeferStatus();
            playback.Expect("A_Play", "A_Pause", "B_Play", "B_Stop", "A_Resume");
            manager.ExitDeferStatus(); playback.Expect();
            a.Dispose(); playback.Expect("A_Stop");

            manager.EnterDeferStatus();
            manager.Play("short deferred scene", BgmConfig("A")).Dispose();
            playback.Expect(); manager.ExitDeferStatus(); playback.Expect("A_Play", "A_Stop");

            // Repeated keys retain the first config, but Play consumes the current
            // argument for Resume exactly as the original manager does.
            a = manager.Play("same", BgmConfig("A"));
            b = manager.Play("same", BgmConfig("B"));
            playback.Expect("A_Play", "A_Pause", "B_Resume");
            b.Dispose(); playback.Expect(); a.Dispose(); playback.Expect("A_Stop");
            Debug.Log("ORIGINAL_BGM_MANAGER_RULES_PASSED priority, repeated keys, lower removal, unknown stop, idempotent handles and FIFO defer");
        }
        private static IEnumerator BgmSteps()
        {
            var emitter = new GameObject("Original BGM manager verification");
            var listener = new GameObject("Original BGM listener verification");
            try
            {
                BgmRules(emitter);
                using (var host = new OfflineBattleWwiseSoundHost(OfflineBattleWwisePackages.DirectoryPath, listener, true))
                {
                    var manager = new BgmManager(new BgmGameObjectProvider { GameObject = emitter }, host);
                    // Original game_1000 GameBgm switch and default GameBgmConfig.
                    manager.SetSwitch("BattleMusic", "Map_11_1_Map");
                    Require(emitter.GetComponent<Zjcs.Official.ProfessionUI.AkGameObj>() != null
                        && emitter.GetComponent<global::AkGameObj>() == null,
                        "BGM switch registers exactly one existing original sound object");
                    var a = manager.Play("first scene", BgmConfig("BattleBgm"));
                    host.RenderFrame(); yield return new WaitForSecondsRealtime(.1f);
                    Require(AkSoundEngine.GetSwitch(762511844u, AkSoundEngine.GetAkGameObjectID(emitter), out uint actual)
                        == AKRESULT.AK_Success && actual == 753380097u, "original BGM switch reaches the native engine");
                    {
                        var ids = new uint[32];
                        uint count = 32;
                        Require(AkSoundEngine.GetPlayingIDsFromGameObject(AkSoundEngine.GetAkGameObjectID(emitter), ref count, ids)
                            == AKRESULT.AK_Success && count > 0, "original battle BGM has a native playing ID");
                    }
                    var b = manager.Play("second scene", BgmConfig("BattleBgm"));
                    host.RenderFrame(); yield return new WaitForSecondsRealtime(.05f);
                    b.Dispose(); host.RenderFrame(); yield return new WaitForSecondsRealtime(.05f);
                    manager.EnterDeferStatus(); a.Dispose();
                    manager.ExitDeferStatus(); host.RenderFrame();
                    yield return new WaitForSecondsRealtime(.2f);
                    Debug.Log("NATIVE_WWISE_BGM_MANAGER_PASSED original switch, battle play, nested scene pause/stop/resume and deferred stop");
                }
            }
            finally { UnityEngine.Object.Destroy(emitter); UnityEngine.Object.Destroy(listener); }
            yield return null;
        }
    }
}
