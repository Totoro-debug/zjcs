using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureSlotsUI;
using Object=UnityEngine.Object;

namespace Zjcs.Offline.TreasureUI
{
    // The enclosing original promotion window supplies its authored parent and native-tree bindings.
    public sealed class OfflineTreasurePageView : IDisposable
    {
        public GameObject Root { get; }
        public TreasurePageSelectItem Content { get; }
        public OfflineTreasurePageView(Transform parent, ITreasurePageServices services, Action<GameObject> bindNativeTree)
        {
            if(!parent||services==null||bindNativeTree==null)throw new ArgumentNullException();
            var prefab=Resources.Load<GameObject>("Treasure/NativePage/TreasurePageSelectItem");
            if(!prefab)throw new InvalidOperationException("The original treasure page selector is unavailable.");
            var staging=new GameObject("Original treasure page service injection");staging.SetActive(false);
            try
            {
                Root=Object.Instantiate(prefab,staging.transform,false);
                Content=Root.GetComponent<TreasurePageSelectItem>();
                if(!Content)throw new InvalidOperationException("The original page controller is unbound.");
                Content.Configure(services);
                foreach(var cell in Root.GetComponentsInChildren<ItemIconTreasureSelectableItem>(true))cell.Configure(services);
                bindNativeTree(Root);
                Root.SetActive(false);Root.transform.SetParent(parent,false);
            }
            catch { if(Root)Object.Destroy(Root);throw; }
            finally { Object.Destroy(staging); }
        }
        public void Show(IProfessionItemData item)
        {
            if(!Root)throw new ObjectDisposedException(nameof(OfflineTreasurePageView));
            // TreasurePromotePanel.RefreshPageSelectItem enables the native hierarchy before InitWrapper.
            Root.SetActive(true);Content.Init(item);
        }
        public void Hide() { if(Root)Root.SetActive(false); }
        public void Dispose() { if(Root){Root.SetActive(false);Object.Destroy(Root);} }
    }
}
