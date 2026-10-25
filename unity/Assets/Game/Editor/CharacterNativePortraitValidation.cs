using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static class CharacterNativePortraitValidation
    {
        public static void Validate(string slot, string root)
        {
            CharacterPortraitPhotography settings = CharacterPortraitPhotography.LoadBattleHead();
            var session = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            var progression = new OfflineCharacterProgressionService(session, OfficialCharacterAssetCatalog.LoadDefault().Load());
            var appearance = progression.ResolveAppearance();
            foreach (int type in settings.excludes) appearance.SkinByType.Remove(type);
            GameObject model = CharacterCreationNativeModelFactory.CreatePreviewModel(slot, root, appearance);
            Texture2D texture = null;
            try
            {
                model.transform.position = new Vector3(10000, 10000, 0);
                Animator animator = model.GetComponent<Animator>();
                if (!OfficialBattleCharacterAnimationCatalog.TryResolve(animator.runtimeAnimatorController,
                    settings.animation, out string state)) throw new InvalidOperationException("Original head pose is not converted.");
                texture = CharacterNativePortraitCapture.Capture(model, settings, state);
                Color32[] pixels = texture.GetPixels32();
                int visible = pixels.Count(value => value.a > 128);
                int colors = pixels.Where(value => value.a > 128)
                    .Select(value => value.r * 65536 + value.g * 256 + value.b).Distinct().Count();
                if (texture.width != 100 || texture.height != 120 || visible < 1000 || visible > 11500 || colors < 50)
                    throw new InvalidOperationException("Native head capture is empty, opaque or lacks original character pixels: visible=" + visible + " colors=" + colors);
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.tools/battle-system-migration"));
                Directory.CreateDirectory(output);
                string sex = session.Save.Player.Wardrobe.Sex == 1 ? "female" : "male";
                File.WriteAllBytes(Path.Combine(output, "native-head-" + sex + ".png"), texture.EncodeToPNG());
                Debug.Log("Official native head photography passed: " + sex + " pose=" + state
                    + " visible=" + visible + " colors=" + colors + ".");
            }
            finally
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                UnityEngine.Object.Destroy(model);
            }
        }
    }
}
