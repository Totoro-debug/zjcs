using System;
using UnityEngine;
using Zjcs.Offline.PartnersUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private ActivityDomainBossRankView activityRankView;
        public void OpenDailyChallengeRank(int activityId)
        {
            if (activityRankView) { activityRankView.Close(); UnityEngine.Object.Destroy(activityRankView.gameObject); }
            var root = new GameObject("OriginalActivityRankBinding", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var view = root.AddComponent<ActivityDomainBossRankView>(); activityRankView = view;
            try
            {
                view.Open(host, activityId, () => { view.Close(); UnityEngine.Object.Destroy(root); OpenCharacterPage(); },
                    ((IOfflinePartnerPresentation)this).PostSound,
                    () => { if (activityRankView == view) activityRankView = null; });
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }
    }
}
