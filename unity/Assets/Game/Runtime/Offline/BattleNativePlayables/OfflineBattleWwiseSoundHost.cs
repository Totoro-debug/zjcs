using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Official.BattleBgm;
using Zjcs.Official.ProfessionUI;
using SoundObject = Zjcs.Official.ProfessionUI.AkGameObj;

namespace Zjcs.Offline.BattleNativePlayables
{
    /// <summary>
    /// Native backend for the existing original-playable sound interface. The owner
    /// keeps this alive until all registered scene objects and graph pools are released.
    /// Listener hierarchy, camera following and saved volume ownership remain with the caller.
    /// </summary>
    public sealed class OfflineBattleWwiseSoundHost : IProfessionSoundHost, IProfessionSoundObjectHost, IBgmPlayback, IDisposable
    {
        [Serializable] private sealed class Package { public string file, bank, language; }
        [Serializable] private sealed class Catalog { public string manifest, sdk_version; public Package[] packages; }
        private readonly Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();
        private readonly HashSet<int> emptyEnvironments = new HashSet<int>();
        private readonly List<uint> banks = new List<uint>(), packages = new List<uint>();
        private readonly AkWwiseInitializationSettings settings;
        private readonly AkBasePlatformSettings platform;
        private GameObject listener;
        private uint initBank;
        private bool ownsEngine, initialized, disposed;

        public bool IsInitialized => initialized && !disposed;
        public float DefaultScalingFactor => platform.DefaultScalingFactor;

        public OfflineBattleWwiseSoundHost(string packageDirectory, GameObject defaultListener = null, bool muteOutput = false)
        {
            if (!Directory.Exists(packageDirectory)) throw new DirectoryNotFoundException(packageDirectory);
            if (AkSoundEngine.IsInitialized()) throw new InvalidOperationException("The native Wwise engine already has an owner.");
            settings = Resources.Load<AkWwiseInitializationSettings>("Combat/Wwise/AkWwiseInitializationSettings");
            var text = Resources.Load<TextAsset>("Combat/Wwise/catalog");
            if (settings == null || text == null) throw new InvalidOperationException("Original Windows Wwise bindings are missing.");
            platform = AkWwiseInitializationSettings.ActivePlatformSettings;
            var catalog = JsonUtility.FromJson<Catalog>(text.text);
            if (catalog.manifest != "884_156692" || catalog.sdk_version != "2022.1.18.8567" || catalog.packages == null)
                throw new InvalidDataException("Unexpected original battle sound catalog.");
            try
            {
                // AkWwiseInitializationSettings.InitializeSoundEngine, with the local
                // package path and WwiseManager's Minimum -> Init -> Minimum sequence.
                using (var native = platform.AkInitializationSettings)
                {
                    var result = AkSoundEngine.Init(native);
                    if (result != AKRESULT.AK_Success) AkSoundEngine.Term();
                    Check(result, "Initialize original Windows engine");
                }
                ownsEngine = true;
                if (muteOutput || Array.IndexOf(Environment.GetCommandLineArgs(), "-muteAudio") >= 0)
                    Check(AkSoundEngine.SetOutputVolume(0, 0), "Mute validation output");
                if (AkSoundEngine.InitSpatialAudio(platform.AkSpatialAudioInitSettings) != AKRESULT.AK_Success)
                    Debug.LogWarning("WwiseUnity: Failed to initialize spatial audio.");
                AkSoundEngine.InitCommunication(platform.AkCommunicationSettings);
                AkCallbackManager.Init(platform.CallbackManagerInitializationSettings);
                Check(AkSoundEngine.AddBasePath(Path.GetFullPath(packageDirectory)), "Bind local sound packages");
                Check(AkSoundEngine.SetCurrentLanguage("zh_CN"), "Select original Chinese language");
                LoadPackage("Minimum.pck");
                Check(AkSoundEngine.LoadBank("Init.bnk", out initBank), "Load original Init bank");
                LoadBank("Minimum");
                foreach (var package in catalog.packages)
                {
                    if (package.bank == "Minimum") continue;
                    if (!string.IsNullOrEmpty(package.language) && package.language != "zh_CN")
                        throw new InvalidDataException("Unbound original voice language: " + package.language);
                    LoadPackage(package.file);
                    LoadBank(package.bank);
                }
                initialized = true;
                if (defaultListener != null)
                {
                    Check((AKRESULT)RegisterGameObject(defaultListener, defaultListener.name), "Register original default listener");
                    listener = defaultListener;
                    Check(AkSoundEngine.SetDefaultListeners(new[] { Id(listener) }, 1), "Bind original default listener");
                    SetObjectPosition(listener, listener.transform.position, listener.transform.forward, listener.transform.up);
                }
            }
            catch { Dispose(); throw; }
        }

