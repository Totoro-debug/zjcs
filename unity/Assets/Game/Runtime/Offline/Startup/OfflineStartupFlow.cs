using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.Startup;

namespace Zjcs.Offline.Startup
{
    public sealed class OfflineStartupFlow : MonoBehaviour
    {
        private OfficialStartupAssets assets;
        private GameObject splash, theme, launch, age;
        private Slider progress;
        private TMP_Text status;
        public string Phase { get; private set; }
        public float Progress => progress == null ? 0f : progress.value;
        public bool HasOpenDialog => age != null;

        public void Begin(Action enterGame)
        {
            DontDestroyOnLoad(gameObject);
            assets = Resources.Load<OfficialStartupAssets>(OfficialStartupAssets.ResourcePath);
            if (assets == null) throw new InvalidOperationException("Official startup prefabs are not bound.");
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(events);
            }
            splash = Instantiate(assets.bootSplash, transform);
            StartCoroutine(Run(enterGame));
        }

        private IEnumerator Run(Action enterGame)
        {
            Phase = "BootSplash";
            Debug.Log("Startup: BootSplash");
            Animation animation = splash.GetComponent<Animation>();
            yield return null;
            yield return new WaitForEndOfFrame();
            animation.Play("BootSplashFadeIn");
            yield return new WaitForSecondsRealtime(animation["BootSplashFadeIn"].length);
            theme = Instantiate(assets.themeCanvas, transform);
            launch = Instantiate(assets.launchCanvas, transform);
            Transform panel = launch.transform.Find("Content/LaunchPanel");
            LaunchPanel launchPanel = launch.GetComponent<LaunchUIRoot>().LaunchPanel;
            // These are the original LaunchPanel/DefaultServerSetter branches
            // for a complete local resource set, before online authentication.
            panel.Find("Content/DefaultUniverseSetter").gameObject.SetActive(false);
            panel.Find("Content/RightTop").gameObject.SetActive(false);
            panel.Find("LaunchBg/Content/AgeTipGo/LowPerformanceTipGo").gameObject.SetActive(false);
            panel.Find("LaunchBg/Content/Version").GetComponent<TMP_Text>().text = "854.156383 · 离线版";
            panel.Find("LaunchBg/Content/AgeTipGo").GetComponent<AgeTipGo>().OpenDialog = OpenAgeTips;
            progress = launchPanel.ProgressSlider;
            status = launchPanel.StatusText;
            launchPanel.SetResourceUpdateState(true);
            launchPanel.OnProgress(assets.resourceStatus, 0f);
            animation.Play("BootSplashFadeOut");
            yield return new WaitForSecondsRealtime(animation["BootSplashFadeOut"].length);
            Destroy(splash);
            Phase = "Loading";
            Debug.Log("Startup: Loading local resources");
            yield return new WaitForEndOfFrame();
            yield return null;
            string[] resources = { "CharacterCreation/OfficialCharacterCreationAssetCatalog", "Character/OfficialCharacterAssetCatalog" };
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceRequest request = Resources.LoadAsync<UnityEngine.Object>(resources[i]);
                while (!request.isDone)
                {
                    progress.value = (i + request.progress) / resources.Length;
                    yield return null;
                }
                if (request.asset == null)
                {
                    Phase = "Failed";
                    status.text = "本地资源加载失败，请检查游戏文件。";
                    Debug.LogError("Startup asset missing: " + resources[i]);
                    yield break;
                }
                progress.value = (i + 1f) / resources.Length;
                yield return null;
            }
            status.text = assets.initializationStatus;
            progress.value = 1f;
            Phase = "Ready";
            Debug.Log("Startup: Ready");
            // Let the original full bar render; keep an explicitly opened
            // original dialog readable until its own confirmation is clicked.
            yield return new WaitForEndOfFrame();
            while (age != null) yield return null;
            yield return new WaitForEndOfFrame();
            try { enterGame(); }
            catch (Exception error)
            {
                Phase = "Failed";
                status.text = "启动失败，请检查游戏文件。";
                Debug.LogException(error);
                yield break;
            }
            yield return null;
            Phase = "Complete";
            Debug.Log("Startup: Complete, entering local save selection");
            Destroy(gameObject);
        }

        private void OpenAgeTips()
        {
            if (age != null) return;
            age = Instantiate(assets.ageTipsDialog, launch.transform.Find("Content/Popup"));
            age.GetComponent<Canvas>().overrideSorting = true;
            age.GetComponent<Canvas>().sortingOrder = 10001;
        }
    }
}
