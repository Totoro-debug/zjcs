using System;
using System.IO;
using UnityEngine;
using Zjcs.Offline.Commerce;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Commerce
{
    /// <summary>
    /// Read-only HUD queries for commerce badges. They load the existing profile once and
    /// never create a default character or modify the save.
    /// </summary>
    public static class CommerceHudStatus
    {
        public static bool FirstPayShouldShow(string slotId)
        {
            try
            {
                if (!ProfileSlotCatalog.TryParseSlotId(slotId)) return false;
                var slots = new ProfileSlotCatalog(Application.persistentDataPath);
                if (!slots.Contains(slotId)) return false;
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenForCommerce(slots, slotId);
                string json = profile.Save.Player.CommerceStateJson;
                if (string.IsNullOrEmpty(json)) return true;
                OfflineCommerceSave save = JsonUtility.FromJson<OfflineCommerceSave>(json);
                if (save?.State == null || !OfflineCommerceState.IsValid(save.State)) return false;
                if (save.State.FirstPurchaseDay < 0) return true;
                int[] days = { 1, 2, 3 };
                foreach (int day in days)
                {
                    string key = day.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    bool claimed = false;
                    foreach (CommerceCounter item in save.State.FirstClaims)
                        if (item.Key == key && item.Count > 0) { claimed = true; break; }
                    if (!claimed) return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool HasCommerceSave(string slotId)
        {
            try
            {
                if (!ProfileSlotCatalog.TryParseSlotId(slotId)) return false;
                var slots = new ProfileSlotCatalog(Application.persistentDataPath);
                if (!slots.Contains(slotId)) return false;
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenForCommerce(slots, slotId);
                return !string.IsNullOrEmpty(profile.Save.Player.CommerceStateJson)
                    || File.Exists(slots.GetCommercePath(slotId));
            }
            catch
            {
                return false;
            }
        }
    }
}
