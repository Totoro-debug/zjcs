using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Official.BattleBgm;
using Zjcs.Offline.BattleNativePlayables;

namespace Zjcs.Editor
{
    public static partial class OfficialBattleWwiseSoundAcceptance
    {
        [Serializable] private sealed class MusicTestScene { public int[] game_ids; public string resource; }
        [Serializable] private sealed class MusicTestOverride { public int id; public string group, value, config; }
        [Serializable] private sealed class MusicTestLocation { public string location, resource; }
        [Serializable] private sealed class MusicTestBindings
        {
            public string default_config_resource;
            public MusicTestScene[] scenes;
            public MusicTestOverride[] stage_override_bgm, fight_theme;
            public MusicTestLocation[] configs;
        }
        private static IEnumerator BgmSceneSteps()
        {
            var camera = new GameObject("Original battle music scene verification");
            var bindings = JsonUtility.FromJson<MusicTestBindings>(Resources.Load<TextAsset>("Combat/Wwise/Bgm/catalog").text);
            var fallback = Resources.Load<GameBgmConfig>(bindings.default_config_resource);
            Require(bindings.scenes.Length == 229 && fallback != null, "all original scene music nodes and global config are bound");
            foreach (var row in bindings.scenes)
            {
                var prefab = Resources.Load<GameObject>(row.resource);
                Require(prefab != null && prefab.GetComponent<GameBgm>() != null && row.game_ids.Length > 0,
                    "original GameBgm prefab binding " + row.resource);
            }
            try
            {
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    lease.PlayMusic();
                    yield return new WaitForSecondsRealtime(.2f);
                    var providers = UnityEngine.Object.FindObjectsOfType<BgmGameObject>();
                    Require(providers.Length == 1, "one original global BGM provider");
                    var provider = providers[0].gameObject;
                    Require(provider.GetComponent<global::AkGameObj>() != null
                        && provider.GetComponent<Zjcs.Official.ProfessionUI.AkGameObj>() == null,
                        "original SDK BGM emitter retains its SDK lifecycle");
                    var listener = provider.GetComponent<global::AkAudioListener>();
                    Require(listener != null && !listener.isDefaultListener && provider.transform.position == Vector3.zero,
                        "original BGM dedicated listener and zero pose");
                    var serialized = new SerializedObject(provider.GetComponent<global::AkGameObj>());
                    Require(!serialized.FindProperty("m_listeners.useDefaultListeners").boolValue
                        && serialized.FindProperty("m_listeners.initialListenerList").arraySize == 1
                        && serialized.FindProperty("m_listeners.initialListenerList").GetArrayElementAtIndex(0).objectReferenceValue == listener,
                        "BGM emits to its original dedicated listener");
                    NativeMusicSwitch(provider, "BattleMusic", "Map_11_1_Map");
                    NativeMusicPlaying(provider, fallback.BgmEventConfig.PlayEvent.Id);
                    lease.SetVisible(false);
                    yield return new WaitForSecondsRealtime(.1f);
                    Require(listener.isActiveAndEnabled && global::AkAudioListener.DefaultListeners.ListenerList.Count == 0,
                        "hiding the camera listener preserves the original dedicated music listener");
                    NativeMusicPlaying(provider, fallback.BgmEventConfig.PlayEvent.Id);
                    lease.SetVisible(true);
                    yield return null;
                    NativeMusicSwitch(provider, "BattleMusic", "Map_11_1_Map");
                }
                yield return null;

                var stage = bindings.stage_override_bgm.First(row => !string.IsNullOrEmpty(row.group));
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    lease.PlayMusic(stage.id);
                    yield return new WaitForSecondsRealtime(.2f);
                    var provider = UnityEngine.Object.FindObjectOfType<BgmGameObject>().gameObject;
                    NativeMusicSwitch(provider, stage.group, stage.value);
                    Require(!UnityEngine.Object.FindObjectOfType<GameBgm>().enabled, "stage override disables the scene switch");
                    lease.SetVisible(false); lease.SetVisible(true);
                    yield return null;
                    NativeMusicSwitch(provider, stage.group, stage.value);
                }
                yield return null;

