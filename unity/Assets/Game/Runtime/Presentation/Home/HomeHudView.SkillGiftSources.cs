using System;
using System.Collections;
using System.Globalization;
using Zjcs.Presentation.Character;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        private void BindSkillGiftSources(OfficialCharacterPanelView view)
        {
            view.SkillGiftSourceBlockConditionMet = condition => condition == 510
                ? host.FantasyTower.GetEntry(1).clearedLayer >= 210
                : throw new InvalidOperationException("Unmigrated skill-gift source block condition: " + condition);
            view.SkillGiftSourceLimitCode = (type, parameter) => {
                switch (type) {
                    case 8: return host.Rules.IsFunctionLimited(2) ? "FantasyLadder" : null;
                    case 18: return host.Rules.IsFunctionLimited(5, host.PetGachaCatalog.Gacha.ChestId) ? "PetGacha" : null;
                    case 19: return host.Rules.IsFunctionLimited(19, 6) ? "DelegateTask" : null;
                    case 11: return host.MaterialGatheringUnlockedLevel(int.Parse(parameter, CultureInfo.InvariantCulture)) <= 0 ? "NormalMaterial" : null;
                    case 12: return host.ItemCount(int.Parse(parameter, CultureInfo.InvariantCulture)) < 1 ? "FurnitureHangUp" : null;
                    case 22: case 23: case 24:
                        if (host.Rules.IsFunctionLimited(34)) return "FunctionLimitLegion";
                        return string.IsNullOrEmpty(host.Profile.Save.Player.GuildName) ? "LegionNotJoin" : null;
                    default: throw new InvalidOperationException("Unmigrated skill-gift source: " + type);
                }
            };
        }

        private bool NavigateSkillGiftSource(string module)
        {
            if (!module.StartsWith("item-source:", StringComparison.Ordinal)) return false;
            string[] parts = module.Split(':');
            int type = int.Parse(parts[1], CultureInfo.InvariantCulture);
            characterRoot.SetActive(false);
            switch (type) {
                case 8: OpenFantasyTowerEntries(); break;
                case 11: OpenMaterialGathering(int.Parse(parts[2], CultureInfo.InvariantCulture)); break;
                case 18: ShowPetGacha(); break;
                case 19: OpenTaskPanel(host.Profile, NavigateMainlineTask); SelectTaskTab(1); break;
                default:
                    characterRoot.SetActive(true);
                    throw new InvalidOperationException("The destination of skill-gift source " + type + " is not connected.");
            }
            host.StartCoroutine(ReturnFromSkillGiftSource());
            return true;
        }

        private IEnumerator ReturnFromSkillGiftSource()
        {
            yield return null;
            while (AnyModalOpen) yield return null;
            if (characterRoot == null) yield break;
            characterRoot.SetActive(true);
            characterRuntime.ReloadProfile();
        }
    }
}
