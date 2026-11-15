using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.World
{
    // User-requested appearance override; the original APK sprite remains intact.
    public static class DialogueBackgroundAppearance
    {
        public static void Apply(GameObject panel)
        {
            var image = panel.transform.Find("Content/CharacterAndTalkContent/TalkContent/BgImg")?.GetComponent<Image>();
            if (image == null) throw new InvalidOperationException("Official dialogue background binding is missing.");
            var sprite = Resources.Load<Sprite>("Story/TransparentDialogueBackground");
            if (sprite == null) throw new InvalidOperationException("Transparent dialogue background is missing.");
            image.sprite = sprite;
        }
    }
}
