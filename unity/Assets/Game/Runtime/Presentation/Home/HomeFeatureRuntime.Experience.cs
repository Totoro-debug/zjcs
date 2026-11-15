using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeExperienceRules;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private HomeExperienceTables homeExperienceTables;

        private CharacterExperienceState CaptureHomeExperienceState(OfflinePlayerProgress saved, CharacterPropCalculator calculator)
        {
            homeExperienceTables ??= OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
            var source = new OfflineHomeExperienceEnvironmentSource(() => saved, homeExperienceTables,
                () => rules.IsFunctionLimited(173), candidate => {
                    var snapshot = calculator.Evaluate(candidate, profile.Save.World, Now());
                    return CharacterPropFormula.Value(snapshot.NonBattleProps,
                        calculator.Props.Id("PlayerLevelLimit"), calculator.Props);
                });
            return OfflineHomeExperienceProjection.GrowthState(
                OfflineHomeExperienceProjection.Capture(saved, homeExperienceTables, source.Capture()));
        }

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
