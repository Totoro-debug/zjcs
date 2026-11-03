using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Character
{
    // Character dialogs adapt their existing source document to the common native popup service.
    internal sealed class OfficialSkillPopupMask : MonoBehaviour
    {
        private OfficialBattlePopupMask popup;
        internal bool IsReady => popup != null && popup.IsReady;
        internal float ShownAtUnscaled => popup == null ? 0 : popup.ShownAtUnscaled;

        internal void Show(Dictionary<string, object> templates, int maskType, bool closeByMask, Action onClose, Action onReady)
        {
            var masks = CharacterModelPackage.AsObject(templates["popup_masks"], "original window masks");
            var source = CharacterModelPackage.AsObject(masks[maskType.ToString(CultureInfo.InvariantCulture)], "original window mask");
            var fields = CharacterModelPackage.AsObject(source["fields"], "WindowBlurMask fields");
            string Text(object value) => Convert.ToString(value, CultureInfo.InvariantCulture);
            string prefix = "/WorldCanvas/" + Text(source["prefab_path"]);
            string Relative(string field) => Text(fields[field]) == prefix ? string.Empty : Text(fields[field]).Substring(prefix.Length + 1);
            var binding = new OfficialBattlePopupMask.Binding {
                prefabResource = Text(source["prefab_resource"]), prefabPath = Text(source["prefab_path"]),
                blurMaterial = Text(source["blur_material"]), buttonPath = Relative("mBtn")
            };
            if (maskType == 3)
            {
                binding.imagePath = Relative("mImage");
                var image = CharacterModelPackage.AsObject(source["raw_image"], "original blur RawImage");
                binding.uvRect = ((List<object>)image["m_UVRect"]).Select(value => Convert.ToSingle(value, CultureInfo.InvariantCulture)).ToArray();
            }
            popup = gameObject.AddComponent<OfficialBattlePopupMask>();
            popup.Show(binding, maskType, closeByMask, onClose, onReady);
        }
    }
}
