using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        internal static void ConfigureAcquiredBattleRequest(OfficialBattleRequest request)
        {
            ConfigureGuardianBattleRequest(request);
            soulSplash?.Configure(request.Checkpoint);
            ConfigureAcquiredBattleRandomInput(request.Checkpoint);
        }
    }
}
