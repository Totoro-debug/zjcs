using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ItemDetailUI
{
    public sealed class ItemSourceGo : UIBase
    {
        public GameObject ItemSourcePrefab;
        public Transform ItemSourceGetSlot;
        public RectTransform MapResourceJumpButtonRect;
        private IItemDetailBindings bindings;
        private int itemId, sourceCount;
        private long requireCount;
        private GameObject container;
        public bool HasSource => sourceCount > 0;
        public void Bind(IItemDetailBindings value) => bindings = value;
        public void Init(int id, GameObject owner = null, long required = 0) => InitWrapper(() =>
        { itemId = id; container = owner; requireCount = required; });
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            foreach (Transform child in ItemSourceGetSlot)
                if (child.gameObject.activeSelf) Destroy(child.gameObject);
            var sources = bindings.Sources(itemId);
            if (sources.Count == 0) return;
            sourceCount = 0;
            foreach (var source in sources)
            {
                if (source.Hidden) continue;
                var view = bindings.InstantiateUi(ItemSourcePrefab, ItemSourceGetSlot);
                view.SetActive(true);
                view.GetComponent<ItemSourceItem>().Init(source, container, requireCount);
                if (source.Type == ItemSourceType.MapResource && !MapResourceJumpButtonRect)
                    MapResourceJumpButtonRect = view.GetComponent<RectTransform>();
                sourceCount++;
            }
        }
        protected override void OnDisableAndInited()
        { MapResourceJumpButtonRect = null; base.OnDisableAndInited(); }
    }
}
