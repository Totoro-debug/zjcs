using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;

namespace Zjcs.Editor
{
    public static class CharacterNativePortraitValidation
    {
        public static void Validate(string slot, string root)
        {
            var session = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            var progression = new OfflineCharacterProgressionService(session, OfficialCharacterAssetCatalog.LoadDefault().Load());
            var owner = new GameObject("Shared battle portrait validation owner");
            try
            {
                string before = JsonUtility.ToJson(session.Save);
                var portrait = OfficialBattleCharacterPortraits.ForProfile(owner, progression, slot, root);
                var player = new FightBattleUnitState { Id = 1, CharacterType = 1 };
                Sprite sprite = portrait(player);
                if (sprite == null || portrait(player) != sprite || owner.GetComponents<CharacterNativePortraitLease>().Length != 1
                    || portrait(new FightBattleUnitState { Id = 2, CharacterType = 2, MonsterId = 3207 }) != null
                    || portrait(new FightBattleUnitState { Id = 3, CharacterType = 1, NpcClassId = 8402 }) != null
                    || JsonUtility.ToJson(session.Save) != before)
                    throw new InvalidOperationException("Shared battle photography did not preserve ownership, reuse, portrait branches or profile state.");
                Texture2D texture = sprite.texture;
                Color32[] pixels = texture.GetPixels32();
                int visible = pixels.Count(value => value.a > 128);
                int colors = pixels.Where(value => value.a > 128)
                    .Select(value => value.r * 65536 + value.g * 256 + value.b).Distinct().Count();
                string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set ZJCS_VALIDATION_DIR for native head validation.");
                output = Path.Combine(output, "portraits");
                Directory.CreateDirectory(output);
                string sex = session.Save.Player.Wardrobe.Sex == 1 ? "female" : "male";
                File.WriteAllBytes(Path.Combine(output, "native-head-" + sex + ".png"), texture.EncodeToPNG());
                if (texture.width != 100 || texture.height != 120 || visible < 1000 || visible > 11500 || colors < 50)
                    throw new InvalidOperationException("Native head capture is empty, opaque or lacks original character pixels: visible=" + visible + " colors=" + colors);
                Debug.Log("Official shared native head photography passed: " + sex
                    + " visible=" + visible + " colors=" + colors + ".");
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
            }
        }
    }
}
