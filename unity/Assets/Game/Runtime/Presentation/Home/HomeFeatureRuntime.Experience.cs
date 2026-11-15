using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        internal OfflineHomeExperienceEnvironmentSource CreateHomeExperienceEnvironment(HomeExperienceTables tables)
        {
            EnsureProgression();
            return new OfflineHomeExperienceEnvironmentSource(() => profile.Save.Player, tables,
                () => rules.IsFunctionLimited(173), saved => {
                    var calculator = progression.PropertyCalculator;
                    var snapshot = calculator.Evaluate(saved, profile.Save.World, Now());
                    return CharacterPropFormula.Value(snapshot.NonBattleProps,
                        calculator.Props.Id("PlayerLevelLimit"), calculator.Props);
                });
        }
    }
}