                var theme = bindings.fight_theme.Single();
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    lease.PlayMusic(stage.id, theme.id);
                    yield return new WaitForSecondsRealtime(.2f);
                    NativeMusicSwitch(UnityEngine.Object.FindObjectOfType<BgmGameObject>().gameObject, theme.group, theme.value);
                }
                yield return null;

                var special = bindings.stage_override_bgm.First(row => !string.IsNullOrEmpty(row.config));
                var specialConfig = Resources.Load<GameBgmConfig>(bindings.configs.Single(row => row.location == special.config).resource);
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    lease.PlayMusic(special.id);
                    yield return new WaitForSecondsRealtime(.2f);
                    NativeMusicPlaying(UnityEngine.Object.FindObjectOfType<BgmGameObject>().gameObject, specialConfig.BgmEventConfig.PlayEvent.Id);
                }
                yield return null;

                var separateScene = bindings.scenes.First(row => Resources.Load<GameObject>(row.resource)
                    .GetComponent<GameBgm>().BgmEventConfigOverride != null);
                var sceneConfig = Resources.Load<GameObject>(separateScene.resource).GetComponent<GameBgm>().BgmEventConfigOverride;
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, separateScene.game_ids[0]))
                {
                    lease.PlayMusic();
                    yield return new WaitForSecondsRealtime(.2f);
                    NativeMusicPlaying(UnityEngine.Object.FindObjectOfType<BgmGameObject>().gameObject, sceneConfig.PlayEvent.Id);
                }
                yield return null;
                using (var lease = OfflineBattleWwiseRuntime.Acquire(camera.transform, 1000))
                {
                    lease.SetVisible(false);
                    lease.PlayMusic();
                    yield return new WaitForSecondsRealtime(.2f);
                    var provider = UnityEngine.Object.FindObjectOfType<BgmGameObject>().gameObject;
                    NativeMusicSwitch(provider, "BattleMusic", "Map_11_1_Map");
                    NativeMusicPlaying(provider, fallback.BgmEventConfig.PlayEvent.Id);
                    Require(global::AkAudioListener.DefaultListeners.ListenerList.Count == 0,
                        "a battle initialized hidden does not enable the camera listener");
                    lease.SetVisible(true);
                    yield return null;
                    NativeMusicSwitch(provider, "BattleMusic", "Map_11_1_Map");
                    NativeMusicPlaying(provider, fallback.BgmEventConfig.PlayEvent.Id);
                }
                yield return null;
                Require(UnityEngine.Object.FindObjectsOfType<GameBgm>().Length == 0, "scene music nodes are released after their leases");
                Debug.Log("NATIVE_WWISE_BGM_SCENES_PASSED 229 original prefab bindings, dedicated listener, real music IDs, stage/theme/scene config priority and hidden return");
            }
            finally { UnityEngine.Object.Destroy(camera); }
        }
        private static void NativeMusicSwitch(GameObject provider, string group, string value)
        {
            Require(AkSoundEngine.GetSwitch(group, AkSoundEngine.GetAkGameObjectID(provider), out uint state) == AKRESULT.AK_Success
                && state == AkSoundEngine.GetIDFromString(value), "original native music selection " + group + "/" + value);
        }
        private static void NativeMusicPlaying(GameObject provider, uint eventId)
        {
            var ids = new uint[64]; uint count = (uint)ids.Length;
            Require(AkSoundEngine.GetPlayingIDsFromGameObject(AkSoundEngine.GetAkGameObjectID(provider), ref count, ids) == AKRESULT.AK_Success
                && ids.Take((int)count).Any(id => AkSoundEngine.GetEventIDFromPlayingID(id) == eventId),
                "original native music event is playing: " + eventId);
        }
    }
}
