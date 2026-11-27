using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Home
{
    public sealed partial class OfficialHomeRules
    {
        public void RegisterRuntimeFurnitureSkin(HomeFurnitureSkinRecord skin)
        {
            if (skin == null || skin.skinItemId <= 0) return;
            HomeFurnitureSkinRecord[] existing = Data.furnitureSkins ?? Array.Empty<HomeFurnitureSkinRecord>();
            if (existing.Any(value => value != null && value.skinItemId == skin.skinItemId)) return;
            var updated = new HomeFurnitureSkinRecord[existing.Length + 1];
            Array.Copy(existing, updated, existing.Length);
            updated[existing.Length] = skin;
            Data.furnitureSkins = updated;
        }

        public void RegisterRuntimeItem(HomeItemRecord item)
        {
            if (item == null || item.id <= 0) return;
            items[item.id] = item;
        }
    }
}
