using System.Collections.Generic;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleHudView
    {
        private readonly HashSet<int> activeSkillSlots = new HashSet<int>();
        private readonly HashSet<int> equippedSkillSlots = new HashSet<int>();

        // Original FightMainPanelSkillUI provider callbacks. Before the delayed
        // HUD mounts, retain active slots so RefreshView shows the live skill.
        public void OnSkillStarted(int unitId, int slot)
        {
            if (unitId != 1 || slot < 0 || skillSlots != null && slot >= skillSlots.Length) return;
            activeSkillSlots.Add(slot);
            if (skillSlots != null && equippedSkillSlots.Contains(slot))
                skillSlots[slot].SetUIState(WorldGameSkillIcon.State.Applied);
        }

        public void OnSkillEnded(int unitId, int slot)
        {
            if (unitId != 1 || slot < 0 || skillSlots != null && slot >= skillSlots.Length) return;
            activeSkillSlots.Remove(slot);
            if (skillSlots != null && equippedSkillSlots.Contains(slot))
                skillSlots[slot].SetUIState(WorldGameSkillIcon.State.Ended);
        }
    }
}