        private static ulong Id(GameObject value) => AkSoundEngine.GetAkGameObjectID(value);
        private void LoadPackage(string file)
        {
            Check(AkSoundEngine.LoadFilePackage(file, out uint id), "Load " + file);
            packages.Add(id);
        }
        private void LoadBank(string bank)
        {
            Check(AkSoundEngine.LoadBank(bank, out uint id), "Load " + bank);
            banks.Add(id);
        }

        public uint PostEvent(uint eventId, GameObject emitter)
        {
            RequireLive();
            RegisterEmitter(emitter);
            return AkSoundEngine.PostEvent(eventId, Id(emitter));
        }

        uint IBgmPlayback.PostEvent(string eventName, GameObject emitter)
            => string.IsNullOrEmpty(eventName) ? 0 : PostEvent(AkSoundEngine.GetIDFromString(eventName), emitter);

        void IBgmPlayback.SetSwitch(string group, string value, GameObject emitter)
        {
            RequireLive();
            if (string.IsNullOrEmpty(group)) return;
            RegisterEmitter(emitter);
            // Use the registered object's native ID, as for skill events. Initial
            // auto-registration is owned by RegisterEmitter above.
            Check(AkSoundEngine.SetSwitch(group, value, Id(emitter)), "Set original BGM switch");
        }

        private void RegisterEmitter(GameObject emitter)
        {
            if (emitter != null)
            {
                if (!objects.ContainsKey(emitter.GetInstanceID()))
                {
                    // The engine has one owner. Original SDK prefab objects
                    // (including the dedicated BGM emitter/listener) register
                    // themselves and retain their SDK destruction lifecycle.
                    if (AkSoundEngine.IsGameObjectRegistered(emitter)) return;
                    // Original GameObject PostEvent auto-registers through AkGameObj.
                    // The existing APK port owns Awake, offsets, epsilon and destruction.
                    var previous = OfficialWwiseEvent.Host;
                    try
                    {
                        OfficialWwiseEvent.Host = this;
                        var soundObject = emitter.GetComponent<SoundObject>();
                        if (soundObject == null) soundObject = emitter.AddComponent<SoundObject>();
                        soundObject.Register();
                        if (!objects.ContainsKey(emitter.GetInstanceID()))
                            throw new InvalidOperationException("The emitter belongs to another sound host.");
                    }
                    finally { OfficialWwiseEvent.Host = previous; }
                }
            }
        }

