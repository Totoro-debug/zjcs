using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Offline.HomeCurrencyUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Official.HomeHudInfrastructureUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeSocialUiControls;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private readonly List<GameObject> nativeStatusEntries = new List<GameObject>();
        private NativeHomeStatusServices nativeStatusServices;

        private void BuildNativeStatusEntries()
        {
            try
            {
                var assets = OfflineHomeHudAssets.LoadDefault();
                nativeStatusServices = new NativeHomeStatusServices(this, assets);
                foreach (var original in assets.homePrefab.GetComponentsInChildren<GameMatchMiniPanel>(true))
                {
                    var root = ReplaceNativeStatusEntry(original.transform, assets);
                    root.GetComponent<GameMatchMiniPanel>().Services = nativeStatusServices;
                    root.SetActive(original.gameObject.activeSelf);
                }
                foreach (var original in assets.homePrefab.GetComponentsInChildren<PlayAsYouDownloadEntry>(true))
                {
                    var root = ReplaceNativeStatusEntry(original.transform, assets);
                    root.GetComponent<PlayAsYouDownloadEntry>().Services = nativeStatusServices;
                    root.SetActive(original.gameObject.activeSelf);
                }
                if (nativeStatusEntries.Count == 0)
                    throw new InvalidOperationException("The original HomeUI status controllers are unavailable.");
            }
            catch { ReleaseNativeStatusEntries(); throw; }
        }

        private GameObject ReplaceNativeStatusEntry(Transform original, OfflineHomeHudAssets assets)
        {
            string path = original.name;
            for (var parent = original.parent; parent != assets.homePrefab.transform; parent = parent.parent)
                path = parent.name + "/" + path;
            var previous = homePanel.transform.Find(path);
            if (!previous) throw new InvalidOperationException("Original HomeUI status path is unavailable: " + path);
            var slot = previous.parent;
            int sibling = previous.GetSiblingIndex();
            var staging = new GameObject("HomeStatusDependencyBinding");
            staging.SetActive(false);
            try
            {
                // Clone the complete selected prefab subtree. Its serialized
                // rectangles, references and initial states remain authoritative.
                var root = Object.Instantiate(original.gameObject, staging.transform, false);
                root.SetActive(false);
                nativeStatusEntries.Add(root);
                root.name = original.name;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    if (text.font) nativeMenuFonts.SetupFont(text.font);
                foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true))
                    text.Localizer = assets.Text;
                foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true))
                    sound.Post = PostPartnerPromptSound;
                previous.gameObject.SetActive(false);
                Object.Destroy(previous.gameObject);
                root.transform.SetParent(slot, false);
                root.transform.SetSiblingIndex(sibling);
                return root;
            }
            finally { Object.Destroy(staging); }
        }

        private void ReleaseNativeStatusEntries()
        {
            // Original OnDisable must release its subscriptions while the menu's
            // shared condition handler and font service still exist.
            foreach (var root in nativeStatusEntries)
                if (root) { root.SetActive(false); Object.Destroy(root); }
            nativeStatusEntries.Clear();
            nativeStatusServices = null;
        }

        private sealed class NativeHomeStatusServices : IHomeMatchServices, IHomeMatchPlayer,
            IHomeHudDownloadServices, IHomeHudDownloadPlayer, IHomeHudDownloadHandler
        {
            private readonly HomeHudView view;
            private readonly OfflineHomeHudAssets assets;
            private Action matchUpdates, confirmStart;
            private Action<string> collectOnce;
            private Action<PlayAsYouDownloadProgressUpdateEvent> downloadProgress;
            private Action<PlayAsYouDownloadStateUpdateEvent> downloadState;

            public NativeHomeStatusServices(HomeHudView view, OfflineHomeHudAssets assets)
            {
                this.view = view; this.assets = assets;
                var data = JObject.Parse(OfflineHomeCurrencyAssets.LoadDefault().configuration.text);
                CheckConditionId = (int)data["tables"]["game_settings"]["PlayAsYouDownloadCheckConditionId"];
            }
            // Confirmed offline scope has no multiplayer matching requests.
            IHomeMatchPlayer IHomeMatchServices.Player => this;
            public bool IsInGameMatch => false;
            public IHomeMatchInfo GameMatchInfo => null;
            public bool IsLeader => false;
            public DateTime GetDateTime() => DateTime.UtcNow;
            public string GetText(string key) => assets.Text(key);
            public event Action OnGameMatchUpdate { add => matchUpdates += value; remove => matchUpdates -= value; }
            public IDisposable SubscribeConfirmStart(Action listener)
            {
                confirmStart += listener;
                return new NativeStatusSubscription(() => confirmStart -= listener);
            }
            public void OperPlayerStopGameMatch(Action<object> onSuccess)
                => throw new InvalidOperationException("The offline home has no active matching request.");

            // All runtime resources ship locally. Disable the network download
            // feature without manufacturing a collected one-time reward.
            IHomeHudDownloadPlayer IHomeHudDownloadServices.Player => this;
            public IHomeHudDownloadHandler PaydHandler => this;
            public bool EnablePlayAsYouDownload => false;
            public PaydState CurrentState => PaydState.Downloaded;
            public long CurrentDownloadBytes => 0;
            public long TotalDownloadBytes => 0;
            public int CheckConditionId { get; }
            public IHomeHudCondition GetCondition(int id) => view.nativeMenuConditions.GetCondition(id);
            public IHomeHudConditionHandler ConditionHandler => view.nativeMenuConditionLifetime;
            public bool IsMet(IHomeHudCondition condition) => view.nativeMenuConditions.IsMet(condition);
            public UnityEngine.Object MapRootIdentity => null;
            public bool IsSplashHidden => true;
            public bool HasCollectedOnceReward(string type)
                => throw new InvalidOperationException("The disabled network download feature has no local reward record.");
            public event Action<string> OnCollectOnceReward { add => collectOnce += value; remove => collectOnce -= value; }
            public IDisposable SubscribeProgress(Action<PlayAsYouDownloadProgressUpdateEvent> listener)
            {
                downloadProgress += listener;
                return new NativeStatusSubscription(() => downloadProgress -= listener);
            }
            public IDisposable SubscribeState(Action<PlayAsYouDownloadStateUpdateEvent> listener)
            {
                downloadState += listener;
                return new NativeStatusSubscription(() => downloadState -= listener);
            }
            public UniTask LeaveMap() => throw NoDownload();
            public void PopupProgress(Action<IHomeHudDownloadProgressDialog> initialize) => throw NoDownload();
            public UniTask PopupProgressGlobal(Action<IHomeHudDownloadProgressDialog> initialize, bool instant) => throw NoDownload();
            private static Exception NoDownload() => new InvalidOperationException("The offline home has no pending download job.");
        }

        private sealed class NativeStatusSubscription : IDisposable
        {
            private Action release;
            public NativeStatusSubscription(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
    }
}
