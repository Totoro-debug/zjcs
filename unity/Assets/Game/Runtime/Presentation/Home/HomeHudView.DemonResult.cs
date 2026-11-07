using System;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private DemonResultWindow demonResult;
        public void ShowDemonResult(FightBattleCheckpoint checkpoint, FightBattleReport report,
            OfflineWeeklyChallengeSession.ChallengeAward award, Action closed)
        {
            if (demonResult) throw new InvalidOperationException("A demon result is already open.");
            var owner = new GameObject("DemonResultWindow");
            owner.transform.SetParent(host.transform, false);
            demonResult = owner.AddComponent<DemonResultWindow>();
            try { demonResult.Initialize(host, canvas.transform, checkpoint, report, award, closed, BindNativeBattleResultReport); }
            catch { demonResult.Release(); throw; }
        }
    }
}
