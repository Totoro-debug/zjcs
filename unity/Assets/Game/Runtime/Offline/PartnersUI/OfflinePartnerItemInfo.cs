using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // Direct projection of the selected official item table for the migrated
    // gift player and ItemIcon controllers.
    public sealed class OfflinePartnerItemInfo : IProfessionItemInfo
    {
        public int ClassId { get; }
        public string Icon { get; }
        public string IconEffect { get; }
        public Quality Quality { get; }
        public bool CanOverlap { get; }
        public OfflinePartnerItemInfo(OfficialPartnerCatalog catalog, int classId)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var row = catalog.Rows("item").FirstOrDefault(value => Convert.ToInt32(value["ClassId"]) == classId);
            if (row == null) throw new InvalidOperationException("Official partner item is missing: " + classId);
            ClassId = classId;
            Icon = Text(row, "Icon");
            IconEffect = Text(row, "IconEffect");
            Quality = (Quality)Number(row, "Quality");
            CanOverlap = !row.TryGetValue("CanOverlap", out var overlap) || Convert.ToBoolean(overlap);
        }
        public object GetFakeIcon() => null;
        private static string Text(Dictionary<string, object> row, string key)
            => row.TryGetValue(key, out var value) ? Convert.ToString(value) : string.Empty;
        private static long Number(Dictionary<string, object> row, string key)
            => row.TryGetValue(key, out var value) ? Convert.ToInt64(value) : 0L;
    }
}
