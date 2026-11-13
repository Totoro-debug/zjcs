param([string]$SourceRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$sdk = [IO.File]::ReadAllText((Join-Path $repository '.tools/wwise/UnityIntegration-2022.1.18.8567.3199/Wwise/MonoBehaviour/Runtime/AkSoundEngineController.cs'))
$hostSource = [IO.File]::ReadAllText((Join-Path $SourceRoot 'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseSoundHost.cs'))
$runtimeSource = [IO.File]::ReadAllText((Join-Path $SourceRoot 'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseRuntime.cs'))

function Get-MethodBody([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing source method: $signature" }
    $open = $source.IndexOf('{', $start)
    $depth = 1; $end = $open + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        elseif ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw "Unbalanced source method: $signature" }
    return $source.Substring($start, $end - $start)
}

# Compile the original SDK's actual conditional callbacks as the oracle, and
# actual adapter methods/callbacks as the subject. Only native calls and live
# settings are substituted, so both Windows and Editor branches can be observed
# without changing the user's real application focus or audio device.
$sdkStart = $sdk.IndexOf('// In the Editor, the sound needs to keep playing')
$sdkEnd = $sdk.IndexOf('// Enable/Disable the audio when pressing play/pause in the editor.', $sdkStart)
if ($sdkStart -lt 0 -or $sdkEnd -lt 0) { throw 'Review the original SDK callback region.' }
$sdkCallbacks = $sdk.Substring($sdkStart, $sdkEnd - $sdkStart)
$sdkCallbacks = [regex]::Replace($sdkCallbacks, '#if UNITY_EDITOR\s*$', '')
$sdkActivate = Get-MethodBody $sdk 'private void ActivateAudio(bool activate, bool renderAnyway = false)'
$hostStart = $hostSource.IndexOf('public void SetApplicationPaused(')
if ($hostStart -lt 0) { $hostStart = $hostSource.IndexOf('public void SetApplicationActive(') }
$hostEnd = $hostSource.IndexOf('public void Dispose()', $hostStart)
if ($hostStart -lt 0 -or $hostEnd -lt 0) { throw 'Review the actual sound host callback region.' }
$hostMethods = $hostSource.Substring($hostStart, $hostEnd - $hostStart)
$runtimeCallbacks = (Get-MethodBody $runtimeSource 'private void OnApplicationPause(bool paused)') + "`n" +
    (Get-MethodBody $runtimeSource 'private void OnApplicationFocus(bool focused)')

$fixture = @'
using System;
using System.Collections.Generic;
using System.Reflection;
namespace NAMESPACE {
public enum AKRESULT { AK_Success }
public sealed class Settings {
    public bool SuspendAudioDuringFocusLoss, RenderDuringFocusLoss;
}
public static class AkWwiseInitializationSettings {
    public static readonly Settings Instance = new Settings();
    public static readonly Settings Platform = new Settings();
    public static Settings ActivePlatformSettings { get { return Platform; } }
}
public static class AkSoundEngine {
    public static readonly List<string> Trace = new List<string>();
    public static bool IsInitialized() { return true; }
    public static AKRESULT Suspend(bool render) { Trace.Add("Suspend:" + render); return AKRESULT.AK_Success; }
    public static AKRESULT WakeupFromSuspend() { Trace.Add("Wakeup"); return AKRESULT.AK_Success; }
    public static AKRESULT RenderAudio() { Trace.Add("Render"); return AKRESULT.AK_Success; }
}
public sealed class OriginalController {
SDK_CALLBACKS
SDK_ACTIVATE
}
public sealed class ActualHost {
    private readonly Settings platform = AkWwiseInitializationSettings.ActivePlatformSettings;
    private void RequireLive() { }
    private void Check(AKRESULT result, string operation) {
        if (result != AKRESULT.AK_Success) throw new Exception(operation);
    }
HOST_METHODS
}
public sealed class ActualRuntime {
    private readonly ActualHost host;
    public ActualRuntime(ActualHost value) { host = value; }
RUNTIME_CALLBACKS
    public void Invoke(bool pause, bool value) {
        GetType().GetMethod(pause ? "OnApplicationPause" : "OnApplicationFocus",
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, new object[] { value });
    }
}
public static class Verify {
    public static int Run() {
        int checks = 0;
        foreach (bool suspend in new[] { false, true })
        foreach (bool platformSuspend in new[] { false, true })
        foreach (bool render in new[] { false, true }) {
            var settings = AkWwiseInitializationSettings.Instance;
            settings.SuspendAudioDuringFocusLoss = suspend;
            var platform = AkWwiseInitializationSettings.ActivePlatformSettings;
            platform.SuspendAudioDuringFocusLoss = platformSuspend;
            platform.RenderDuringFocusLoss = render;
            var actual = new ActualRuntime(new ActualHost());
            var original = new OriginalController();
            // Include overlapping pause/focus sequences: the SDK applies each
            // callback directly rather than aggregating an invented active state.
            foreach (int item in new[] { 0, 1, 2, 3, 0, 2, 1, 3, 2, 0, 3, 1 }) {
                bool pause = item < 2, value = item == 0 || item == 3;
                AkSoundEngine.Trace.Clear();
                actual.Invoke(pause, value);
                string got = string.Join(",", AkSoundEngine.Trace);
                AkSoundEngine.Trace.Clear();
                if (pause) original.OnApplicationPause(value);
                else original.OnApplicationFocus(value);
                string wanted = string.Join(",", AkSoundEngine.Trace);
                if (got != wanted) throw new Exception("SDK lifecycle mismatch: globalSuspend=" + suspend
                    + ", platformSuspend=" + platformSuspend
                    + ", render=" + render + ", pauseCallback=" + pause + ", value=" + value
                    + ", actual=" + got + ", original=" + wanted);
                checks++;
            }
        }
        new ActualRuntime(null).Invoke(true, true);
        new ActualRuntime(null).Invoke(false, false);
        return checks;
    }
}
}
'@
foreach ($variant in @('Windows', 'Editor')) {
    $name = 'BattleSoundLifecycle' + $variant
    $source = $fixture.Replace('NAMESPACE', $name).Replace('SDK_CALLBACKS', $sdkCallbacks).
        Replace('SDK_ACTIVATE', $sdkActivate).Replace('HOST_METHODS', $hostMethods).
        Replace('RUNTIME_CALLBACKS', $runtimeCallbacks)
    if ($variant -eq 'Editor') { $source = "#define UNITY_EDITOR`n" + $source }
    Add-Type -TypeDefinition $source
    $count = ([type]($name + '.Verify')).GetMethod('Run').Invoke($null, @())
    Write-Output ("Original Wwise $variant callback comparisons passed: $count; null runtime host callbacks passed.")
}