        public int RegisterGameObject(GameObject emitter, string objectName)
        {
            RequireLive();
            var result = AkSoundEngine.RegisterGameObj(emitter, objectName);
            if (result == AKRESULT.AK_Success) objects[emitter.GetInstanceID()] = emitter;
            return (int)result;
        }
        public void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up)
        {
            RequireLive();
            Check(AkSoundEngine.SetObjectPosition(Id(emitter), position, forward, up), "Update original sound position");
        }
        public void InitializeEmptyEnvironment(GameObject emitter, Collider sourceCollider)
        {
            RequireLive();
            if (sourceCollider != null) throw new InvalidOperationException("Original collider environments require their scene binding.");
            emptyEnvironments.Remove(emitter.GetInstanceID());
        }
        public void UpdateEmptyEnvironment(GameObject emitter, Vector3 position)
        {
            RequireLive();
            // AkGameObjEnvironmentData.UpdateAuxSend sends zero once for an empty
            // environment list, then suppresses repeated zero sends even when moving.
            if (!emptyEnvironments.Add(emitter.GetInstanceID())) return;
            using (var sends = new AkAuxSendArray())
                Check(AkSoundEngine.SetGameObjectAuxSendValues(Id(emitter), sends, 0), "Clear empty environment sends");
        }
        public void SetDefaultListeners(GameObject emitter)
        {
            RequireLive();
            Check(AkSoundEngine.ResetListenersToDefault(Id(emitter)), "Use default listeners");
        }
        public void SetScalingFactor(GameObject emitter, float factor)
        {
            RequireLive();
            Check(AkSoundEngine.SetScalingFactor(emitter, factor), "Set original attenuation scale");
        }
        public void UnregisterGameObject(GameObject emitter)
        {
            if (!IsInitialized || emitter == null || !objects.Remove(emitter.GetInstanceID())) return;
            emptyEnvironments.Remove(emitter.GetInstanceID());
            Check(AkSoundEngine.UnregisterGameObj(emitter), "Unregister original sound object");
        }

        public void SetSpeed(float speed)
        {
            RequireLive();
            Check(AkSoundEngine.SetRTPCValue(414249091u, speed), "Set original game-speed RTPC");
        }
        public void SetVolumes(float all, float music, float voice, float sound)
        {
            RequireLive();
            // Original WwiseManagerConfig references and SoundVolumeService truncation.
            Check(AkSoundEngine.SetRTPCValue(3240620755u, (int)(all * 100f)), "Set Volume_ALL");
            Check(AkSoundEngine.SetRTPCValue(3659619896u, (int)(music * 100f)), "Set Volume_BGM");
            Check(AkSoundEngine.SetRTPCValue(3676256460u, (int)(voice * 100f)), "Set Volume_Voice");
            Check(AkSoundEngine.SetRTPCValue(3673881719u, (int)(sound * 100f)), "Set Volume_SFX");
        }
        public void RenderFrame()
        {
            RequireLive();
            if (listener != null)
                SetObjectPosition(listener, listener.transform.position, listener.transform.forward, listener.transform.up);
            // AkInitializer.LateUpdate: deliver prior-frame callbacks before rendering.
            AkCallbackManager.PostCallbacks();
            Check(AkSoundEngine.RenderAudio(), "Render original audio frame");
        }
        public void SetApplicationActive(bool active)
        {
            RequireLive();
#if !UNITY_EDITOR
            if (!platform.SuspendAudioDuringFocusLoss) return;
            Check(active ? AkSoundEngine.WakeupFromSuspend() : AkSoundEngine.Suspend(platform.RenderDuringFocusLoss),
                "Update original application suspension");
            Check(AkSoundEngine.RenderAudio(), "Render application suspension");
#endif
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!ownsEngine) return;
            // WwiseManager.Dispose releases banks before Init and package handles;
            // original settings then stop, drain callbacks, Term and reset managers.
            try
            {
                AkSoundEngine.StopAll();
                foreach (var emitter in objects.Values)
                    if (emitter != null) AkSoundEngine.UnregisterGameObj(emitter);
                foreach (uint bank in banks) AkSoundEngine.UnloadBank(bank, IntPtr.Zero);
                if (initBank != 0) AkSoundEngine.UnloadBank(initBank, IntPtr.Zero);
                foreach (uint package in packages) AkSoundEngine.UnloadFilePackage(package);
            }
            finally
            {
                try { settings.TerminateSoundEngine(); }
                finally
                {
                    objects.Clear(); emptyEnvironments.Clear(); banks.Clear(); packages.Clear();
                    listener = null; initialized = false; ownsEngine = false;
                }
            }
        }
        private void RequireLive()
        {
            if (!IsInitialized) throw new ObjectDisposedException(nameof(OfflineBattleWwiseSoundHost));
        }
        private static void Check(AKRESULT result, string action)
        {
            if (result != AKRESULT.AK_Success) throw new InvalidOperationException(action + ": " + result);
        }
    }
}
