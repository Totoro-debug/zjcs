using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialBattleProgressDriver
    {
        public static bool Tick(OfficialBattleRuntimeController controller, ref float remainingSeconds,
            float deltaTime)
        {
            if (controller == null || !controller.CanAdvance) return false;
            remainingSeconds -= Mathf.Max(0f, deltaTime);
            if (remainingSeconds > 0f) return false;

            controller.Advance();
            remainingSeconds = controller.LastStepSeconds;
            return true;
        }
    }
}
