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
        private Func<string, string> previousLocalize;

        public void Open(OfficialPartnerRosterAssetCatalog assets, IPartnerRosterUiServices services)
        {
            if (assets == null || services == null) throw new ArgumentNullException();
            previousLocalize = WorldMultiLanguageText.Localize;
            var localize = assets.CreateLocalizer();
            WorldMultiLanguageText.Localize = key =>
            {
                string value = localize(key);
                return value == key ? previousLocalize?.Invoke(key) ?? key : value;
            };
            content = Instantiate(assets.RosterPrefab, transform, false);
            content.name = "OfficialPartnerRosterContent";
            content.SetActive(true);
            var list = content.GetComponent<NpcPanelNpcList>();
            if (list == null) throw new InvalidOperationException("Official partner roster list component is missing.");
            list.Configure(services);
            var loop = content.GetComponentInChildren<LoopVerticalScrollRect>(true);
            if (loop == null || loop.DefaultPrefabSource == null || loop.DefaultPrefabSource.Prefab == null)
                throw new InvalidOperationException("Official partner roster scroll binding is missing.");
            PrefabSource.SetValue(loop, new CardPool(loop.DefaultPrefabSource, services));
        }

        public void Close()
        {
            if (content != null) Destroy(content);
            content = null;
            WorldMultiLanguageText.Localize = previousLocalize;
            previousLocalize = null;
        }
    }
}
