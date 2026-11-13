using System;
using UnityEngine;
using Zjcs.Official.BattleBgm;

namespace Zjcs.Offline.BattleNativePlayables
{
    public sealed partial class OfflineBattleWwiseRuntime
    {
        [Serializable] private sealed class BgmOverride { public int id; public string group, value, config; }
        [Serializable] private sealed class BgmScene { public int[] game_ids; public string resource; }
        [Serializable] private sealed class BgmConfigLocation { public string location, resource; }
        [Serializable] private sealed class BgmBindings
        {
            public string manifest, provider_resource, default_config_resource;
            public BgmScene[] scenes;
            public BgmOverride[] stage_override_bgm, fight_theme;
            public FightThemeInfo[] theme_selection;
            public ThemeConditionInfo[] theme_conditions;
            public BgmConfigLocation[] configs;
        }
        private BgmBindings bgmBindings;
        private BgmGameObjectProvider bgmProvider;
        private BgmManager bgmManager;
        private GameObject bgmRoot;

        private void EnsureMusic()
        {
            if (bgmManager != null) return;
            var asset = Resources.Load<TextAsset>("Combat/Wwise/Bgm/catalog");
            bgmBindings = asset == null ? null : JsonUtility.FromJson<BgmBindings>(asset.text);
            if (bgmBindings?.manifest != "884_156692" || bgmBindings.scenes == null)
                throw new InvalidOperationException("Original battle BGM bindings are missing.");
            var prefab = Resources.Load<GameObject>(bgmBindings.provider_resource);
            if (prefab == null) throw new InvalidOperationException("Original BgmGameObject prefab is missing.");
            bgmRoot = new GameObject("Original global BGM");
            bgmRoot.SetActive(false);
            bgmRoot.transform.SetParent(transform, false);
            try
            {
                var node = UnityEngine.Object.Instantiate(prefab, bgmRoot.transform, false);
                var source = node.GetComponent<BgmGameObject>();
                if (source == null) throw new InvalidOperationException("Original BgmGameObject script is unbound.");
                bgmProvider = new BgmGameObjectProvider();
                source.mProvider = bgmProvider;
                // Original Awake supplies the shared object before GameBgm's
                // OnEnable and the manager's first switch/play operation.
                bgmRoot.SetActive(true);
                if (bgmProvider.GameObject != node)
                    throw new InvalidOperationException("Original BGM provider did not initialize.");
                bgmManager = new BgmManager(bgmProvider, host);
            }
            catch { ReleaseMusic(); throw; }
        }

        private GameBgmConfig LoadMusicConfig(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var binding = Array.Find(bgmBindings.configs, row => row.location == path);
            var result = binding == null ? null : Resources.Load<GameBgmConfig>(binding.resource);
            if (result == null) Debug.LogError("[Fight] BgmConfigPath 加载失败, path:" + path);
            return result;
        }

        // FightBgmOverrideHandler.ResolveEventConfig: explicit config, scene
        // config, global config. Resources supplies the immutable local assets.
        private BgmEventConfig ResolveMusicConfig(GameBgm scene, string path)
        {
            var configured = LoadMusicConfig(path);
            if (configured != null) return configured.BgmEventConfig;
            if (scene != null)
            {
                var sceneConfig = scene.BgmEventConfigOverride;
                if (sceneConfig != null) return sceneConfig;
            }
            var fallback = Resources.Load<GameBgmConfig>(bgmBindings.default_config_resource);
            if (fallback == null) throw new InvalidOperationException("Original global GameBgmConfig is missing.");
            return fallback.BgmEventConfig;
        }

        private void ReleaseMusic()
        {
            bgmManager = null; bgmProvider = null; bgmBindings = null; themeSelector = null;
            if (bgmRoot != null) { bgmRoot.SetActive(false); UnityEngine.Object.Destroy(bgmRoot); }
            bgmRoot = null;
        }

        public sealed partial class Lease
        {
            private PlayBgmHandle music;
            private GameObject musicSwitch;
            private string musicGroup, musicValue;

            // FightSceneRoot starts music only after InitFight succeeds. The
            // common controller calls this once its scene and hosts are ready.
            public void PlayMusic(int stageId = 0, int themeId = 0, int stageType = 0)
            {
                if (owner == null) throw new ObjectDisposedException(nameof(Lease));
                if (music != null) throw new InvalidOperationException("This battle music is already playing.");
                owner.EnsureMusic();
                var bindings = owner.bgmBindings;
                if (themeId == 0) themeId = owner.SelectOrdinaryDomainTheme(stageType);
                var binding = Array.Find(bindings.scenes, row => Array.IndexOf(row.game_ids, gameId) >= 0);
                var prefab = binding == null ? null : Resources.Load<GameObject>(binding.resource);
                if (prefab == null) throw new InvalidOperationException("Original GameBgm scene is missing: " + gameId);
                // Inject and apply the original override before OnEnable can
                // write the scene's switch, including when the scene is hidden.
                var staging = new GameObject("Original battle BGM injection");
                staging.SetActive(false);
                staging.transform.SetParent(root.transform, false);
                try
                {
                    musicSwitch = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                    var scene = musicSwitch.GetComponent<GameBgm>();
                    if (scene == null) throw new InvalidOperationException("Original GameBgm script is unbound.");
                    scene.mBgmGameObjectProvider = owner.bgmProvider;
                    BgmOverride selected = themeId > 0
                        ? Array.Find(bindings.fight_theme, row => row.id == themeId) : null;
                    if (themeId > 0 && selected == null)
                        throw new InvalidOperationException("Original fight theme is missing: " + themeId);
                    // A present theme suppresses stage overrides even when its
                    // group/config fields are empty, matching the original branch.
                    if (selected == null) selected = Array.Find(bindings.stage_override_bgm, row => row.id == stageId);
                    var config = owner.ResolveMusicConfig(scene, selected?.config);
                    musicGroup = selected?.group; musicValue = selected?.value;
                    if (!string.IsNullOrEmpty(musicGroup))
                    {
                        scene.enabled = false;
                        owner.bgmManager.SetSwitch(musicGroup, musicValue);
                    }
                    // Initialize the original scene switch even if this local
                    // battle starts hidden. Then mirror its camera visibility;
                    // GameBgm has no OnDisable music-stop operation.
                    musicSwitch.transform.SetParent(owner.transform, false);
                    musicSwitch.transform.SetParent(root.transform, false);
                    music = owner.bgmManager.Play(root.name, config);
                }
                catch { StopMusic(); throw; }
                finally { UnityEngine.Object.Destroy(staging); }
            }

            private void RestoreMusicSwitch()
            {
                // FightRoot.OnEnable reapplies an explicit theme/stage switch.
                // With no override, the original GameBgm.OnEnable handles it.
                if (music != null && !string.IsNullOrEmpty(musicGroup))
                    owner.bgmManager.SetSwitch(musicGroup, musicValue);
            }
            private void StopMusic()
            {
                music?.Dispose(); music = null;
                if (musicSwitch != null)
                { musicSwitch.SetActive(false); UnityEngine.Object.Destroy(musicSwitch); }
                musicSwitch = null; musicGroup = null; musicValue = null;
            }
        }
    }
}
