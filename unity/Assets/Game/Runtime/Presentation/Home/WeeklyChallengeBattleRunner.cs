using System;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    // Activity hosts use this thin adapter to enter the already shared battle
    // runner. Period, participation and rewards stay outside presentation.
    public sealed class WeeklyChallengeBattleRunner : MonoBehaviour
    {
        private SkillDamageTestBattleRunner delegateRunner;

        public SkillDamageTestBattleRunner Delegate => delegateRunner;

        public void Begin(HomeFeatureRuntime runtime, int stageId)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (delegateRunner != null) throw new InvalidOperationException("Weekly battle is already active.");
            delegateRunner = gameObject.AddComponent<SkillDamageTestBattleRunner>();
            delegateRunner.BeginWeekly(runtime, stageId);
        }

        private void OnDestroy()
        {
            if (delegateRunner != null) Destroy(delegateRunner);
            delegateRunner = null;
        }
    }
}
