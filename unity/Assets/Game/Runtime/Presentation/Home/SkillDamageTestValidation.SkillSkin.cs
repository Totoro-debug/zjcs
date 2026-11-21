using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Character;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static IEnumerator SelectAcquiredSkillSkin(OfflineCharacterProgressionService progression,
            int skinId, string output)
        {
            Require(skinId == 39521 && progression.EquippedSkillSkinId(10082) == 0,
                "Start the original frost-song selection with the base water blade.");
            Click(FlameFlowButton(button => button.name == "AppearanceBtn"));
            yield return new WaitForSecondsRealtime(.6f);
            var dialog = FindObjectsOfType<Transform>().Single(node => node.name == "skill_skin_replace");
            Click(dialog.GetComponentsInChildren<Button>().Single(button => button.name == "ItemIconSkill (1)"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(dialog.GetComponentsInChildren<TMP_Text>().Any(label => label.name == "TitleTxt"
                && label.text.Contains("霜歌圣使")), "The original skin dialog did not select frost-song.");
            yield return CaptureAcquiredActive(output, "skin-selection");
            Click(dialog.GetComponentsInChildren<Button>().Single(button => button.name == "ConfirmBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(progression.EquippedSkillSkinId(10082) == skinId,
                "Original skill-skin input did not persist the selected appearance.");
            yield return CaptureAcquiredActive(output, "skin-equipped");
            Require(FindObjectsOfType<CharacterSkillPanel>().Single().HandleEscape(), "The original skin dialog did not handle back input.");
            yield return new WaitForSecondsRealtime(.4f);
            Require(!FindObjectsOfType<Transform>().Any(node => node.name == "skill_skin_replace"), "Original skin back input retained its dialog.");
            Debug.Log("[Acquired active skin] 39521 selected, equipped and persisted through original input.");
        }
    }
}
