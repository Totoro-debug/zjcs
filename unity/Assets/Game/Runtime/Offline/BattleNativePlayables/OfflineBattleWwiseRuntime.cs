using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.BattleNativePlayables
{
    // WwiseManager owns one engine across scene changes. Battle scene leases own
    // only their original listener prefab; graph pools are released before leases.
    [DefaultExecutionOrder(-100)]
    public sealed partial class OfflineBattleWwiseRuntime : MonoBehaviour
    {
        [Serializable] private sealed class Bindings { public string manifest, prefab_resource; public int[] game_ids; }
        private static OfflineBattleWwiseRuntime instance;
        private static float[] volumes;
        private readonly HashSet<Lease> leases = new HashSet<Lease>();
        private OfflineBattleWwiseSoundHost host;
        private float speed = float.NaN;

        public static Lease Acquire(Transform outputCamera, int gameId)
        {
            if (outputCamera == null) throw new ArgumentNullException(nameof(outputCamera));
            if (!Application.isPlaying) throw new InvalidOperationException("Battle sound scenes require Play Mode.");
            var data = Resources.Load<TextAsset>("Combat/Wwise/listener-bindings");
            var bindings = data == null ? null : JsonUtility.FromJson<Bindings>(data.text);
            if (bindings?.manifest != "884_156692" || bindings.game_ids == null
                || Array.IndexOf(bindings.game_ids, gameId) < 0)
                throw new InvalidOperationException("Original battle listener binding is missing: " + gameId);
            var prefab = Resources.Load<GameObject>(bindings.prefab_resource);
            if (prefab == null) throw new InvalidOperationException("Original battle listener prefab is missing.");
            var owner = GetOrCreate();
            var root = new GameObject("Original battle sound scene " + gameId);
            // Independent local scene instances own independent original BGM keys.
            root.name += " " + root.GetInstanceID();
            root.SetActive(false);
            root.transform.SetParent(outputCamera, false);
            try
            {
                UnityEngine.Object.Instantiate(prefab, root.transform, false);
                var lease = new Lease(owner, root, gameId);
                owner.leases.Add(lease);
                root.SetActive(true);
                return lease;
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }

        public static void ConfigureVolumes(float all, float music, float voice, float sound)
        {
            volumes = new[] { all, music, voice, sound };
            if (instance != null && instance.host != null)
                instance.host.SetVolumes(all, music, voice, sound);
        }

        private static OfflineBattleWwiseRuntime GetOrCreate()
        {
            if (instance != null) return instance;
            var root = new GameObject("Original Wwise local runtime");
            root.SetActive(false);
            var owner = root.AddComponent<OfflineBattleWwiseRuntime>();
            try
            {
                bool mute = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
#if UNITY_EDITOR
                mute = true;
#endif
                // Original AkInitializer precedes the scene's AkAudioListener.
                // The prefab's own SDK lifecycle registers its default listener.
                owner.host = new OfflineBattleWwiseSoundHost(OfflineBattleWwisePackages.DirectoryPath, muteOutput: mute);
                if (volumes != null) owner.host.SetVolumes(volumes[0], volumes[1], volumes[2], volumes[3]);
                instance = owner;
                UnityEngine.Object.DontDestroyOnLoad(root);
                root.SetActive(true);
                return owner;
            }
            catch { owner.host?.Dispose(); UnityEngine.Object.Destroy(root); throw; }
        }

        private void LateUpdate()
        {
            if (host == null) return;
            // GameSoundSpeedController writes the actual game time scale.
            if (speed != Time.timeScale) { speed = Time.timeScale; host.SetSpeed(speed); }
            // AkInitializer renders in LateUpdate even while gameplay is paused.
            host.RenderFrame();
        }
        private void OnApplicationPause(bool paused) { host?.SetApplicationActive(!paused); }
        private void OnApplicationFocus(bool focused) { host?.SetApplicationActive(focused); }
        private void OnApplicationQuit() { Release(); }
        private void OnDestroy() { Release(); }
        private void Release()
        {
            foreach (Lease lease in new List<Lease>(leases)) lease.Dispose();
            ReleaseMusic();
            host?.Dispose(); host = null;
            if (instance == this) instance = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaySession() { instance = null; volumes = null; }

        public sealed partial class Lease : IDisposable
        {
            private OfflineBattleWwiseRuntime owner;
            private readonly GameObject root;
            private readonly int gameId;
            internal Lease(OfflineBattleWwiseRuntime owner, GameObject root, int gameId)
            { this.owner = owner; this.root = root; this.gameId = gameId; }
            public IProfessionSoundHost SoundHost => owner != null ? owner.host
                : throw new ObjectDisposedException(nameof(Lease));
            public void SetVisible(bool visible)
            {
                if (owner == null) throw new ObjectDisposedException(nameof(Lease));
                if (visible && root != null && !root.activeSelf) RestoreMusicSwitch();
                if (root != null) root.SetActive(visible);
            }
            public void Dispose()
            {
                if (owner == null) return;
                StopMusic();
                owner.leases.Remove(this); owner = null;
                if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            }
        }
    }
}
