using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class BattleThemePlayerValidation
    {
        [Serializable] private sealed class AudioLifecycleResult { public AudioLifecycleCase[] cases; }
        [Serializable] private sealed class AudioLifecycleCase
        {
            public bool globalSuspend, platformSuspend, renderDuringFocusLoss;
            public long initial, focusLost, focusRestored, paused, unpaused;
        }

        private IEnumerator VerifyAudioLifecycle(OfficialBattleRuntimeController controller)
        {
            var runtime = GameObject.Find("Original Wwise local runtime");
            Require(runtime != null, "The formal battle has no application sound runtime.");
            // Home has no direct dependency on the SDK. Read its public runtime
            // settings/query through reflection only inside this explicit test.
            var sdk = AppDomain.CurrentDomain.GetAssemblies().Single(value => value.GetName().Name == "AK.Wwise.Unity.API");
            var settingsType = sdk.GetType("AkWwiseInitializationSettings", true);
            object global = settingsType.GetProperty("Instance").GetValue(null);
            object platform = settingsType.GetProperty("ActivePlatformSettings").GetValue(null);
            object globalAdvanced = global.GetType().GetField("AdvancedSettings").GetValue(global);
            object platformAdvanced = platform.GetType().GetField("AdvancedSettings").GetValue(platform);
            Require(!ReferenceEquals(globalAdvanced, platformAdvanced), "The original Windows settings were not independently loaded.");
            var globalSuspend = globalAdvanced.GetType().GetField("m_SuspendAudioDuringFocusLoss");
            var platformSuspend = platformAdvanced.GetType().GetField("m_SuspendAudioDuringFocusLoss");
            var platformRender = platformAdvanced.GetType().GetField("m_RenderDuringFocusLoss");
            bool oldGlobal = (bool)globalSuspend.GetValue(globalAdvanced);
            bool oldPlatform = (bool)platformSuspend.GetValue(platformAdvanced);
            bool oldRender = (bool)platformRender.GetValue(platformAdvanced);
            MethodInfo query = sdk.GetType("AkSoundEngine", true).GetMethod("GetSampleTick", Type.EmptyTypes);
            Func<ulong> tick = () => (ulong)query.Invoke(null, null);
            var cases = new List<AudioLifecycleCase>();
            try
            {
                foreach (var item in new[] {
                    new AudioLifecycleCase { globalSuspend = false, platformSuspend = true },
                    new AudioLifecycleCase { globalSuspend = true, platformSuspend = false },
                    new AudioLifecycleCase { globalSuspend = true, platformSuspend = true, renderDuringFocusLoss = true } })
                {
                    // Ensure the first background frame's original focus callback
                    // cannot leave a prior suspension active when testing the gate.
                    globalSuspend.SetValue(globalAdvanced, true);
                    runtime.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
                    globalSuspend.SetValue(globalAdvanced, item.globalSuspend);
                    platformSuspend.SetValue(platformAdvanced, item.platformSuspend);
                    platformRender.SetValue(platformAdvanced, item.renderDuringFocusLoss);
                    yield return MeasureAudioClock(tick, value => item.initial = value);
                    Require(item.initial > 0, "The formal sound clock did not start.");
                    runtime.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
                    yield return MeasureAudioClock(tick, value => item.focusLost = value);
                    Require((item.focusLost > 0) == (!item.globalSuspend || item.renderDuringFocusLoss),
                        "The formal focus callback ignored the original global/platform settings.");
                    runtime.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
                    yield return MeasureAudioClock(tick, value => item.focusRestored = value);
                    Require(item.focusRestored > 0, "The formal sound clock did not recover from focus loss.");
                    runtime.SendMessage("OnApplicationPause", true, SendMessageOptions.RequireReceiver);
                    yield return MeasureAudioClock(tick, value => item.paused = value);
                    Require((item.paused > 0) == !item.globalSuspend,
                        "The formal pause callback used the platform rendering option or gate.");
                    runtime.SendMessage("OnApplicationPause", false, SendMessageOptions.RequireReceiver);
                    yield return MeasureAudioClock(tick, value => item.unpaused = value);
                    Require(item.unpaused > 0, "The formal sound clock did not recover from pause.");
                    Require(controller.Phase == FightBattlePhase.Preparing,
                        "Sound callback injection changed the formal battle phase.");
                    cases.Add(item);
                }
            }
            finally
            {
                globalSuspend.SetValue(globalAdvanced, true);
                runtime.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
                globalSuspend.SetValue(globalAdvanced, oldGlobal);
                platformSuspend.SetValue(platformAdvanced, oldPlatform);
                platformRender.SetValue(platformAdvanced, oldRender);
                runtime.SendMessage("OnApplicationFocus", Application.isFocused, SendMessageOptions.RequireReceiver);
            }
            Require((bool)globalSuspend.GetValue(globalAdvanced) == oldGlobal
                && (bool)platformSuspend.GetValue(platformAdvanced) == oldPlatform
                && (bool)platformRender.GetValue(platformAdvanced) == oldRender,
                "The diagnostic did not restore the original audio settings.");
            File.WriteAllText(Path.Combine(output, "sound-lifecycle.json"),
                JsonUtility.ToJson(new AudioLifecycleResult { cases = cases.ToArray() }, true));
            Debug.Log("ORIGINAL_BATTLE_AUDIO_LIFECYCLE_WINDOWS_PASSED: injected formal callbacks, native clocks, independent global/platform gates, pause/focus rendering and settings restoration.");
        }

        private static IEnumerator MeasureAudioClock(Func<ulong> tick, Action<long> result)
        {
            yield return new WaitForSecondsRealtime(.12f);
            ulong before = tick();
            yield return new WaitForSecondsRealtime(.2f);
            result(checked((long)(tick() - before)));
        }
    }
}
