using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.PartnersUI
{
    /// <summary>Hosts the original roster hierarchy while keeping all state local.</summary>
    public sealed class PartnerRosterPanelView : MonoBehaviour
    {
        private sealed class CardPool : LoopScrollPrefabSource
        {
            private readonly LoopScrollPrefabSource source;
            private readonly IPartnerRosterUiServices services;
            public CardPool(LoopScrollPrefabSource source, IPartnerRosterUiServices services)
            { this.source = source; this.services = services; }
            public Transform GetPoolRoot() => source.GetPoolRoot();
            public GameObject GetObject(int index)
            {
                var value = source.GetObject(index);
                value.GetComponent<NpcPanelNpcItem>()?.Configure(services);
                return value;
            }
            public void ReturnObject(Transform value, int index) => source.ReturnObject(value, index);
        }

        private static readonly FieldInfo PrefabSource = typeof(LoopScrollRectBase)
            .GetField("mPrefabSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private GameObject content;
        private IDisposable uiBinding;

        public void Open(OfficialPartnerRosterAssetCatalog assets, IPartnerRosterUiServices services)
        {
            if (assets == null || services == null) throw new ArgumentNullException();
            if (uiBinding != null) throw new InvalidOperationException("Original roster is already open.");
            uiBinding = OfflineUiBindingModule.Register(transform, new OfflineUiBindings {
                Font = () => Resources.Load<TMPro.TMP_FontAsset>("Combat/FZY4K-Dynamic"),
                Localize = assets.CreateLocalizer()
            });
            try
            {
                content = Instantiate(assets.RosterPrefab, transform, false);
                content.name = "OfficialPartnerRosterContent";
                // Configure the official list before enabling it. NpcPanelNpcList.OnEnable
                // immediately reads the local player and fills the loop-scroll cells.
                content.SetActive(false);
                var list = content.GetComponent<NpcPanelNpcList>();
                if (list == null) throw new InvalidOperationException("Official partner roster list component is missing.");
                list.Configure(services);
                var loop = content.GetComponentInChildren<LoopVerticalScrollRect>(true);
                if (loop == null || loop.DefaultPrefabSource == null || loop.DefaultPrefabSource.Prefab == null)
                    throw new InvalidOperationException("Official partner roster scroll binding is missing.");
                // Resolve the original source before wrapping it. Its getter creates
                // the inactive pool, so a new cell can be configured before OnEnable.
                var source = loop.prefabSource;
                PrefabSource.SetValue(loop, new CardPool(source, services));
                content.SetActive(true);
            }
            catch { Close(); throw; }
        }

        public void Close()
        {
            uiBinding?.Dispose(); uiBinding = null;
            if (content != null) Destroy(content);
            content = null;
        }
        private void OnDestroy() => Close();
    }
}
